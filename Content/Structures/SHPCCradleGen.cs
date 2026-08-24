using CalamityOverhaul.Content.LegendWeapon.SHPCLegend;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.MoldProcessingTables;
using CalamityOverhaul.Content.UIs.OverhaulSettings;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Structures
{
    /// <summary>
    /// SHPC 坠舱空岛：出生点上空程序生成的坠毁舱段，SHPC 的开局获取点<br/>
    /// 纯代码构建，无结构资产依赖；全部使用原版物块（石土岩体 + 锡镀层舱体）<br/>
    /// 布局为三舱式：外舱（补给桶）→ 中舱（模具加工台）→ 内舱（SHPC 箱），
    /// 舱尾从岩体中探出并留有撞击痕。<br/>
    /// 动线：岛底主锚绳一路下探到地面，落点烧出焦土与镀层碎片，
    /// 这块地面痕迹才是玩家在出生点真正能读到的线索
    /// </summary>
    internal static class SHPCCradleGen
    {
        /// <summary>密度设置键，注册于 <see cref="WorldGenDensitySave.StructureNames"/></summary>
        internal const string DensityKey = "SHPCCradle";

        /// <summary>
        /// 本世界是否走坠舱路线；为假时行星实验室保留 SHPC 兜底（见 ModifyStructure）。<br/>
        /// 倒置世界（Don't Dig Up）出生点被放在世界最底部，"出生点上空"就是地狱，
        /// 坠舱语义不成立，直接交回行星实验室
        /// </summary>
        internal static bool Enabled => WorldGenDensitySave.GetDensity(DensityKey) != StructureDensity.Extinction
            && !Main.remixWorld;

        //════════ 几何常量（局部坐标，箱体左上为原点） ════════

        private const int BoxW = 46;
        private const int BoxH = 28;

        //岩体椭圆
        private const float RockCenterX = 22.5f;
        private const int RockCenterY = 15;
        private const float RockRadiusX = 21f;
        private const float RockRadiusY = 8f;
        /// <summary>岩体下半拉伸，底部更厚重</summary>
        private const float RockBellyStretch = 1.35f;

        //舱体：顶板 6，内部 7-12，地板 13，总高 8
        private const int HullRoofY = 6;
        private const int HullFloorY = 13;
        private const int HullWidth = 28;
        /// <summary>门洞占内部下三行（10-12）</summary>
        private const int DoorTopY = 10;

        //放置搜索
        private const int AirMargin = 6;
        private const int MinHeightAboveSpawn = 40;
        private const int WorldEdgePad = 60;

        //垂绳
        /// <summary>主锚绳最大下探格数，足够从常规空岛高度落到地面</summary>
        private const int AnchorRopeMaxLength = 240;
        /// <summary>另一侧的断绳，坠落时崩断的那根</summary>
        private const int TornRopeLength = 9;

        //舱内布局：一律用"距舱尾偏移"表达，两个入口朝向共用同一张表
        private const int OuterTorchOffset = 2;
        private const int SupplyChestOffset = 5;
        private const int TerminalOffset = 11;
        private const int TableOffset = 15;
        private const int MidTorchOffset = 19;
        private const int InnerTorchOffset = 22;
        private const int ShpcChestOffset = 24;

        /// <summary>世界生成入口</summary>
        internal static bool Generate() {
            if (!TryFindSkyPlacement(out Point16 origin)) {
                CWRMod.Instance.Logger.Warn("[SHPCCradle] no sky placement found; falling back to an existing chest.");
                //行星实验室的箱子早在 Final Cleanup 之前就填完了，回填不了，
                //只能自己找一个已存在的容器，否则 SHPC 这局彻底不可得
                if (!TryStuffIntoNearestChest()) {
                    CWRMod.Instance.Logger.Error("[SHPCCradle] no chest available either; SHPC unobtainable in this world!");
                }
                return false;
            }
            Build(origin);
            CWRMod.Instance.Logger.Info($"[SHPCCradle] placed at {origin.X}, {origin.Y}.");
            return true;
        }

        //════════ 寻位 ════════

        /// <summary>
        /// 在出生点上空寻找落点。
        /// 不借用 SaveStructure.FindSafePlacement：它尝试耗尽后会硬放在 startY-50，
        /// 对空岛意味着可能叠进原版浮岛
        /// </summary>
        private static bool TryFindSkyPlacement(out Point16 origin) {
            origin = default;

            //太空层下界；topY 越小越高，不允许高过这条线
            int minTopY = (int)(Main.worldSurface * 0.35) + 20;
            int maxTopY = Main.spawnTileY - MinHeightAboveSpawn - BoxH;
            if (maxTopY < minTopY) {
                return false;
            }

            //阶段一：出生点两侧 40-90 格、上方 60-100 格的理想带内随机取点
            for (int attempt = 0; attempt < 48; attempt++) {
                int dir = WorldGen.genRand.NextBool() ? 1 : -1;
                int centerX = Main.spawnTileX + dir * WorldGen.genRand.Next(40, 91);
                int topY = Math.Clamp(Main.spawnTileY - WorldGen.genRand.Next(60, 101) - BoxH, minTopY, maxTopY);
                if (TestPlacement(centerX, topY, out origin)) {
                    return true;
                }
            }

            //阶段二：确定性横向扫描，逐步远离出生点，保证正常世界必有落点
            int[] bottomOffsets = [70, 90, 110, 55];
            for (int dist = 40; dist <= 400; dist += 8) {
                for (int side = 0; side < 2; side++) {
                    int centerX = Main.spawnTileX + (side == 0 ? dist : -dist);
                    foreach (int offset in bottomOffsets) {
                        int topY = Math.Clamp(Main.spawnTileY - offset - BoxH, minTopY, maxTopY);
                        if (TestPlacement(centerX, topY, out origin)) {
                            return true;
                        }
                    }
                }
            }

            //阶段三：放宽留白要求。空岛是 ClearBox 平地起的，强放的代价只是碰掉几格原版天空物块，
            //而寻位失败的代价是 SHPC 整局不可得，两者不对等
            return TryFindRelaxedPlacement(minTopY, maxTopY, out origin);
        }

        /// <summary>候选中心点展开为左上角并做边界与纯空气校验</summary>
        private static bool TestPlacement(int centerX, int topY, out Point16 origin) {
            origin = default;
            int left = centerX - BoxW / 2;
            int padLeft = left - AirMargin;
            int padTop = topY - AirMargin;
            int padW = BoxW + AirMargin * 2;
            int padH = BoxH + AirMargin * 2;

            if (padLeft < WorldEdgePad || padLeft + padW > Main.maxTilesX - WorldEdgePad) {
                return false;
            }
            if (padTop < WorldEdgePad || padTop + padH > Main.maxTilesY - WorldEdgePad) {
                return false;
            }

            for (int x = padLeft; x < padLeft + padW; x++) {
                for (int y = padTop; y < padTop + padH; y++) {
                    Tile tile = Framing.GetTileSafely(x, y);
                    if (tile.HasTile || tile.LiquidAmount > 0) {
                        return false;
                    }
                }
            }

            origin = new Point16(left, topY);
            return true;
        }

        /// <summary>
        /// 保底寻位：不再要求周边留白，只挑箱体范围内阻挡最少的落点。
        /// 找到全空的立即用，否则取最优候选强放
        /// </summary>
        private static bool TryFindRelaxedPlacement(int minTopY, int maxTopY, out Point16 origin) {
            origin = default;
            int bestBlocked = int.MaxValue;

            int[] bottomOffsets = [70, 90, 110, 55, 130];
            for (int dist = 30; dist <= 500; dist += 6) {
                for (int side = 0; side < 2; side++) {
                    int left = Main.spawnTileX + (side == 0 ? dist : -dist) - BoxW / 2;
                    if (left < WorldEdgePad || left + BoxW > Main.maxTilesX - WorldEdgePad) {
                        continue;
                    }
                    foreach (int offset in bottomOffsets) {
                        int topY = Math.Clamp(Main.spawnTileY - offset - BoxH, minTopY, maxTopY);
                        if (topY < WorldEdgePad || topY + BoxH > Main.maxTilesY - WorldEdgePad) {
                            continue;
                        }
                        int blocked = CountBlocked(left, topY);
                        if (blocked >= bestBlocked) {
                            continue;
                        }
                        bestBlocked = blocked;
                        origin = new Point16(left, topY);
                        if (blocked == 0) {
                            return true;
                        }
                    }
                }
            }

            if (bestBlocked == int.MaxValue) {
                return false;
            }
            CWRMod.Instance.Logger.Warn($"[SHPCCradle] relaxed placement used; {bestBlocked} tiles will be cleared.");
            return true;
        }

        /// <summary>箱体范围内的物块/液体格数，越小越适合强放</summary>
        private static int CountBlocked(int left, int topY) {
            int count = 0;
            for (int x = left; x < left + BoxW; x++) {
                for (int y = topY; y < topY + BoxH; y++) {
                    Tile tile = Framing.GetTileSafely(x, y);
                    if (tile.HasTile || tile.LiquidAmount > 0) {
                        count++;
                    }
                }
            }
            return count;
        }

        /// <summary>寻位彻底失败时的最后手段：把 SHPC 塞进离出生点最近的已有箱子</summary>
        private static bool TryStuffIntoNearestChest() {
            List<Chest> chests = [];
            for (int i = 0; i < Main.maxChests; i++) {
                if (Main.chest[i] != null) {
                    chests.Add(Main.chest[i]);
                }
            }
            if (chests.Count == 0) {
                return false;
            }

            int Distance(Chest chest)
                => Math.Abs(chest.x - Main.spawnTileX) + Math.Abs(chest.y - Main.spawnTileY);
            chests.Sort((a, b) => Distance(a).CompareTo(Distance(b)));

            foreach (Chest chest in chests) {
                if (AddChestItem(chest, SHPCOverride.ID, 1)) {
                    CWRMod.Instance.Logger.Warn($"[SHPCCradle] SHPC put into fallback chest at {chest.x}, {chest.y}.");
                    return true;
                }
            }
            return false;
        }

        //════════ 构建 ════════

        /// <summary>在 origin（箱体左上角）处构建整座坠舱空岛；调试命令也走此入口</summary>
        internal static void Build(Point16 origin) {
            //舱尾（入口端）朝向随机
            int entryDir = WorldGen.genRand.NextBool() ? 1 : -1;
            int hullX0 = entryDir == 1 ? 12 : BoxW - 12 - HullWidth;   //12 或 6
            int hullX1 = hullX0 + HullWidth - 1;
            int tailX = entryDir == 1 ? hullX1 : hullX0;

            ClearBox(origin);
            BuildRock(origin);
            BuildHull(origin, hullX0, hullX1, tailX, entryDir);
            CarveCrash(origin, hullX0, hullX1, tailX, entryDir);
            GrowGrass(origin);
            Point16 landing = PlaceRopes(origin);

            //本 pass 位于 Final Cleanup 之后，没有后续整帧机会，必须自框
            int frameBottom = landing.X >= 0 ? landing.Y + 2 : origin.Y + BoxH + TornRopeLength + 2;
            WorldGen.RangeFrame(origin.X - 1, origin.Y - 1, origin.X + BoxW + 1, frameBottom);

            MarkImpactGround(landing);
            Furnish(origin, tailX, entryDir);
        }

        /// <summary>清空箱体（物块/墙/液体），保证调试重复构建与世界生成一致</summary>
        private static void ClearBox(Point16 origin) {
            for (int lx = 0; lx < BoxW; lx++) {
                for (int ly = 0; ly < BoxH; ly++) {
                    Tile tile = Framing.GetTileSafely(origin.X + lx, origin.Y + ly);
                    tile.HasTile = false;
                    tile.WallType = WallID.None;
                    tile.LiquidAmount = 0;
                }
            }
        }

        /// <summary>椭圆 + 每列随机游走噪声的岩体，顶部两层土</summary>
        private static void BuildRock(Point16 origin) {
            int topWalk = 0;
            int botWalk = 0;
            for (int lx = 0; lx < BoxW; lx++) {
                float dx = (lx - RockCenterX) / RockRadiusX;
                float t = 1f - dx * dx;
                if (t <= 0.02f) {
                    continue;
                }
                float half = RockRadiusY * MathF.Sqrt(t);

                topWalk = Math.Clamp(topWalk + WorldGen.genRand.Next(-1, 2), -2, 2);
                botWalk = Math.Clamp(botWalk + WorldGen.genRand.Next(-1, 2), -3, 3);
                int top = Math.Max(3, (int)(RockCenterY - half) + topWalk);
                int bottom = Math.Min(BoxH - 1, (int)(RockCenterY + half * RockBellyStretch) + botWalk);

                for (int ly = top; ly <= bottom; ly++) {
                    ushort type = ly <= top + 1 ? TileID.Dirt : TileID.Stone;
                    PlaceSolid(origin.X + lx, origin.Y + ly, type);
                }
            }
        }

        /// <summary>锡镀层舱体：外壳、内部掏空刷墙、两道隔舱壁与门洞、入口门洞</summary>
        private static void BuildHull(Point16 origin, int hullX0, int hullX1, int tailX, int entryDir) {
            //外壳：顶板/地板 + 两端壁
            for (int lx = hullX0; lx <= hullX1; lx++) {
                PlaceSolid(origin.X + lx, origin.Y + HullRoofY, TileID.TinPlating);
                PlaceSolid(origin.X + lx, origin.Y + HullFloorY, TileID.TinPlating);
            }
            for (int ly = HullRoofY; ly <= HullFloorY; ly++) {
                PlaceSolid(origin.X + hullX0, origin.Y + ly, TileID.TinPlating);
                PlaceSolid(origin.X + hullX1, origin.Y + ly, TileID.TinPlating);
            }

            //内舱区间：距舱尾 21-26 格（用于差异化墙面）
            (int innerMin, int innerMax) = RoomSpan(tailX, entryDir, 21, 26);

            //内部掏空并刷墙
            for (int lx = hullX0 + 1; lx <= hullX1 - 1; lx++) {
                bool innerRoom = lx >= innerMin && lx <= innerMax;
                for (int ly = HullRoofY + 1; ly <= HullFloorY - 1; ly++) {
                    Tile tile = Framing.GetTileSafely(origin.X + lx, origin.Y + ly);
                    tile.HasTile = false;
                    tile.LiquidAmount = 0;
                    tile.WallType = innerRoom ? WallID.TinPlating : WallID.IronBrick;
                }
            }

            //两道隔舱壁：距舱尾 9 与 20，下三行（10-12）留门洞
            foreach (int offset in new[] { 9, 20 }) {
                int lx = AtOffset(tailX, entryDir, offset);
                for (int ly = HullRoofY + 1; ly < DoorTopY; ly++) {
                    PlaceSolid(origin.X + lx, origin.Y + ly, TileID.TinPlating);
                }
            }

            //舱尾端壁开入口门洞（10-12）
            for (int ly = DoorTopY; ly <= HullFloorY - 1; ly++) {
                Tile tile = Framing.GetTileSafely(origin.X + tailX, origin.Y + ly);
                tile.HasTile = false;
            }
        }

        /// <summary>撞击痕：入口前廊清空、尾半段顶部撬开露出顶板、灰烬灼痕与镀层碎片</summary>
        private static void CarveCrash(Point16 origin, int hullX0, int hullX1, int tailX, int entryDir) {
            //入口前廊：舱尾外侧 5 格、门洞至顶板高度清空，保证落点必然可走进来
            for (int step = 1; step <= 5; step++) {
                int lx = tailX + entryDir * step;
                if (lx < 0 || lx >= BoxW) {
                    break;
                }
                for (int ly = HullRoofY; ly <= HullFloorY - 1; ly++) {
                    Tile tile = Framing.GetTileSafely(origin.X + lx, origin.Y + ly);
                    tile.HasTile = false;
                }
            }

            //尾半段顶部随机撬开，露出顶板形成撞击擦痕
            int scarMin = Math.Min(tailX, tailX - entryDir * (HullWidth / 2));
            int scarMax = Math.Max(tailX, tailX - entryDir * (HullWidth / 2));
            for (int lx = scarMin; lx <= scarMax; lx++) {
                if (lx < hullX0 || lx > hullX1 || !WorldGen.genRand.NextBool(5, 12)) {
                    continue;
                }
                for (int ly = 0; ly < HullRoofY; ly++) {
                    Tile tile = Framing.GetTileSafely(origin.X + lx, origin.Y + ly);
                    tile.HasTile = false;
                }
            }

            //灼痕与碎片：擦痕带附近的岩面部分转灰烬，散落少量镀层单块
            for (int lx = Math.Max(0, scarMin - 4); lx <= Math.Min(BoxW - 1, scarMax + 4); lx++) {
                if (!TryFindSurface(origin, lx, out int surfaceY)) {
                    continue;
                }
                Tile surface = Framing.GetTileSafely(origin.X + lx, origin.Y + surfaceY);
                if (surface.TileType is TileID.Dirt or TileID.Stone && WorldGen.genRand.NextBool(3)) {
                    surface.TileType = TileID.Ash;
                }
                if (surfaceY > HullRoofY && WorldGen.genRand.NextBool(7)) {
                    PlaceSolid(origin.X + lx, origin.Y + surfaceY - 1, TileID.TinPlating);
                }
            }
        }

        /// <summary>暴露在空气下的表层土转草，给远景剪影一点生机</summary>
        private static void GrowGrass(Point16 origin) {
            for (int lx = 0; lx < BoxW; lx++) {
                if (!TryFindSurface(origin, lx, out int surfaceY)) {
                    continue;
                }
                Tile surface = Framing.GetTileSafely(origin.X + lx, origin.Y + surfaceY);
                if (surface.TileType == TileID.Dirt) {
                    surface.TileType = TileID.Grass;
                }
            }
        }

        /// <summary>
        /// 岛底两条垂绳：主锚绳一路下探到地面，把空岛真正接进开局动线；
        /// 另一侧只留一截断绳。<br/>
        /// 返回主锚绳最末一格的世界坐标，没铺成时 X 为 -1
        /// </summary>
        private static Point16 PlaceRopes(Point16 origin) {
            int anchorLx = (int)RockCenterX - 5;
            int tornLx = (int)RockCenterX + 5;

            int anchorEndY = DropRope(origin, anchorLx, AnchorRopeMaxLength);
            DropRope(origin, tornLx, TornRopeLength);

            return anchorEndY < 0 ? new Point16(-1, -1) : new Point16(origin.X + anchorLx, anchorEndY);
        }

        /// <summary>从该列岩底往下铺绳，撞到物块即止；返回最末一格绳的世界 Y，没铺成返回 -1</summary>
        private static int DropRope(Point16 origin, int lx, int maxLength) {
            int bottomLy = -1;
            for (int ly = BoxH - 1; ly >= 0; ly--) {
                Tile tile = Framing.GetTileSafely(origin.X + lx, origin.Y + ly);
                if (tile.HasTile && Main.tileSolid[tile.TileType]) {
                    bottomLy = ly;
                    break;
                }
            }
            if (bottomLy < 0) {
                return -1;
            }

            int lastY = -1;
            for (int step = 1; step <= maxLength; step++) {
                int y = origin.Y + bottomLy + step;
                if (y >= Main.maxTilesY - WorldEdgePad) {
                    break;
                }
                Tile tile = Framing.GetTileSafely(origin.X + lx, y);
                if (tile.HasTile) {
                    break;
                }
                tile.HasTile = true;
                tile.TileType = TileID.Rope;
                lastY = y;
            }
            return lastY;
        }

        /// <summary>
        /// 锚绳落点的地面撞击痕：焦土加几块崩落的镀层碎片。
        /// 空岛本身在两三屏之外，地面上这块痕迹才是玩家在出生点能读到的线索
        /// </summary>
        private static void MarkImpactGround(Point16 landing) {
            if (landing.X < 0) {
                return;
            }

            //整片焦痕共用一个半径，否则每列各随机会长成锯齿状
            int scorchRadius = 3 + WorldGen.genRand.Next(2);
            for (int dx = -5; dx <= 5; dx++) {
                int x = landing.X + dx;
                if (x < WorldEdgePad || x >= Main.maxTilesX - WorldEdgePad) {
                    continue;
                }
                //从锚绳末端上方起扫：落点两侧地形抬升时，直接从末端往下会扫进山体内部
                if (!TryFindGroundBelow(x, landing.Y - 8, out int groundY)) {
                    continue;
                }

                Tile ground = Framing.GetTileSafely(x, groundY);
                if (Math.Abs(dx) <= scorchRadius
                    && ground.TileType is TileID.Dirt or TileID.Grass or TileID.Stone or TileID.Sand) {
                    ground.TileType = TileID.Ash;
                }

                //碎片落在焦痕外围，像是撞击弹开的
                if (Math.Abs(dx) >= 2 && WorldGen.genRand.NextBool(4)
                    && !Framing.GetTileSafely(x, groundY - 1).HasTile) {
                    PlaceSolid(x, groundY - 1, TileID.TinPlating);
                }
            }

            WorldGen.RangeFrame(landing.X - 7, landing.Y - 10, landing.X + 7, landing.Y + 44);
        }

        //════════ 家具与战利品 ════════

        /// <summary>外舱补给桶、中舱模具加工台、内舱 SHPC 箱与蓝火把；关键物均有兜底路径</summary>
        private static void Furnish(Point16 origin, int tailX, int entryDir) {
            int furnitureY = HullFloorY - 1;

            //蓝火把先放：三舱各一，坠舱应急照明的冷色；靠墙锚定，不占地板
            foreach (int offset in new[] { OuterTorchOffset, MidTorchOffset, InnerTorchOffset }) {
                WorldGen.PlaceTile(origin.X + AtOffset(tailX, entryDir, offset), origin.Y + DoorTopY,
                    TileID.Torches, mute: true, forced: false, plr: -1, style: TorchID.Blue);
            }

            //外舱补给桶（Containers style 5 = 木桶）
            int supplyChest = WorldGen.PlaceChest(origin.X + AtOffset(tailX, entryDir, SupplyChestOffset),
                origin.Y + furnitureY, TileID.Containers, notNearOtherChests: false, style: 5);
            if (supplyChest >= 0) {
                FillSupplyChest(Main.chest[supplyChest]);
            }

            //中舱模具加工台（4x3，Origin 在 (1,2)，锚满宽地板）
            int tableType = ModContent.TileType<MoldProcessingTableTile>();
            bool tablePlaced = WorldGen.PlaceObject(origin.X + AtOffset(tailX, entryDir, TableOffset),
                origin.Y + furnitureY, tableType, mute: true);

            //中舱旧网接入终端：坠舱既是 SHPC 的家也是深潜口，碎片从这里出发也从这里铭刻
            PlaceAccessTerminal(origin.X + AtOffset(tailX, entryDir, TerminalOffset), origin.Y + furnitureY);

            //内舱 SHPC 箱（Containers style 13 = 天域箱，见原版 SkywareChest 物品的 placeStyle）
            int shpcChest = WorldGen.PlaceChest(origin.X + AtOffset(tailX, entryDir, ShpcChestOffset),
                origin.Y + furnitureY, TileID.Containers, notNearOtherChests: false, style: 13);

            //SHPC 落位：内舱箱 → 补给桶兜底 → 全世界最近的箱子 → 记错误
            int shpcHome = shpcChest >= 0 ? shpcChest : supplyChest;
            if (shpcHome >= 0 && AddChestItem(Main.chest[shpcHome], SHPCOverride.ID, 1)) {
                CWRMod.Instance.Logger.Info("Shoving SHPC into the cradle chest.");
                //加工台物块放置失败时把物品塞进同一个箱子，产线不断档
                if (!tablePlaced) {
                    AddChestItem(Main.chest[shpcHome], ModContent.ItemType<MoldProcessingTable>(), 1);
                    CWRMod.Instance.Logger.Warn("[SHPCCradle] table tile placement failed; item added to chest instead.");
                }
                return;
            }

            CWRMod.Instance.Logger.Warn("[SHPCCradle] cradle chests unusable; trying world chests.");
            if (!TryStuffIntoNearestChest()) {
                CWRMod.Instance.Logger.Error("[SHPCCradle] SHPC unobtainable from cradle!");
            }
        }

        /// <summary>
        /// 旧网终端手铺：该 tile 没有 TileObjectData，过不了 WorldGen.PlaceTile 的放置校验，
        /// 只能直写；本 pass 在 Final Cleanup 之后，帧要自己补
        /// </summary>
        private static void PlaceAccessTerminal(int x, int y) {
            Tile slot = Framing.GetTileSafely(x, y);
            if (slot.HasTile) {
                CWRMod.Instance.Logger.Warn("[SHPCCradle] OldNet access terminal slot occupied; skipped.");
                return;
            }
            slot.HasTile = true;
            slot.TileType = (ushort)ModContent.TileType<Scenarios.OldNet.Tiles.OldNetAccessTerminalTile>();
            slot.TileFrameX = 0;
            slot.TileFrameY = 0;
            slot.LiquidAmount = 0;
            WorldGen.SquareTileFrame(x, y);
        }

        /// <summary>开局向补给：绳、火把、木材、小回复、荧光棒与一点碎银</summary>
        private static void FillSupplyChest(Chest chest) {
            AddChestItem(chest, ItemID.Rope, WorldGen.genRand.Next(40, 61));
            AddChestItem(chest, ItemID.Torch, WorldGen.genRand.Next(15, 26));
            AddChestItem(chest, ItemID.Wood, WorldGen.genRand.Next(30, 51));
            AddChestItem(chest, ItemID.LesserHealingPotion, WorldGen.genRand.Next(3, 6));
            AddChestItem(chest, ItemID.Glowstick, WorldGen.genRand.Next(10, 21));
            AddChestItem(chest, ItemID.SilverCoin, WorldGen.genRand.Next(20, 61));
        }

        /// <summary>找列内自上而下第一块实心物块</summary>
        private static bool TryFindSurface(Point16 origin, int lx, out int surfaceY) {
            for (int ly = 0; ly < BoxH; ly++) {
                Tile tile = Framing.GetTileSafely(origin.X + lx, origin.Y + ly);
                if (tile.HasTile && Main.tileSolid[tile.TileType]) {
                    surfaceY = ly;
                    return true;
                }
            }
            surfaceY = -1;
            return false;
        }

        /// <summary>从给定高度往下找第一块实心物块，限 48 格内</summary>
        private static bool TryFindGroundBelow(int x, int fromY, out int groundY) {
            int limit = Math.Min(fromY + 48, Main.maxTilesY - WorldEdgePad);
            for (int y = Math.Max(fromY, WorldEdgePad); y < limit; y++) {
                Tile tile = Framing.GetTileSafely(x, y);
                if (tile.HasTile && Main.tileSolid[tile.TileType]) {
                    groundY = y;
                    return true;
                }
            }
            groundY = -1;
            return false;
        }

        /// <summary>按距舱尾的偏移区间求房间的左右界（自动适配入口朝向）</summary>
        private static (int min, int max) RoomSpan(int tailX, int entryDir, int fromOffset, int toOffset) {
            int a = AtOffset(tailX, entryDir, fromOffset);
            int b = AtOffset(tailX, entryDir, toOffset);
            return (Math.Min(a, b), Math.Max(a, b));
        }

        /// <summary>距舱尾偏移 → 局部 X，两个入口朝向共用同一张布局表</summary>
        private static int AtOffset(int tailX, int entryDir, int offset) => tailX - entryDir * offset;

        private static void PlaceSolid(int x, int y, ushort type) {
            Tile tile = Framing.GetTileSafely(x, y);
            tile.HasTile = true;
            tile.TileType = type;
            tile.Slope = SlopeType.Solid;
            tile.IsHalfBlock = false;
            tile.LiquidAmount = 0;
        }

        /// <summary>塞进第一个空格；箱子满或参数无效返回 false</summary>
        private static bool AddChestItem(Chest chest, int itemType, int stack) {
            if (chest == null || itemType <= 0 || stack <= 0) {
                return false;
            }
            for (int i = 0; i < chest.item.Length; i++) {
                Item item = chest.item[i];
                if (item != null && item.type != ItemID.None) {
                    continue;
                }
                chest.item[i] = new Item(itemType, stack);
                return true;
            }
            return false;
        }
    }
}
