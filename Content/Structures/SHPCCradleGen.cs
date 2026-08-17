using CalamityOverhaul.Content.LegendWeapon.SHPCLegend;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.MoldProcessingTables;
using CalamityOverhaul.Content.UIs.OverhaulSettings;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Structures
{
    /// <summary>
    /// SHPC 坠舱空岛：出生点上空程序生成的坠毁舱段，SHPC 的开局获取点。<br/>
    /// 纯代码构建，无结构资产依赖；全部使用原版物块（石土岩体 + 锡镀层舱体）。<br/>
    /// 布局为三舱式：外舱（补给桶）→ 中舱（模具加工台）→ 内舱（SHPC 箱），
    /// 舱尾从岩体中探出并留有撞击痕，岛底垂绳提示可达
    /// </summary>
    internal static class SHPCCradleGen
    {
        /// <summary>密度设置键，注册于 <see cref="WorldGenDensitySave.StructureNames"/></summary>
        internal const string DensityKey = "SHPCCradle";

        /// <summary>本世界是否生成坠舱；为假时行星实验室保留 SHPC 兜底（见 ModifyStructure）</summary>
        internal static bool Enabled => WorldGenDensitySave.GetDensity(DensityKey) != StructureDensity.Extinction;

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
        private const int RopeLength = 14;

        /// <summary>世界生成入口：寻位失败则放弃并 Warn，绝不硬放</summary>
        internal static bool Generate() {
            if (!TryFindSkyPlacement(out Point16 origin)) {
                CWRMod.Instance.Logger.Warn("[SHPCCradle] no clear sky area found near spawn; generation skipped.");
                return false;
            }
            Build(origin);
            CWRMod.Instance.Logger.Info($"[SHPCCradle] placed at {origin.X}, {origin.Y}.");
            return true;
        }

        //════════ 寻位 ════════

        /// <summary>
        /// 在出生点上空寻找一块含留白的纯空气区域。
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
            return false;
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
            PlaceRopes(origin);

            //本 pass 位于 Final Cleanup 之后，没有后续整帧机会，必须自框
            WorldGen.RangeFrame(origin.X - 1, origin.Y - 1,
                origin.X + BoxW + 1, origin.Y + BoxH + RopeLength + 2);

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
                int lx = tailX - entryDir * offset;
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

        /// <summary>岛底两条垂绳：可达性的无言提示，锚在岩腹之下</summary>
        private static void PlaceRopes(Point16 origin) {
            foreach (int lx in new[] { (int)RockCenterX - 5, (int)RockCenterX + 5 }) {
                int bottomY = -1;
                for (int ly = BoxH - 1; ly >= 0; ly--) {
                    Tile tile = Framing.GetTileSafely(origin.X + lx, origin.Y + ly);
                    if (tile.HasTile && Main.tileSolid[tile.TileType]) {
                        bottomY = ly;
                        break;
                    }
                }
                if (bottomY < 0) {
                    continue;
                }
                for (int step = 1; step <= RopeLength; step++) {
                    Tile tile = Framing.GetTileSafely(origin.X + lx, origin.Y + bottomY + step);
                    if (tile.HasTile) {
                        break;
                    }
                    tile.HasTile = true;
                    tile.TileType = TileID.Rope;
                }
            }
        }

        //════════ 家具与战利品 ════════

        /// <summary>外舱补给桶、中舱模具加工台、内舱 SHPC 箱与蓝火把；关键物均有兜底路径</summary>
        private static void Furnish(Point16 origin, int tailX, int entryDir) {
            (int outerMin, _) = RoomSpan(tailX, entryDir, 1, 8);
            (int midMin, _) = RoomSpan(tailX, entryDir, 10, 19);
            (int innerMin, _) = RoomSpan(tailX, entryDir, 21, 26);

            int furnitureY = HullFloorY - 1;

            //蓝火把先放：三舱各一，坠舱应急照明的冷色
            foreach (int lx in new[] { outerMin + 2, midMin + 1, innerMin + 1 }) {
                WorldGen.PlaceTile(origin.X + lx, origin.Y + DoorTopY, TileID.Torches,
                    mute: true, forced: false, plr: -1, style: TorchID.Blue);
            }

            //外舱补给桶（Containers style 5 = 木桶）
            int supplyChest = WorldGen.PlaceChest(origin.X + outerMin + 3, origin.Y + furnitureY,
                TileID.Containers, notNearOtherChests: false, style: 5);
            if (supplyChest >= 0) {
                FillSupplyChest(Main.chest[supplyChest]);
            }

            //中舱模具加工台（4x3，Origin 在 (1,2)，锚满宽地板）
            int tableType = ModContent.TileType<MoldProcessingTableTile>();
            bool tablePlaced = WorldGen.PlaceObject(origin.X + midMin + 4, origin.Y + furnitureY, tableType, mute: true);

            //中舱旧网接入终端：坠舱既是 SHPC 的家也是深潜口——碎片从这里出发也从这里铭刻
            int accessX = origin.X + midMin + 8;
            int accessY = origin.Y + furnitureY;
            Tile accessSlot = Framing.GetTileSafely(accessX, accessY);
            if (!accessSlot.HasTile) {
                accessSlot.HasTile = true;
                accessSlot.TileType = (ushort)ModContent.TileType<Scenarios.OldNet.Tiles.OldNetAccessTerminalTile>();
                accessSlot.TileFrameX = 0;
                accessSlot.TileFrameY = 0;
            }
            else {
                CWRMod.Instance.Logger.Warn("[SHPCCradle] OldNet access terminal slot occupied; skipped.");
            }

            //内舱 SHPC 箱（Containers style 13 = 天域箱，见原版 SkywareChest 物品的 placeStyle）
            int shpcChest = WorldGen.PlaceChest(origin.X + innerMin + 2, origin.Y + furnitureY,
                TileID.Containers, notNearOtherChests: false, style: 13);

            //SHPC 落位：内舱箱 → 补给桶兜底 → 全失败记错误
            int shpcHome = shpcChest >= 0 ? shpcChest : supplyChest;
            if (shpcHome >= 0) {
                AddChestItem(Main.chest[shpcHome], SHPCOverride.ID, 1);
                CWRMod.Instance.Logger.Info("Shoving SHPC into the cradle chest.");
                //加工台物块放置失败时把物品塞进同一个箱子，产线不断档
                if (!tablePlaced) {
                    AddChestItem(Main.chest[shpcHome], ModContent.ItemType<MoldProcessingTable>(), 1);
                    CWRMod.Instance.Logger.Warn("[SHPCCradle] table tile placement failed; item added to chest instead.");
                }
            }
            else {
                CWRMod.Instance.Logger.Error("[SHPCCradle] both chests failed to place; SHPC unobtainable from cradle!");
            }
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

        /// <summary>按距舱尾的偏移区间求房间的左右界（自动适配入口朝向）</summary>
        private static (int min, int max) RoomSpan(int tailX, int entryDir, int fromOffset, int toOffset) {
            int a = tailX - entryDir * fromOffset;
            int b = tailX - entryDir * toOffset;
            return (Math.Min(a, b), Math.Max(a, b));
        }

        private static void PlaceSolid(int x, int y, ushort type) {
            Tile tile = Framing.GetTileSafely(x, y);
            tile.HasTile = true;
            tile.TileType = type;
            tile.Slope = SlopeType.Solid;
            tile.IsHalfBlock = false;
            tile.LiquidAmount = 0;
        }

        private static void AddChestItem(Chest chest, int itemType, int stack) {
            if (chest == null || itemType <= 0 || stack <= 0) {
                return;
            }
            for (int i = 0; i < chest.item.Length; i++) {
                Item item = chest.item[i];
                if (item != null && item.type != ItemID.None) {
                    continue;
                }
                chest.item[i] = new Item(itemType, stack);
                return;
            }
        }
    }
}
