using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.BossRooms
{
    /// <summary>
    /// 验收堂：铸造监工的专属 Boss 房（L6 铸造机关层，横长扁厅，导轨戏要横向行程）。
    /// 镜像 FloodGalleryRoom 全套纪律（计数拼接 prefab + 行长断言 fail loud、
    /// 家具 PlaceObject 拒绝记日志、末尾 RangeFrame、Place() 落成即向看守注册）。
    /// 几何即战斗语言：房顶一条贯通导轨（rel 6 墙面贴带）是 Boss 的一维舞台；
    /// 四条镖口横巷（rel 14/15/24/25 双侧墙面口）是镖阵的固定发射源；
    /// 左右 1/6 位两座检修位（触发板+壁龛）是对冲活塞反杀的赌桌；
    /// 断轨点（中列锈痕）是 P3 钟摆相变的生成期伏笔。三条渣槽给地面分段节奏。
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

        /// <summary>断轨点（P3 钟摆锚）：中列，生成期即刷锈痕伏笔</summary>
        internal const int BreakCol = 38;

        /// <summary>点检台：台阶 rel29 cols 9..11，触发区 3×3（rows 26..28）</summary>
        internal static readonly Point DaisOffset = new(10, 27);

        /// <summary>检修位（对冲活塞反杀）：左右 1/6 位，触发板中心列</summary>
        internal const int BayLeftCol = 15;
        internal const int BayRightCol = 62;

        /// <summary>镖口横巷四行（车床式镖阵的固定发射行；空窗巷道逐轮异色声明）</summary>
        internal static readonly int[] DartLaneRows = [14, 15, 24, 25];

        /// <summary>蛰伏吊臂停靠位（右端挂轨）</summary>
        internal static readonly Point RigHomeOffset = new(68, 9);

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

        /// <summary>断轨锚世界像素（P3 钟摆悬点）</summary>
        internal static Vector2 BreakAnchorWorld(Point origin)
            => new((origin.X + BreakCol + 1) * 16f, (origin.Y + RailRel) * 16f + 8f);

        /// <summary>地板顶世界 Y（压锤行程终点）</summary>
        internal static float FloorWorldY(Point origin) => (origin.Y + FloorRel) * 16f;

        /// <summary>镖巷行世界 Y（laneIndex 0..3）与左右发射口 X</summary>
        internal static float DartLaneWorldY(Point origin, int lane)
            => (origin.Y + DartLaneRows[Math.Clamp(lane, 0, DartLaneRows.Length - 1)]) * 16f + 8f;
        internal static float DartPortWorldX(Point origin, bool left)
            => (origin.X + (left ? InteriorLeft + 1 : InteriorRight - 1)) * 16f;

        //==================== 字符画（计数拼接；行长断言 fail loud）====================
        //# 实心蓝砖  . 空+蓝墙  , 空+板岩蓝墙(检修龛)  : 空+瓷面蓝墙(顶拱)
        //r 空+瓷面墙+灰漆(导轨带)  p 空+瓷面墙+红漆(镖口)  D 门插槽

        private static readonly string[] Rows = BuildRows();

        /// <summary>
        /// 布局（rel 行）：0~2 壳顶 / 3~5 顶拱 / 6 导轨带 / 14~15 与 24~25 镖口横巷 /
        /// 26~29 门插槽+点检台+双检修龛 / 30 地板顶（22~25、37~40、52~55 三条渣槽）/
        /// 31~35 地板体与壳底。断轨点锈痕由 Place 后补漆。
        /// </summary>
        private static string[] BuildRows() {
            string solid = new('#', Width);
            string arch = "###" + new string(':', 72) + "###";
            string rail = "###" + new string('r', 72) + "###";
            string open = "###" + new string('.', 72) + "###";
            //镖口行：两侧各 2 格口位
            string ports = "###" + "pp" + new string('.', 68) + "pp" + "###";
            //门插槽行（rows 26..28）：点检区 9..11 空 + 检修龛 13..17 / 60..64 板岩背景
            string door = "DDD" + new string('.', 10) + new string(',', 5)
                + new string('.', 42) + new string(',', 5) + new string('.', 10) + "DDD";
            //末门行（row 29）：点检台阶 9..11
            string doorDais = "DDD" + new string('.', 6) + "###" + "." + new string(',', 5)
                + new string('.', 42) + new string(',', 5) + new string('.', 10) + "DDD";
            //地板顶：三条渣槽（1 深凹槽）22..25 / 37..40 / 52..55
            string floorTop = "###" + new string('#', 19) + new string('.', 4) + new string('#', 11)
                + new string('.', 4) + new string('#', 11) + new string('.', 4) + new string('#', 19) + "###";

            var rows = new string[Height];
            for (int i = 0; i < Height; i++) {
                rows[i] = i switch {
                    <= 2 => solid,
                    <= 5 => arch,
                    6 => rail,
                    (14 or 15 or 24 or 25) => ports,
                    <= 25 => open,
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

            //断轨点锈痕伏笔（导轨中段棕漆做旧，P3 从这里崩断）
            for (int dx = -2; dx <= 2; dx++) {
                int x = originX + BreakCol + dx;
                WorldGen.paintWall(x, originY + RailRel, PaintID.BrownPaint);
                WorldGen.paintWall(x, originY + RailRel - 1, PaintID.BrownPaint);
            }

            //装修遍：点检台与双检修位的触发板（纯装饰锚点，真正判定是站立/合取裁决；
            //板下无线，踩踏 HitSwitch 空转无副作用）
            int placed = 0, failed = 0;
            (int x, int y)[] plates = [
                (originX + DaisOffset.X, originY + 28),
                (originX + BayLeftCol, originY + FloorRel - 1),
                (originX + BayRightCol, originY + FloorRel - 1),
            ];
            foreach ((int x, int y) in plates) {
                WorldGen.PlaceTile(x, y, TileID.PressurePlates, mute: true, style: 2);
                if (Main.tile[x, y].HasTile && Main.tile[x, y].TileType == TileID.PressurePlates) {
                    placed++;
                }
                else {
                    failed++;
                    CWRMod.Instance.Logger.Warn($"[ProofingHallRoom] 触发板放置失败 at tile ({x},{y})");
                }
            }

            //自框收尾：直写区域全量帧修（生成期 P80 会再跑一遍，重复无害）
            WorldGen.RangeFrame(originX - 1, originY - 1, originX + Width + 1, originY + Height + 1);

            ProofingHallWatcher.RegisterRoom(new Point(originX, originY));
            //刷怪静默区（IMPL-D 接口，门禁自检；Boss 房内不刷普通敌怪）
            NPCs.Elites.DungeonworldEliteDirector.RegisterQuietZone(
                Bounds(new Point(originX, originY)), 12, "验收堂");
            CWRMod.Instance.Logger.Info(
                $"[ProofingHallRoom] 落成 origin=({originX},{originY}) 触发板 {placed} 成/{failed} 拒");
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
