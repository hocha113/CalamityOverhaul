using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Layers.L6;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.BossRooms
{
    /// <summary>
    /// 验收堂：铸造监工的专属 Boss 房（L6 铸造机关层，横长扁厅，导轨戏要横向行程）。
    /// 镜像 FloodGalleryRoom 全套纪律（计数拼接 prefab + 行长断言 fail loud、
    /// 家具 PlaceObject 拒绝记日志、末尾 RangeFrame、Place() 落成即向看守注册）。
    /// 几何即战斗语言：房顶一条贯通导轨（rel 6 墙面贴带）挂在一排齿轮吊架下，是 Boss 的一维舞台；
    /// 四条镖口横巷（rel 14/15/24/25 双侧墙面口）是镖阵的固定发射源；
    /// 左右 1/6 位两座检修位（吊架托座+触发板+壁龛+警示纹）是对冲活塞反杀的赌桌；
    /// 断轨点（中列裂纹托架+锈带）是 P3 钟摆相变的生成期伏笔；右端三宽托座是蛰伏吊臂的泊位。
    /// 三条渣槽（浇注坑）给地面分段节奏，坑底热漆坑缘焦黑，渣堆铸锭铺出「还在运转」的现场感。
    /// 全程不显式消耗 genRand（做旧走 hash，选址记账合同见 ProofingHallSiting）。
    /// </summary>
    internal static class ProofingHallRoom
    {
        //==================== 尺寸与语义槽（tile 坐标，相对 prefab 左上角）====================

        internal const int Width = 78;
        internal const int Height = 36;

        /// <summary>左右门插槽：Archway 3 深 × 4 高，底沿与室内地板齐平（战斗期齿轮闸门封死）</summary>
        internal static readonly Point LeftDoorOffset = new(0, 26);
        internal static readonly Point RightDoorOffset = new(75, 26);
        internal const int DoorHeight = 4;

        internal const int InteriorLeft = 3;
        internal const int InteriorRight = 74;
        internal const int InteriorTop = 3;
        internal const int FloorRel = 30;

        /// <summary>导轨墙面贴带行（Boss 毂心悬挂行=Rail+2）</summary>
        internal const int RailRel = 6;
        internal const int HubHangRel = 8;

        /// <summary>断轨点（P3 钟摆锚）：中列，生成期即刷裂纹托架+锈痕伏笔</summary>
        internal const int BreakCol = 38;

        /// <summary>点检台：台阶 rel29 cols 9..11，触发区 3×3（rows 26..28）</summary>
        internal static readonly Point DaisOffset = new(10, 27);

        /// <summary>检修位（对冲活塞反杀）：左右 1/6 位，触发板中心列</summary>
        internal const int BayLeftCol = 15;
        internal const int BayRightCol = 62;

        /// <summary>镖口横巷四行（车床式镖阵的固定发射行；空窗巷道逐轮异色声明）</summary>
        internal static readonly int[] DartLaneRows = [14, 15, 24, 25];

        /// <summary>蛰伏吊臂停靠位（右端挂轨，泊位托座 cols 67..69 正上方）</summary>
        internal static readonly Point RigHomeOffset = new(68, 9);

        /// <summary>三条渣槽（浇注坑）左沿列，槽宽 4、深 1（坑底=rel31 顶面）</summary>
        internal static readonly int[] GutterLeftCols = [22, 37, 52];
        internal const int GutterWidth = 4;

        internal static Rectangle Bounds(Point origin) => new(origin.X, origin.Y, Width, Height);

        /// <summary>吊臂停靠位世界像素</summary>
        internal static Vector2 RigWorldPos(Point origin)
            => new((origin.X + RigHomeOffset.X) * 16f + 8f, (origin.Y + RigHomeOffset.Y) * 16f + 8f);

        /// <summary>点检台触发区世界矩形（3×3 格）</summary>
        internal static Rectangle DaisZoneWorld(Point origin)
            => new((origin.X + DaisOffset.X - 1) * 16, (origin.Y + DaisOffset.Y - 1) * 16, 48, 48);

        /// <summary>检修位触发区世界矩形（板前 3×3 格）</summary>
        internal static Rectangle BayZoneWorld(Point origin, bool left) {
            int col = left ? BayLeftCol : BayRightCol;
            return new Rectangle((origin.X + col - 1) * 16, (origin.Y + FloorRel - 3) * 16, 48, 48);
        }

        /// <summary>毂心悬挂行世界 Y（Boss 一维舞台）</summary>
        internal static float HubWorldY(Point origin) => (origin.Y + HubHangRel) * 16f + 8f;

        /// <summary>导轨带世界 Y（贴带行中心；断轨锚同高）</summary>
        internal static float RailWorldY(Point origin) => (origin.Y + RailRel) * 16f + 8f;

        /// <summary>断轨锚世界像素（P3 钟摆悬点）</summary>
        internal static Vector2 BreakAnchorWorld(Point origin)
            => new((origin.X + BreakCol + 1) * 16f, (origin.Y + RailRel) * 16f + 8f);

        /// <summary>地板顶世界 Y（压锤行程终点）</summary>
        internal static float FloorWorldY(Point origin) => (origin.Y + FloorRel) * 16f;

        /// <summary>渣槽熔池面中心世界像素（i=0..2；池面取坑格半深）</summary>
        internal static Vector2 GutterPoolCenterWorld(Point origin, int i)
            => new((origin.X + GutterLeftCols[i] + GutterWidth * 0.5f) * 16f,
                   (origin.Y + FloorRel) * 16f + 10f);

        /// <summary>渣槽坑格世界矩形（i=0..2；宽 4 格 × 坑深 1 格）</summary>
        internal static Rectangle GutterWorldRect(Point origin, int i)
            => new((origin.X + GutterLeftCols[i]) * 16, (origin.Y + FloorRel) * 16, GutterWidth * 16, 16);

        /// <summary>镖巷行世界 Y（laneIndex 0..3）与左右发射口 X</summary>
        internal static float DartLaneWorldY(Point origin, int lane)
            => (origin.Y + DartLaneRows[Math.Clamp(lane, 0, DartLaneRows.Length - 1)]) * 16f + 8f;
        internal static float DartPortWorldX(Point origin, bool left)
            => (origin.X + (left ? InteriorLeft + 1 : InteriorRight - 1)) * 16f;

        //==================== 字符画（计数拼接；行长断言 fail loud）====================
        //# 实心蓝砖  . 空+蓝墙  , 空+板岩蓝墙(检修龛)  : 空+瓷面蓝墙(顶拱)
        //r 空+瓷面墙+灰漆(导轨带)  p 空+瓷面墙+红漆(镖口)  D 门插槽
        //H 实心齿轮块(吊架/托座,层染刷亮橙)  k 实心裂纹砖(断轨点失效托架)  C 实心齿轮块+板岩墙(基座/过梁)

        //顶拱吊架布点(rows 3..5 同列贯通,底沿贴住 rel6 导轨带,读作"轨挂在架上"):
        //单列齿=8+9i 八根,对齐 A3 轨灯画点(FoundryOverseer.DrawGlowLayers 的 8+i*9,灯装在架上);
        //三宽托座=14..16 与 61..63(检修位活塞承座)、67..69(吊臂泊位);断轨点 38..39 裂纹砖=正在失效的托架
        private static readonly int[] HangerTeethCols = [8, 17, 26, 35, 44, 53, 62, 71];
        private static readonly int[] BracketLeftCols = [14, 61, 67];
        private const int BracketWidth = 3;

        //黄铜灯笼吊点(顶拱空档,列距均匀照亮全厅;炉光为主灯"低"档)
        private static readonly int[] LanternCols = [11, 27, 42, 57];

        //静态字段按文本顺序初始化:BuildRows 读上面两张布点表,Rows 必须声明在它们之后,
        //否则表还是 null,首次触及本类即 TypeInitializationException
        private static readonly string[] Rows = BuildRows();

        /// <summary>
        /// 布局（rel 行）：0~2 壳顶 / 3~5 顶拱+吊架托座 / 6 导轨带 / 14~15 与 24~25 镖口横巷 /
        /// 25 门顶齿轮过梁 / 26~29 门插槽+点检台+双检修龛 / 30 地板顶（22~25、37~40、52~55 三条渣槽，
        /// 检修位触发板下垫齿轮基座）/ 31~35 地板体与壳底。做旧与照明由 Place 后补。
        /// </summary>
        private static string[] BuildRows() {
            string solid = new('#', Width);

            //顶拱行(3..5):瓷面墙底 + 吊架齿/托座/断轨裂纹托架
            char[] archCells = ("###" + new string(':', 72) + "###").ToCharArray();
            foreach (int col in HangerTeethCols) {
                archCells[col] = 'H';
            }
            foreach (int left in BracketLeftCols) {
                for (int i = 0; i < BracketWidth; i++) {
                    archCells[left + i] = 'H';
                }
            }
            archCells[BreakCol] = 'k';
            archCells[BreakCol + 1] = 'k';
            string arch = new(archCells);

            string rail = "###" + new string('r', 72) + "###";
            string open = "###" + new string('.', 72) + "###";
            //镖口行：两侧各 2 格口位
            string ports = "###" + "pp" + new string('.', 68) + "pp" + "###";
            //末条镖口行(row 25)兼门顶过梁：门洞正上方三格换齿轮块(闸门的机匣读法)
            string portsLintel = "CCC" + "pp" + new string('.', 68) + "pp" + "CCC";
            //门插槽行（rows 26..28）：点检区 9..11 空 + 检修龛 13..17 / 60..64 板岩背景
            string door = "DDD" + new string('.', 10) + new string(',', 5)
                + new string('.', 42) + new string(',', 5) + new string('.', 10) + "DDD";
            //末门行（row 29）：点检台阶 9..11（中格齿轮=触发板基座）
            string doorDais = "DDD" + new string('.', 6) + "#C#" + "." + new string(',', 5)
                + new string('.', 42) + new string(',', 5) + new string('.', 10) + "DDD";
            //地板顶：三条渣槽（1 深凹槽）22..25 / 37..40 / 52..55；检修位触发板列垫齿轮基座
            char[] floorCells = ("###" + new string('#', 19) + new string('.', 4) + new string('#', 11)
                + new string('.', 4) + new string('#', 11) + new string('.', 4) + new string('#', 19) + "###")
                .ToCharArray();
            floorCells[BayLeftCol] = 'C';
            floorCells[BayRightCol] = 'C';
            string floorTop = new(floorCells);

            var rows = new string[Height];
            for (int i = 0; i < Height; i++) {
                rows[i] = i switch {
                    <= 2 => solid,
                    <= 5 => arch,
                    6 => rail,
                    (14 or 15 or 24) => ports,
                    25 => portsLintel,
                    <= 23 => open,
                    <= 28 => door,
                    29 => doorDais,
                    30 => floorTop,
                    _ => solid,
                };
            }
            return rows;
        }

        //==================== 材质常量 ====================

        private const ushort Brick = TileID.BlueDungeonBrick;
        private const ushort WallBase = WallID.BlueDungeonUnsafe;
        private const ushort WallSlab = WallID.BlueDungeonSlabUnsafe;
        private const ushort WallTile = WallID.BlueDungeonTileUnsafe;

        //==================== 构建 ====================

        /// <summary>
        /// 在 origin（tile 左上角）落一间验收堂并登记到运行时看守与刷怪静默区。
        /// 生成期与运行期（测试钥匙）通用；运行期联机的区块同步由调用方负责。
        /// </summary>
        internal static void Place(int originX, int originY) {
            ValidatePrefab();

            for (int ry = 0; ry < Height; ry++) {
                string row = Rows[ry];
                for (int rx = 0; rx < Width; rx++) {
                    int x = originX + rx;
                    int y = originY + ry;
                    if (!WorldGen.InWorld(x, y, 5)) {
                        continue;
                    }
                    switch (row[rx]) {
                        case '#':
                            SetSolid(x, y, Brick, WallSlab);
                            break;
                        case 'H':
                            //吊架齿/托座：齿轮块（层染统一刷亮橙，机件从蓝钢底里跳出来）
                            SetSolid(x, y, L6Palette.CogBlock, WallTile);
                            break;
                        case 'k':
                            //断轨点失效托架：裂纹砖（P3 从这里崩断的生成期伏笔）
                            SetSolid(x, y, L6Palette.CrackedBrick, WallTile);
                            break;
                        case 'C':
                            //基座/过梁：齿轮块，板岩墙衬底
                            SetSolid(x, y, L6Palette.CogBlock, WallSlab);
                            break;
                        case '.':
                            ClearCell(x, y, WallBase);
                            break;
                        case ',':
                            ClearCell(x, y, WallSlab);
                            break;
                        case ':':
                            ClearCell(x, y, WallTile);
                            break;
                        case 'r':
                            //导轨带：瓷面墙 + 灰漆横带（Boss 的一维舞台）
                            ClearCell(x, y, WallTile);
                            WorldGen.paintWall(x, y, PaintID.GrayPaint);
                            break;
                        case 'p':
                            //镖口：红漆口位（镖永远从口出，不凭空）
                            ClearCell(x, y, WallTile);
                            WorldGen.paintWall(x, y, PaintID.RedPaint);
                            break;
                        case 'D':
                            ClearCell(x, y, WallBase);
                            break;
                        default:
                            CWRMod.Instance.Logger.Warn(
                                $"[ProofingHallRoom] 未知图例字符 '{row[rx]}' at ({rx},{ry})");
                            break;
                    }
                }
            }

            PaintSignatures(originX, originY);
            int placed = PlaceFurniture(originX, originY, out int failed);

            //全层基调层染：锈橙洗未上漆格 + 齿轮机件刷亮橙（hash 布点，零 genRand，钥匙重放同形）
            (LayerTint.TintReport wash, int cogs) =
                L6Palette.RustWash(Bounds(new Point(originX, originY)));

            //自框收尾：直写区域全量帧修（生成期 P80 会再跑一遍，重复无害）
            WorldGen.RangeFrame(originX - 1, originY - 1, originX + Width + 1, originY + Height + 1);

            ProofingHallWatcher.RegisterRoom(new Point(originX, originY));
            ProofingHallScene.NoteRoom(new Point(originX, originY));
            //刷怪静默区（IMPL-D 接口，门禁自检；Boss 房内不刷普通敌怪）
            NPCs.Elites.DungeonworldEliteDirector.RegisterQuietZone(
                Bounds(new Point(originX, originY)), 12, "验收堂");
            CWRMod.Instance.Logger.Info(
                $"[ProofingHallRoom] 落成 origin=({originX},{originY}) 家具 {placed} 成/{failed} 拒 " +
                $"层染{wash}/机件{cogs}");
        }

        //==================== 做旧签名遍（焦油族=焦痕+油渍；全 hash 布点，零 genRand）====================

        private static void PaintSignatures(int originX, int originY) {
            //断轨点做旧：裂纹托架刷棕锈(tile 面漆)、托架两翼导轨带覆棕锈(轨在这里先锈后断)
            for (int ry = InteriorTop; ry <= RailRel - 1; ry++) {
                WorldGen.paintTile(originX + BreakCol, originY + ry, PaintID.BrownPaint);
                WorldGen.paintTile(originX + BreakCol + 1, originY + ry, PaintID.BrownPaint);
            }
            for (int dx = -2; dx <= 2; dx++) {
                int x = originX + BreakCol + dx;
                if (!Main.tile[x, originY + RailRel - 1].HasTile) {
                    WorldGen.paintWall(x, originY + RailRel - 1, PaintID.BrownPaint);
                }
                WorldGen.paintWall(x, originY + RailRel, PaintID.BrownPaint);
            }
            //断轨托架下缘焦油垂滴（漏下来的润滑油烧焦了）
            L6Palette.TarDrip(originX + BreakCol - 1, originY + RailRel + 1, 2);
            L6Palette.TarDrip(originX + BreakCol + 2, originY + RailRel + 1, 3);

            //吊臂泊位下方油迹（蛰伏吊臂常年停这儿，滴了一地）
            L6Palette.TarDrip(originX + RigHomeOffset.X, originY + RigHomeOffset.Y + 1, 3);
            L6Palette.TarDrip(originX + RigHomeOffset.X - 1, originY + RigHomeOffset.Y + 1, 2);

            //渣槽（浇注坑）：坑底热漆(铁水映底)、坑背墙与坑缘砖焦黑、坑上方墙面焦痕放射斑
            foreach (int left in GutterLeftCols) {
                for (int i = 0; i < GutterWidth; i++) {
                    int x = originX + left + i;
                    WorldGen.paintTile(x, originY + FloorRel + 1, L6Palette.HotPaint);
                    WorldGen.paintWall(x, originY + FloorRel, L6Palette.TarPaint);
                }
                WorldGen.paintTile(originX + left - 1, originY + FloorRel, L6Palette.TarPaint);
                WorldGen.paintTile(originX + left + GutterWidth, originY + FloorRel, L6Palette.TarPaint);
                HashScorch(originX + left + 2, originY + FloorRel - 2, 4);
            }

            //检修龛警示纹：龛背墙焦黑/锈橙逐列相间（这是赌桌，站进来自担风险）
            foreach (int bayLeft in new[] { BayLeftCol - 2, BayRightCol - 2 }) {
                for (int i = 0; i < 5; i++) {
                    int x = originX + bayLeft + i;
                    byte paint = (i & 1) == 0 ? L6Palette.TarPaint : L6Palette.RustPaint;
                    for (int ry = 26; ry <= 29; ry++) {
                        if (!Main.tile[x, originY + ry].HasTile) {
                            WorldGen.paintWall(x, originY + ry, paint);
                        }
                    }
                }
            }

            //点检台：背墙灰漆净面 + 台阶两翼灰面（全厅唯一干净角落=人的工位）
            for (int i = 0; i < 3; i++) {
                int x = originX + 9 + i;
                for (int ry = 26; ry <= 28; ry++) {
                    WorldGen.paintWall(x, originY + ry, PaintID.GrayPaint);
                }
            }
            WorldGen.paintTile(originX + 9, originY + 29, PaintID.GrayPaint);
            WorldGen.paintTile(originX + 11, originY + 29, PaintID.GrayPaint);

            //门内油渍引导线：两门槛各一条拖到点检台方向（看得见的动线）
            L6Palette.OilStreakFloor(originX + InteriorLeft, originY + FloorRel, 6);
            L6Palette.OilStreakFloor(originX + InteriorRight, originY + FloorRel, 6, -1);
            //检修位进位短油渍（有人反复踩进去过）
            L6Palette.OilStreakFloor(originX + BayLeftCol + 3, originY + FloorRel, 4);
            L6Palette.OilStreakFloor(originX + BayRightCol - 3, originY + FloorRel, 4, -1);
        }

        /// <summary>
        /// 焦痕放射斑的确定性版本：镜像 L6Palette.ScorchDisk 的族约束，
        /// 圆缘灰烬过渡改 hash 掷点（Place 不许碰 genRand，钥匙重放必须同形）。
        /// </summary>
        private static void HashScorch(int cx, int cy, int r) {
            int r2 = r * r;
            for (int x = cx - r; x <= cx + r; x++) {
                for (int y = cy - r; y <= cy + r; y++) {
                    if (!WorldGen.InWorld(x, y, 5)) {
                        continue;
                    }
                    int d2 = (x - cx) * (x - cx) + (y - cy) * (y - cy);
                    if (d2 > r2) {
                        continue;
                    }
                    Tile tile = Main.tile[x, y];
                    if (tile.WallType != WallBase && tile.WallType != WallSlab && tile.WallType != WallTile) {
                        continue;
                    }
                    bool rim = d2 > (r - 1) * (r - 1);
                    tile.WallColor = rim && (Hash(x, y) & 1) == 0 ? L6Palette.AshPaint : L6Palette.TarPaint;
                }
            }
        }

        private static uint Hash(int x, int y) {
            uint h = (uint)(x * 374761393 + y * 668265263) ^ 0x9E3779B9u;
            h = (h ^ (h >> 13)) * 1274126177u;
            return h ^ (h >> 16);
        }

        //==================== 家具遍（放置以场上出现为准，拒绝记日志）====================

        private static int PlaceFurniture(int originX, int originY, out int failed) {
            int placed = 0;
            int failedCount = 0;
            void Tally(bool ok, string what, int x, int y) {
                if (ok) {
                    placed++;
                }
                else {
                    failedCount++;
                    CWRMod.Instance.Logger.Warn($"[ProofingHallRoom] {what} 放置失败 at tile ({x},{y})");
                }
            }

            //触发板×3（纯装饰锚点，真正判定是站立/合取裁决；板下无线，踩踏 HitSwitch 空转无副作用）
            (int x, int y)[] plates = [
                (originX + DaisOffset.X, originY + 28),
                (originX + BayLeftCol, originY + FloorRel - 1),
                (originX + BayRightCol, originY + FloorRel - 1),
            ];
            foreach ((int x, int y) in plates) {
                WorldGen.PlaceTile(x, y, TileID.PressurePlates, mute: true, style: 2);
                Tally(Main.tile[x, y].HasTile && Main.tile[x, y].TileType == TileID.PressurePlates,
                    "触发板", x, y);
            }

            //黄铜灯笼×4（顶拱吊点；炉光为主灯"低"档，只保证通行读图）
            foreach (int col in LanternCols) {
                Tally(L6Palette.TryPlaceObject(originX + col, originY + InteriorTop,
                    TileID.HangingLanterns, L6Palette.LanternBrassStyle), "黄铜灯笼", originX + col, originY + InteriorTop);
            }

            //点检台蜡烛（人的工位的一点人味）
            Tally(L6Palette.TryPlaceTile(originX + DaisOffset.X + 1, originY + 28,
                TileID.Candles, L6Palette.CandleStyle), "蜡烛", originX + DaisOffset.X + 1, originY + 28);

            //渣堆：渣槽两翼半砖灰烬堆 + 内缘整砖渣墩（浇注溢出来的凝渣）
            (int x, bool half)[] slag = [
                (21, true), (26, false), (36, true), (41, true), (51, false), (56, true),
            ];
            foreach ((int col, bool half) in slag) {
                int x = originX + col;
                int y = originY + FloorRel - 1;
                if (Main.tile[x, y].HasTile) {
                    failedCount++;
                    CWRMod.Instance.Logger.Warn($"[ProofingHallRoom] 渣堆落点被占 at tile ({x},{y})");
                    continue;
                }
                SetSolid(x, y, Brick, Main.tile[x, y].WallType);
                Tile slagTile = Main.tile[x, y];
                slagTile.IsHalfBlock = half;
                WorldGen.paintTile(x, y, L6Palette.AshPaint);
                placed++;
            }

            //铸锭堆：吊臂泊位下方的待验工件（验收机器的"在制品"）
            Tally(L6Palette.TryPlaceTile(originX + 65, originY + FloorRel - 1,
                TileID.MetalBars, L6Palette.BarIronStyle), "铁锭堆", originX + 65, originY + FloorRel - 1);
            Tally(L6Palette.TryPlaceTile(originX + 66, originY + FloorRel - 1,
                TileID.MetalBars, L6Palette.BarLeadStyle), "铅锭堆", originX + 66, originY + FloorRel - 1);

            //告示牌×2：点检台侧（开工引导）+ 左检修位外侧（反杀教学）
            Tally(L6Palette.PlaceSignWithText(originX + 6, originY + FloorRel - 1,
                "点检台。站上去等班铃数完，这台机器就开工验收。闸门会落锁，验完自开。"),
                "点检台告示", originX + 6, originY + FloorRel - 1);
            Tally(L6Palette.PlaceSignWithText(originX + BayLeftCol + 4, originY + FloorRel - 1,
                "检修位。毂心过顶时踩板，对冲活塞替你砸一次。位上方的灯还亮几盏，就还剩几次。"),
                "检修位告示", originX + BayLeftCol + 4, originY + FloorRel - 1);

            failed = failedCount;
            return placed;
        }

        //==================== 断轨疤痕事务（看守调用；服务器写 + 看守负责回播）====================

        /// <summary>断轨疤痕涉及的 tile 矩形（回播 SendTileSquare 用）</summary>
        internal static Rectangle RailScarRect(Point origin)
            => new(origin.X + BreakCol - 3, origin.Y + InteriorTop, 8, 11);

        /// <summary>
        /// P3 断轨落锤后的现场：断轨带焦黑、断口下方拉出焦油垂痕（疤痕列 36/38/40；
        /// 生成期旧滴在 37 列与 40 列，40 列与疤痕共列，复位时统一清后按生成期长度回补）。
        /// 只写漆层，不动物块，失败无副作用。
        /// </summary>
        internal static void PaintRailScar(Point origin) {
            for (int dx = -2; dx <= 2; dx++) {
                int x = origin.X + BreakCol + dx;
                if (!Main.tile[x, origin.Y + RailRel - 1].HasTile) {
                    WorldGen.paintWall(x, origin.Y + RailRel - 1, L6Palette.TarPaint);
                }
                WorldGen.paintWall(x, origin.Y + RailRel, L6Palette.TarPaint);
            }
            foreach (int col in new[] { BreakCol - 2, BreakCol, BreakCol + 2 }) {
                L6Palette.TarDrip(origin.X + col, origin.Y + RailRel + 1, 6);
            }
        }

        /// <summary>吊臂复位时恢复断轨点到生成期状态（棕锈带回位、疤痕垂滴清除）</summary>
        internal static void PaintRailRestore(Point origin) {
            for (int dx = -2; dx <= 2; dx++) {
                int x = origin.X + BreakCol + dx;
                if (!Main.tile[x, origin.Y + RailRel - 1].HasTile) {
                    WorldGen.paintWall(x, origin.Y + RailRel - 1, PaintID.BrownPaint);
                }
                WorldGen.paintWall(x, origin.Y + RailRel, PaintID.BrownPaint);
            }
            foreach (int col in new[] { BreakCol - 2, BreakCol, BreakCol + 2 }) {
                for (int i = 0; i < 6; i++) {
                    int y = origin.Y + RailRel + 1 + i;
                    Tile tile = Main.tile[origin.X + col, y];
                    if (tile.HasTile) {
                        break;
                    }
                    tile.WallColor = 0;
                }
            }
            //生成期断口两翼旧滴（-1/+2 列）回补，保证复位后与初落成同形
            L6Palette.TarDrip(origin.X + BreakCol - 1, origin.Y + RailRel + 1, 2);
            L6Palette.TarDrip(origin.X + BreakCol + 2, origin.Y + RailRel + 1, 3);
        }

        //==================== 受约束写入（镜像 FloodGalleryRoom）====================

        private static void SetSolid(int x, int y, ushort type, ushort wall) {
            Tile tile = Main.tile[x, y];
            tile.HasTile = true;
            tile.TileType = type;
            tile.Slope = SlopeType.Solid;
            tile.IsHalfBlock = false;
            tile.WallType = wall;
            tile.LiquidAmount = 0;
        }

        private static void ClearCell(int x, int y, ushort wall) {
            Tile tile = Main.tile[x, y];
            tile.HasTile = false;
            tile.WallType = wall;
            tile.LiquidAmount = 0;
        }

        //==================== 校验（构造性保证优先，失败即硬错误）====================

        private static bool validated;

        private static void ValidatePrefab() {
            if (validated) {
                return;
            }
            if (Rows.Length != Height) {
                throw new InvalidOperationException(
                    $"[ProofingHallRoom] prefab 行数 {Rows.Length} != Height {Height}");
            }
            for (int i = 0; i < Rows.Length; i++) {
                if (Rows[i].Length != Width) {
                    throw new InvalidOperationException(
                        $"[ProofingHallRoom] prefab 第 {i} 行长 {Rows[i].Length} != Width {Width}");
                }
            }
            validated = true;
        }
    }
}
