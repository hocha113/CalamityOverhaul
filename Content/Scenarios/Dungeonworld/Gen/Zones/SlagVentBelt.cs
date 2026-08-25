using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Layers.L6;
using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Rooms;
using CalamityOverhaul.Content.Scenarios.Dungeonworld.Machines;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.Utilities;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Zones
{
    //====================================================================
    //L6 渣汽疏泄带(Slag-Steam Vent Belt,WAVE2-ENVIRONMENTS §6)。
    //
    //铸造层末两折地板下的"下水道":排渣口旁的检修落口下去是一串渣池窖——
    //静液岩浆在堰坎渣池里发橙光(地带照明主体,零灯具),池沿凝狱石渣壳(触烫,
    //装备答题),头顶/池沿的疏泄口按错相节拍轮流喷火柱(DungeonworldZoneVents 驱动),
    //干沿+平台桥=构造性公平的"读节拍踩间隙"热舞台。
    //
    //液体纪律:岩浆直写 255,不跑 settle——构造密封+液体流动停摆(F16/F17)
    //=写完即静定(WaterGate 运行时同款论据);代之以地带级断言:扫窖箱矩形,
    //岩浆格必须全部落在登记渣池矩形内,越界 fail loud(镜像 AssertBandWater)。
    //
    //狱石经济:总量硬帽 40 格(跨编队裁决 §1-7"量压死"),每池 4~8 格,
    //噩梦镐档可采,回放制下"不可再生"指单次进入;协调者核价后可整体改纯装饰。
    //
    //派系:Tiled 95 原样继承(零换墙),烈焰轮/狱甲天然到位;
    //密度极点由 DungeonworldZoneNPC 的 pool[BlazingWheel] 补完。
    //====================================================================
    internal static class SlagVentBelt
    {
        private const int MinCaverns = 3;
        private const int MaxCaverns = 7;
        //窖内膛档:18~30 宽 x 8~12 高(计划 §6.4)
        private const int CavWidthMin = 18;
        private const int CavWidthMax = 30;
        private const int CavHeightMin = 8;
        private const int CavHeightMax = 12;
        //井侧干沿恒 5 列(含 3 宽井落点),远侧干沿 3~5 列
        private const int NearLedge = 5;
        //狱石全带硬帽(裁决 §1-7)
        private const int HellstoneCap = 40;
        //一窖一 x 槽,摊开成带不扎堆
        private const int SlotCols = 64;

        internal static void PlanAndBuild(LayerBuildContext ctx, UnifiedRandom rand) {
            LayerBand band = ctx.Band;
            int bottomLimit = band.SpineInteriorTop - 4;
            //末两折死带:带高后 27% 起(折高约 187~225,覆盖 6/7 折两档的末两折区)
            int rowLo = band.Top + (band.Bottom - band.Top) * 73 / 100;

            //锚点候选:末两折行窗内的既有房;主控(Exit)/奖库(Treasure)的地板不许穿
            var hosts = new List<RoomNode>();
            foreach (RoomNode room in ctx.Graph.Rooms) {
                if (room.Bounds.Bottom < rowLo - 220 || room.Bounds.Bottom > bottomLimit - 20
                    || room.Role == RoomRole.Exit || room.Role == RoomRole.Treasure
                    || room.InteriorRight - room.InteriorLeft < 10
                    || ZoneWorks.HoldsLiquid(room)) {
                    continue;
                }
                hosts.Add(room);
            }
            hosts.Sort(static (l, r) => {
                int byX = l.Bounds.Left.CompareTo(r.Bounds.Left);
                return byX != 0 ? byX : l.Bounds.Top.CompareTo(r.Bounds.Top);
            });

            var taken = new HashSet<int>();
            var caverns = new List<Cavern>();
            int hellstoneLeft = HellstoneCap;
            foreach (RoomNode host in hosts) {
                if (caverns.Count >= MaxCaverns) {
                    break;
                }
                Cavern cavern = TryBuildCavern(ctx, rand, host, rowLo, bottomLimit,
                    taken, caverns.Count, ref hellstoneLeft);
                if (cavern != null) {
                    caverns.Add(cavern);
                }
            }

            if (caverns.Count == 0) {
                CWRMod.Instance.Logger.Error(
                    "[SlagVentBelt] 末两折死带零渣池窖,渣汽疏泄带弃建,责任=锚点缺席或死带碎片化");
                return;
            }
            if (caverns.Count < MinCaverns) {
                CWRMod.Instance.Logger.Error(
                    $"[SlagVentBelt] 渣池窖仅{caverns.Count}<{MinCaverns},未成带(已建半成品保留,连通不受损)");
            }

            //连窖短道:相邻窖地板齐平且缝隙可留即通(可选环边,窖各有自井保底连通)
            int drifts = RouteDrifts(ctx, caverns);

            //淬火库:带尽头(最右窖)井侧干沿(M4 战利品表对位前的锁金箱占位,镜像副翼)
            Cavern last = caverns[^1];
            int chestX = last.ExtendRight ? last.IntL : last.IntR - 2;
            bool chestOk = WorldGen.PlaceChest(chestX, last.FloorTop - 1, TileID.Containers,
                notNearOtherChests: false, style: 2) >= 0;
            if (!chestOk) {
                CWRMod.Instance.Logger.Warn(
                    $"[SlagVentBelt] 淬火库箱放置失败 at ({chestX},{last.FloorTop - 1})");
            }

            //地带级岩浆断言:窖箱内液体必须全是岩浆且全在登记池内,越界 fail loud
            AssertLavaSealed(caverns);

            CWRMod.Instance.Logger.Info(
                $"[SlagVentBelt] 渣汽疏泄带落成 窖{caverns.Count} 短道{drifts}"
                + $" 喷口{DungeonworldZoneVents.Vents.Count} 狱石{HellstoneCap - hellstoneLeft}格(帽{HellstoneCap})"
                + $" 淬火库={(chestOk ? "成" : "拒")}");
        }

        //一窖的落成快照(短道路由/箱位/断言用)
        private sealed class Cavern
        {
            internal Rectangle Box;
            internal Rectangle Pool;
            internal int IntL;
            internal int IntR;
            internal int FloorTop;
            internal bool ExtendRight;
            internal int GraphIndex;
        }

        //==================== 单窖:锚点→贴身探空→预留→刻画→渣池→喷口 ====================

        private static Cavern TryBuildCavern(LayerBuildContext ctx, UnifiedRandom rand,
            RoomNode host, int rowLo, int bottomLimit, HashSet<int> taken,
            int builtCount, ref int hellstoneLeft) {

            int shaftX = ZoneWorks.PlanHostFloorGap(host, host.Bounds.Left + host.Bounds.Width / 2 - 1);
            if (shaftX < 0 || !taken.Add(shaftX / SlotCols)) {
                return null;
            }

            int cavW = rand.Next(CavWidthMin, CavWidthMax + 1);
            int cavH = rand.Next(CavHeightMin, CavHeightMax + 1);
            int depth = rand.Next(2, 4);
            int boxH = cavH + depth + 5;
            //窖体横位:交替左右延伸,井恒落在近侧干沿上
            bool extendRight = (builtCount & 1) == 0;
            int intL = extendRight ? shaftX - 2 : shaftX + 5 - cavW;
            int intR = intL + cavW;
            var box = new Rectangle(intL - 2, 0, cavW + 4, boxH);
            if (box.Left < DungeonworldMetrics.PlayLeft + 8
                || box.Right > DungeonworldMetrics.PlayRight - 8
                || HitsShaft(box.Left, box.Right)) {
                taken.Remove(shaftX / SlotCols);
                return null;
            }

            //贴身探空(镜像 IntersticePlanner.TryProbeGap):井柱正下的第一段空档
            int probeTop = host.Bounds.Bottom + DungeonworldMetrics.RoomPadding;
            if (probeTop >= bottomLimit) {
                taken.Remove(shaftX / SlotCols);
                return null;
            }
            List<(int top, int bottom)> gaps = ctx.Grid.FreeGaps(shaftX - 1, 5,
                probeTop, bottomLimit, boxH + 8);
            if (gaps.Count == 0 || gaps[0].top > probeTop + 4) {
                taken.Remove(shaftX / SlotCols);
                return null;
            }
            (int gapTop, int gapBottom) = gaps[0];
            //窖顶尽量压向末折之下:先取空档下沿,退到行窗下界之上
            int cavTop = System.Math.Min(gapBottom - boxH, bottomLimit - boxH);
            cavTop = System.Math.Max(cavTop, System.Math.Max(gapTop + 4, rowLo - 40));
            if (cavTop + boxH > gapBottom || cavTop < gapTop + 2) {
                taken.Remove(shaftX / SlotCols);
                return null;
            }
            box.Y = cavTop;
            if (!ctx.Grid.TryReserve(box, 0)) {
                taken.Remove(shaftX / SlotCols);
                return null;
            }
            ctx.Grid.MarkUnchecked(new Rectangle(shaftX - 1, probeTop, 5, cavTop + 2 - probeTop));

            //===刻画:落口→之字井→窖内膛===
            int intT = cavTop + 2;
            int floorTop = intT + cavH;
            ZoneWorks.OpenHostFloorGap(host, shaftX, L6Palette.PlatformFrameY, L6Palette.WallTiled);
            CorridorRouter.CarveStairWell(shaftX, host.Bounds.Bottom, intT,
                L6Palette.PlatformFrameY, L6Palette.WallTiled);
            TileBrush.CarveRect(intL, intT, intR, floorTop, L6Palette.WallTiled);

            //===渣池:池顶行留空作凹陷池唇,岩浆直写静定(不跑 settle)===
            int farEdge = rand.Next(3, 6);
            int pl = extendRight ? intL + NearLedge : intL + farEdge;
            int pr = extendRight ? intR - farEdge : intR - NearLedge;
            TileBrush.CarveRect(pl, floorTop, pr, floorTop + depth + 1, L6Palette.WallTiled);
            var pool = new Rectangle(pl, floorTop + 1, pr - pl, depth);
            FillLava(pool);

            //狱石渣壳:池沿表面行等价交换(触烫=装备答题),全带硬帽内逐池 4~8 格
            int rim = System.Math.Min(rand.Next(4, 9), hellstoneLeft);
            hellstoneLeft -= PlaceHellstoneRim(pl, pr, floorTop, rim);
            //余烬红壳缘:深红漆刷池唇两侧砖面(与 L6 基调深橙区分一档,待签字)
            for (int i = 1; i <= 2; i++) {
                WorldGen.paintTile(pl - 2 - i, floorTop, PaintID.DeepRedPaint);
                WorldGen.paintTile(pr + 1 + i, floorTop, PaintID.DeepRedPaint);
            }

            //平台桥:池上方 4 行,读节拍踩间隙的干路(构造性公平,池永不横贯全窖)
            TileBrush.PlatformRow(pl - 1, pr + 1, floorTop - 4, L6Palette.PlatformFrameY);

            //===疏泄口:远沿地板 1 口(向上),宽窖再加窖顶倒装 1 口(向下,喷桥面)===
            int vents = 0;
            int ventX = extendRight ? intR - 3 : intL + 1;
            if (TryPlaceGeyser(ventX, floorTop - 1)) {
                if (DungeonworldZoneVents.Register(new Point(ventX, floorTop - 1), down: false)) {
                    vents++;
                }
                L6Palette.ScorchDisk(ventX + 1, floorTop - 3, 4);
                L6Palette.OilStreakFloor(ventX + (extendRight ? -4 : 2), floorTop, 4);
            }
            if (cavW >= 24) {
                int cvx = (pl + pr) / 2 - 1;
                if (TryPlaceGeyser(cvx, intT)) {
                    if (DungeonworldZoneVents.Register(new Point(cvx, intT), down: true)) {
                        vents++;
                    }
                    L6Palette.ScorchDisk(cvx + 1, intT + 2, 3);
                }
            }
            if (vents == 0) {
                CWRMod.Instance.Logger.Warn(
                    $"[SlagVentBelt] 窖({box.X},{box.Y})零喷口(放置拒/硬帽满),降级=静态渣池窖");
            }

            //===入图===
            var node = new RoomNode { Bounds = box };
            int hostIdx = ZoneWorks.IndexOf(ctx.Graph, host);
            int nodeIdx = ctx.Graph.Rooms.Count;
            ctx.Graph.Rooms.Add(node);
            ctx.Graph.Edges.Add(new RoomEdge(hostIdx, nodeIdx,
                SocketKind.PlatformGap, EdgeForm.StairWell));
            ZoneRegistry.Register(ZoneKind.SlagVentBelt, box);

            return new Cavern {
                Box = box, Pool = pool, IntL = intL, IntR = intR,
                FloorTop = floorTop, ExtendRight = extendRight, GraphIndex = nodeIdx,
            };
        }

        //==================== 渣池小件 ====================

        //岩浆直写:镜像 FillState 的实心判据;液体流动停摆下写完即静定
        private static void FillLava(Rectangle pool) {
            for (int x = pool.Left; x < pool.Right; x++) {
                for (int y = pool.Top; y < pool.Bottom; y++) {
                    if (!WorldGen.InWorld(x, y, 5)) {
                        continue;
                    }
                    Tile t = Main.tile[x, y];
                    if (t.HasTile && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType]) {
                        continue;
                    }
                    t.LiquidAmount = byte.MaxValue;
                    t.LiquidType = LiquidID.Lava;
                }
            }
        }

        //池沿表面行蓝砖→狱石(等价交换保 slope);左右沿轮流放,返回实际格数
        private static int PlaceHellstoneRim(int pl, int pr, int floorTop, int budget) {
            int placed = 0;
            for (int i = 0; i < 4 && placed < budget; i++) {
                foreach (int x in new[] { pl - 1 - i, pr + i }) {
                    if (placed >= budget || !WorldGen.InWorld(x, floorTop, 5)) {
                        continue;
                    }
                    Tile t = Main.tile[x, floorTop];
                    if (t.HasTile && t.TileType == L6Palette.Brick && !Main.tile[x, floorTop - 1].HasTile) {
                        ZoneWorks.SwapSolidType(x, floorTop, TileID.Hellstone);
                        placed++;
                    }
                }
            }
            return placed;
        }

        //喷口砖 443(2x1,PlaceObject 自带上/下锚 alternate 遍历);以场上出现为准
        private static bool TryPlaceGeyser(int x, int y) {
            WorldGen.PlaceObject(x, y, TileID.GeyserTrap, mute: true);
            for (int i = 0; i < 2; i++) {
                Tile t = Main.tile[x + i, y];
                if (t.HasTile && t.TileType == TileID.GeyserTrap) {
                    return true;
                }
            }
            return false;
        }

        //==================== 短道 / 断言 ====================

        //相邻窖地板齐平且缝 ≤36 列即架净高 4 的连窖短道(环边;不齐平的靠各自井,不硬修)
        private static int RouteDrifts(LayerBuildContext ctx, List<Cavern> caverns) {
            int drifts = 0;
            for (int i = 0; i + 1 < caverns.Count; i++) {
                Cavern a = caverns[i];
                Cavern b = caverns[i + 1];
                if (a.FloorTop != b.FloorTop) {
                    continue;
                }
                int gapL = a.Box.Right;
                int gapR = b.Box.Left;
                if (gapR - gapL <= 0 || gapR - gapL > 36 || HitsShaft(gapL, gapR)) {
                    continue;
                }
                var strip = new Rectangle(gapL, a.FloorTop - 6, gapR - gapL, 8);
                if (!ctx.Grid.CanReserve(strip, 0)) {
                    continue;
                }
                ctx.Grid.MarkUnchecked(strip);
                CorridorRouter.CarveHorizontal(gapL, gapR, a.FloorTop, L6Palette.WallTiled);
                //把窖壳打通到短道(CarveHorizontal 只挖两箱之间,壳各 2 格自己开)
                TileBrush.CarveRect(a.IntR, a.FloorTop - 4, gapL, a.FloorTop, L6Palette.WallTiled);
                TileBrush.CarveRect(gapR, b.FloorTop - 4, b.IntL, b.FloorTop, L6Palette.WallTiled);
                ctx.Graph.Edges.Add(new RoomEdge(a.GraphIndex, b.GraphIndex,
                    SocketKind.Archway, EdgeForm.Horizontal));
                drifts++;
            }
            return drifts;
        }

        //静液岩浆越界断言(fail loud,镜像 AssertBandWater):窖箱内液体必须
        //全是岩浆且全落在登记池矩形内;箱外由 L4 全带水断言与 P80 兜底
        private static void AssertLavaSealed(List<Cavern> caverns) {
            int lava = 0, stray = 0, wrongType = 0;
            foreach (Cavern cavern in caverns) {
                for (int x = cavern.Box.Left; x < cavern.Box.Right; x++) {
                    for (int y = cavern.Box.Top; y < cavern.Box.Bottom; y++) {
                        Tile t = Main.tile[x, y];
                        if (t.LiquidAmount == 0) {
                            continue;
                        }
                        if (t.LiquidType != LiquidID.Lava) {
                            wrongType++;
                            continue;
                        }
                        lava++;
                        if (!cavern.Pool.Contains(x, y)) {
                            stray++;
                        }
                    }
                }
            }
            if (stray > 0 || wrongType > 0) {
                CWRMod.Instance.Logger.Error(
                    $"[SlagVentBelt] 岩浆断言失败:池外岩浆{stray}格/异种液体{wrongType}格,责任=渣池堰坎构造");
            }
            else {
                CWRMod.Instance.Logger.Info($"[SlagVentBelt] 岩浆断言通过 池内{lava}格零越界");
            }
        }

        private static bool HitsShaft(int left, int right)
            => left < DungeonworldMetrics.ShaftLeft + DungeonworldMetrics.ShaftWidth + 3
            && right > DungeonworldMetrics.ShaftLeft - 3;
    }
}
