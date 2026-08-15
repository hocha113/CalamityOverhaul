using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Rooms;
using Terraria;
using Terraria.ID;
using Terraria.Utilities;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Infill
{
    //====================================================================
    //填充体系的四族构件,夹层带(P52)与封存副翼(P54)共用。
    //
    //设计意图:这四族是"连接组织",不是与主房并列的内容——刻意做小做短,
    //玩家的读法应当是"主结构之间还塞着后勤面",而不是"又一层房间"。
    //所以尺寸上限压在主房之下,装修密度也只给主房的一半。
    //
    //几何纪律:外壳不刻画。P10 已把全世界浇成本带砖,房间落位又有 RoomPadding
    //保证四周≥2格未被预留,所以只挖内膛、只刷墙,壳自然就是本带砖——
    //这既省一半写入,也天然满足§2.5的"侧墙≥2厚"。
    //家具一律 WorldGen.PlaceObject/PlaceTile,拒绝即计失败不强写帧(§3.2-1)。
    //====================================================================
    internal static class InfillRooms
    {
        internal struct Tally
        {
            internal int Placed;
            internal int Rejected;

            internal void Add(bool ok, string what, int x, int y) {
                if (ok) {
                    Placed++;
                }
                else {
                    Rejected++;
                    CWRMod.Instance.Logger.Warn($"[InfillRooms] {what}放置失败 at ({x},{y})");
                }
            }
        }

        //净空底线:走廊4(§2.5标准档),房间5(留一行给家具头顶)
        internal const int CorridorClearance = DungeonworldMetrics.CorridorClearance;
        internal const int RoomMinClearance = 5;
        //废墟堆的最大高度:抬2格封顶,保证堆上仍有≥3净空可过(F1)
        private const int RubbleMaxRise = 2;

        //==================== ① 服务廊 ====================

        /// <summary>
        /// 服务廊:净高4的后勤走道,挖内膛+刷墙+沿顶挂灯。
        /// 段长由调用方按空闲量生长,本函数只管刻画与装修。
        /// </summary>
        internal static Tally ServiceCorridor(int left, int right, int floorTop,
            InfillSkin skin, UnifiedRandom rand) {
            var tally = new Tally();
            int top = floorTop - CorridorClearance;
            TileBrush.CarveRect(left, top, right, floorTop, skin.WallBase);
            PatchWalls(left, right, top, floorTop, skin);

            //地面旧损:成段而非逐格(§3.2-6禁逐格噪声)。
            //认领层(L5/L6)换裂砖,其余层只上做旧漆——裂砖在踩得到的面上是真陷阱(F31),
            //在禁用层铺一段就等于偷偷给那层加了一处坠落机关
            for (int x = left + 4; x < right - 4; x += rand.Next(18, 31)) {
                int len = rand.Next(3, 7);
                for (int i = 0; i < len && x + i < right - 2; i++) {
                    if (skin.AllowCrackedFloor) {
                        TileBrush.SetSolid(x + i, floorTop, skin.CrackedBrick);
                    }
                    else {
                        WorldGen.paintTile(x + i, floorTop, skin.AgePaint);
                    }
                }
            }

            //照明:每10~16列一盏,挂灯族缺席的层(L1)退回落地烛台
            for (int x = left + 4; x < right - 3; x += rand.Next(10, 17)) {
                bool ok = skin.LanternStyle >= 0
                    ? TryPlaceObject(x, top, TileID.HangingLanterns, skin.LanternStyle)
                    : TryPlaceTile(x, floorTop - 1, TileID.Candelabras, skin.CandelabraStyle);
                tally.Add(ok, "服务廊灯", x, top);
            }
            ApplyTint(left, top, right, floorTop, skin);
            return tally;
        }

        //==================== ② 检修井 ====================

        /// <summary>
        /// 检修井:把上位房的地板开口直落到下方走廊,井口盖平台防误落。
        /// 连通性靠它兑现,所以井体一定要在调用方确认空闲之后才刻画。
        /// </summary>
        internal static void MaintenanceShaft(RoomNode from, int dropOffset, int targetFloorTop,
            InfillSkin skin) {
            var gap = new DoorSocket(SocketSide.Bottom, dropOffset,
                SocketKind.PlatformGap, DungeonworldMetrics.StairWellWidth);
            from.Sockets.Add(gap);
            CorridorRouter.RouteToFloorBelow(from, gap, targetFloorTop,
                skin.PlatformFrameY, skin.WallBase);
        }

        /// <summary>无宿主房的裸井段:上端接一条已挖走廊的地板,下端落到目标走廊</summary>
        internal static void BareShaft(int left, int floorTopUpper, int floorTopLower, InfillSkin skin) {
            CorridorRouter.CarveStairWell(left, floorTopUpper, floorTopLower,
                skin.PlatformFrameY, skin.WallBase);
            TileBrush.PlatformRow(left, left + DungeonworldMetrics.StairWellWidth,
                floorTopUpper, skin.PlatformFrameY);
        }

        //==================== ③ 小功能间 ====================

        /// <summary>净空档:8~14宽 x 6~9高(压在主房之下,读法是"储藏/值班"而非厅室)</summary>
        internal static Point UtilityInteriorSize(UnifiedRandom rand)
            => new(rand.Next(8, 15), rand.Next(6, 10));

        /// <summary>
        /// 小功能间:一件工作面 + 一盏光 + 一两件杂物。最廉价的体积填充,
        /// 所以坚决不放大件家具——大件是主房的语汇。
        /// </summary>
        internal static Tally BuildUtility(RoomNode room, InfillSkin skin, UnifiedRandom rand) {
            var tally = new Tally();
            CarveInterior(room, skin);
            RoomShell.Dress(room, skin.Brick);

            int floor = room.FloorTop;
            int left = room.InteriorLeft;
            int right = room.InteriorRight;
            int mid = (left + right) / 2;

            //工作面三选一:检修台/桌椅/空架(空架=只落一盏灯,留白也是密度的一部分)
            switch (rand.Next(3)) {
                case 0:
                    tally.Add(TryPlaceTile(mid, floor - 1, TileID.WorkBenches, skin.WorkBenchStyle),
                        "检修台", mid, floor - 1);
                    tally.Add(TryPlaceTile(mid, floor - 2, TileID.Candles, skin.CandleStyle),
                        "台面蜡烛", mid, floor - 2);
                    break;
                case 1:
                    tally.Add(TryPlaceTile(mid, floor - 1, TileID.Tables, skin.TableStyle),
                        "桌", mid, floor - 1);
                    tally.Add(TryPlaceTile(mid - 2, floor - 1, TileID.Chairs, skin.ChairStyle),
                        "椅", mid - 2, floor - 1);
                    break;
                default:
                    tally.Add(TryPlaceTile(left + 1, floor - 1, TileID.Candelabras, skin.CandelabraStyle),
                        "烛台", left + 1, floor - 1);
                    break;
            }

            //顶灯一盏
            if (skin.LanternStyle >= 0) {
                tally.Add(TryPlaceObject(mid + 2, room.InteriorTop, TileID.HangingLanterns, skin.LanternStyle),
                    "顶灯", mid + 2, room.InteriorTop);
            }

            //杂物:罐0~2件,靠墙落
            int pots = rand.Next(3);
            for (int i = 0; i < pots; i++) {
                int px = i == 0 ? right - 2 : left + 1 + i;
                WorldGen.PlacePot(px, floor - 1, TileID.Pots,
                    rand.Next(skin.PotStyleMin, skin.PotStyleMax));
            }
            //五间里约一间藏个桶(M4战利品表对位前的占位)
            if (rand.NextBool(5)) {
                tally.Add(WorldGen.PlaceChest(right - 3, floor - 1, TileID.Containers,
                    notNearOtherChests: false, skin.ChestStyle) >= 0, "储物桶", right - 3, floor - 1);
            }
            ApplyTint(left, room.InteriorTop, right, floor, skin);
            return tally;
        }

        //==================== ④ 塌方废墟 ====================

        /// <summary>净空档:14~26宽 x 8~13高(比功能间大一档,塌方要看得出体量)</summary>
        internal static Point RubbleInteriorSize(UnifiedRandom rand)
            => new(rand.Next(14, 27), rand.Next(8, 14));

        /// <summary>
        /// 塌方废墟:本层自己 archetype 的半塌版本。地面堆两级碎砖、顶角塌一块,
        /// 抬高一律≤2格并且不连成通长墙,保证堆上仍有≥3净空(F1),不会做出走不过去的房。
        /// </summary>
        internal static Tally BuildRubble(RoomNode room, InfillSkin skin, UnifiedRandom rand) {
            var tally = new Tally();
            CarveInterior(room, skin);

            int floor = room.FloorTop;
            int left = room.InteriorLeft;
            int right = room.InteriorRight;
            int top = room.InteriorTop;
            int height = floor - top;

            //顶角塌陷:一侧顶角填回三角碎砖,轮廓按阶梯收分(§3.2-6只许阶梯式)。
            //天花位踩不到,裂砖在这里只是纹理,各层通用。
            //深度按净高封顶:塌进来的三角必须给玩家包络(2宽3高)留够余量
            bool caveLeft = rand.NextBool();
            int caveW = rand.Next(3, 6);
            int caveMaxDepth = System.Math.Max(1, height - RubbleMaxRise - 3);
            for (int i = 0; i < caveW; i++) {
                int depth = System.Math.Min(caveW - i, caveMaxDepth);
                for (int k = 0; k < depth; k++) {
                    int cx = caveLeft ? left + i : right - 1 - i;
                    TileBrush.SetSolid(cx, top + k, skin.CrackedBrick);
                }
            }

            //地面碎砖堆:2~3处,每处宽3~7,梯形收边(不是逐格噪点)。
            //堆是玩家要踩上去的面,所以一律本层砖+做旧漆:裂砖踩碎会坠(F31),
            //堆用裂砖等于给每间废墟白送一个陷阱。
            //堆区避开塌陷那一侧——顶上塌下来、地上又堆起来,叠在同一列就是堵死
            int moundL = caveLeft ? left + caveW + 1 : left + 1;
            int moundR = caveLeft ? right - 1 : right - caveW - 1;
            int mounds = rand.Next(2, 4);
            for (int m = 0; m < mounds; m++) {
                int mw = rand.Next(3, 8);
                if (moundR - moundL <= mw + 1) {
                    break;
                }
                int mx = rand.Next(moundL, moundR - mw);
                int rise = rand.Next(1, RubbleMaxRise + 1);
                for (int i = 0; i < mw; i++) {
                    //两端各收一级,中段满高
                    int h = System.Math.Min(rise, System.Math.Min(i + 1, mw - i));
                    for (int k = 0; k < h; k++) {
                        TileBrush.SetSolid(mx + i, floor - 1 - k, skin.Brick);
                        WorldGen.paintTile(mx + i, floor - 1 - k, skin.AgePaint);
                    }
                }
            }

            //残留物:罐一两件(落在没堆砖的地上,PlacePot自带占位校验会挡掉重叠)
            for (int i = 0; i < 2; i++) {
                int px = rand.Next(left + 2, right - 2);
                WorldGen.PlacePot(px, floor - 1, TileID.Pots,
                    rand.Next(skin.PotStyleMin, skin.PotStyleMax));
            }
            if (skin.LanternStyle >= 0) {
                int lx = caveLeft ? right - 3 : left + 2;
                tally.Add(TryPlaceObject(lx, top, TileID.HangingLanterns, skin.LanternStyle),
                    "废墟残灯", lx, top);
            }
            //做旧签名加重:废墟是本体系里唯一允许铺满做旧的地方。
            //先做旧再层染,顺序不能倒(见 ApplyTint 注释)
            LayerTint.Wash(new Rectangle(left, top, right - left, floor - top),
                skin.AgePaint, 60, skin.PatchSalt ^ 0x77, skin.WallFamily, skin.BrickFamily);
            ApplyTint(left, top, right, floor, skin);
            return tally;
        }

        //==================== 共用原语 ====================

        /// <summary>只挖内膛+刷墙,外壳交给未被预留的原生砖(见文件头几何纪律)</summary>
        internal static void CarveInterior(RoomNode room, InfillSkin skin) {
            TileBrush.CarveRect(room.InteriorLeft, room.InteriorTop,
                room.InteriorRight, room.FloorTop, skin.WallBase);
            PatchWalls(room.InteriorLeft, room.InteriorRight, room.InteriorTop, room.FloorTop, skin);
        }

        /// <summary>
        /// 基调层染,每个填充区收尾时补一道。必须排在做旧之后:Wash只填未上漆的格,
        /// 谁先跑谁占位,反过来就把做旧痕迹挤没了(INDEX §3"层染只填空白格")。
        /// </summary>
        internal static void ApplyTint(int left, int top, int right, int bottom, InfillSkin skin) {
            if (skin.TintCoverage <= 0) {
                return;
            }
            LayerTint.Wash(new Rectangle(left, top, right - left, bottom - top),
                skin.TintPaint, skin.TintCoverage, skin.PatchSalt ^ 0x5A5A,
                skin.WallFamily, skin.BrickFamily);
        }

        /// <summary>墙变体成片混斑:块散列而非逐格掷骰,否则墙面是椒盐噪点(L4Palette.BandWalls同法)</summary>
        internal static void PatchWalls(int left, int right, int top, int bottom, InfillSkin skin) {
            for (int x = left; x < right; x++) {
                for (int y = top; y < bottom; y++) {
                    if (!WorldGen.InWorld(x, y, 5) || Main.tile[x, y].HasTile) {
                        continue;
                    }
                    Tile tile = Main.tile[x, y];
                    if (tile.WallType != skin.WallBase) {
                        continue;
                    }
                    if (LayerTint.BlockPatch(x, y, 30, skin.PatchSalt)) {
                        tile.WallType = skin.WallSlab;
                    }
                    else if (LayerTint.BlockPatch(x, y, 12, skin.PatchSalt ^ 0x3F1)) {
                        tile.WallType = skin.WallTiled;
                    }
                }
            }
        }

        internal static bool TryPlaceTile(int x, int y, int type, int style = 0) {
            WorldGen.PlaceTile(x, y, type, mute: true, forced: false, -1, style);
            return Main.tile[x, y].HasTile && Main.tile[x, y].TileType == type;
        }

        //挂件锚定放置:纵向试两格,以场上出现为准(L4Palette同法)
        internal static bool TryPlaceObject(int x, int y, int type, int style) {
            for (int dy = 0; dy <= 1; dy++) {
                WorldGen.PlaceObject(x, y + dy, type, mute: true, style: style);
                if (Main.tile[x, y + dy].HasTile && Main.tile[x, y + dy].TileType == type) {
                    return true;
                }
            }
            return false;
        }
    }
}
