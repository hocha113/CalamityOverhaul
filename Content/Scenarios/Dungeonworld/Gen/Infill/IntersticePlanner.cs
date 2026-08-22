using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Rooms;
using System.Collections.Generic;
using Terraria;
using Terraria.Utilities;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Infill
{
    //====================================================================
    //夹层带规划器(P52消费)。
    //
    //===要解决的问题===
    //L3用21层甲板消化1348行,节距52~68,挂房几乎吃满整个节距;而L4/L5/L6用的是
    //同样的房高、3~4倍的节距(L4 pitch=193、L5地层间160~260、L6折间187~224),
    //于是每个节距里有六到八成的行数是从没被任何pass碰过的实心砖。
    //本规划器不改这三层任何既有节奏,只把节距之间那段死岩接管过来。
    //
    //===连通性不变量(本体系最大风险)===
    //每个夹层簇必须从锚点生长,绝不自由落位。锚点=主内容已落成的房(ctx.Graph.Rooms),
    //顺序恒为:选锚点→探空→预留→开地板口→检修井下探→落服务廊→挂房→全部注册进图。
    //任何一步预留失败即整簇止步(已建成的部分保留,它们仍挂在锚点上),
    //不硬写、不留断头井，断头井会被P80洪泛覆盖率立刻抓到。
    //
    //===足印纪律===
    //一切落位先过ctx.Grid,主内容的足印(含RoomPadding外扩)天然把本器挡在外面,
    //不硬编码任何避让坐标。随机全走WorldGen.genRand(F22);本pass的消耗整段排在
    //P50七层之后,既有种子的L1~L7布局逐格不变(R4)。
    //====================================================================
    internal static class IntersticePlanner
    {
        //一个夹层需要的最小死带高度:装不下一层就不动它
        private const int MinGapRows = 70;
        //单层夹层的行高档;上界压在L3迷宫节距(68)附近,读法才是"同一座楼里的夹层"
        private const int TierRowsMin = 54;
        private const int TierRowsMax = 72;
        //死带顶部留白:紧贴主房开井会把两层黏成一层,留一段岩才有"下沉"的读法
        private const int GapTopMargin = 12;
        private const int MaxTiers = 4;
        //服务廊预留带:天花缓冲1+内膛4+地板2+落口缓冲2=9行,[cf-6,cf+3)(镜像L3甲板廊)
        private const int CorridorBandTop = 6;
        private const int CorridorBandRows = 9;
        //挂房地板=服务廊地板上收10行(L3 RoomHang同值,楼梯井落口刚好一跳程)
        private const int RoomHang = 10;
        private const int CorridorSpanMin = 34;
        //上限不给全幅:一条横贯的廊就是第二套甲板,而本体系要的是"后勤面的碎片"。
        //220配合下面的簇间距,每条死带大约能被四五段廊断续覆盖掉一半列
        private const int CorridorSpanMax = 220;
        private const int GrowStep = 6;
        //宿主门槛:太窄的房开不了3宽落口
        private const int HostMinInteriorWidth = 12;
        //簇间距按(x槽,死带)二维记,不是只按x：只按x的话每个x槽只出一簇,
        //五条死带里会有四条一簇都摊不到,纵向照旧空
        private const int HostSeparation = 64;
        private const int GapBucketRows = 64;
        private const int MaxClustersPerBand = 28;

        internal struct Report
        {
            internal int Clusters;
            internal int Tiers;
            internal int Corridors;
            internal int Utilities;
            internal int Rubbles;
            internal int Shafts;
            internal int HostsRejected;
            internal int FurnPlaced;
            internal int FurnRejected;

            public override readonly string ToString()
                => $"簇{Clusters} 层{Tiers} 廊{Corridors} 功能间{Utilities} 废墟{Rubbles}"
                + $" 井{Shafts} 宿主拒{HostsRejected} 家具{FurnPlaced}成/{FurnRejected}拒";
        }

        /// <summary>层带主入口:选宿主→逐簇下探→逐层落廊挂房。返回计数供GenReport比对</summary>
        internal static Report Build(LayerBuildContext ctx, InfillSkin skin, UnifiedRandom rand) {
            var report = new Report();
            LayerBand band = ctx.Band;
            int xLeft = DungeonworldMetrics.PlayLeft + 6;
            int xRight = DungeonworldMetrics.PlayRight - 6;
            //脊预留带上沿:P30已把脊到带底整条标死,这里再钳一次防越界刻画
            int bottomLimit = band.SpineInteriorTop - 4;

            //宿主快照:只认主内容已落成的房,本器自己新增的节点不再当宿主
            //(否则夹层会顺着自己无限往下长,深度失控且与下一地层撞车)
            var hosts = new List<RoomNode>(ctx.Graph.Rooms);
            hosts.Sort(static (l, r) => {
                int byX = l.Bounds.Left.CompareTo(r.Bounds.Left);
                return byX != 0 ? byX : l.Bounds.Top.CompareTo(r.Bounds.Top);
            });

            //已占的(x槽,死带)对;同一格里只许一簇,两个轴都摊得开
            var taken = new HashSet<(int xSlot, int gapSlot)>();
            foreach (RoomNode host in hosts) {
                if (report.Clusters >= MaxClustersPerBand) {
                    break;
                }
                if (host.Bounds.Right - host.Bounds.Left
                    - DungeonworldMetrics.RoomShellThick * 2 < HostMinInteriorWidth) {
                    continue;
                }
                if (HoldsLiquid(host)) {
                    //在淹着的房底开落口=把那舱水挂在平台上。子世界液体流动停摆(F16/F17)
                    //不会真漏,但观感就是一舱悬空的水;况且L4的水位两态版图是预计算的,
                    //事后给它多一个洞不在账里。淹房一律不当宿主
                    continue;
                }
                int shaftX = host.Bounds.Left + DungeonworldMetrics.RoomShellThick;
                if (!TryProbeGap(ctx, shaftX, host, xLeft, xRight, bottomLimit,
                    out int gapTop, out int gapBottom)) {
                    report.HostsRejected++;
                    continue;
                }
                var slot = (shaftX / HostSeparation, gapTop / GapBucketRows);
                if (!taken.Add(slot)) {
                    continue;
                }
                if (!GrowCluster(ctx, skin, rand, host, shaftX, gapTop, gapBottom,
                    xLeft, xRight, ref report)) {
                    report.HostsRejected++;
                }
            }
            return report;
        }

        //房内是否存了水:贴地板那一行采样即可,水面再低也压在这行上
        private static bool HoldsLiquid(RoomNode room) {
            int y = room.FloorTop - 1;
            for (int x = room.InteriorLeft; x < room.InteriorRight; x++) {
                if (WorldGen.InWorld(x, y, 5) && Main.tile[x, y].LiquidAmount > 0) {
                    return true;
                }
            }
            return false;
        }

        //===探空:宿主足印下方(含padding)起的第一段连续空闲竖档===
        private static bool TryProbeGap(LayerBuildContext ctx, int shaftX, RoomNode host,
            int xLeft, int xRight, int bottomLimit, out int gapTop, out int gapBottom) {
            gapTop = gapBottom = 0;
            if (shaftX - 1 < xLeft || shaftX + DungeonworldMetrics.StairWellWidth + 1 > xRight) {
                return false;
            }
            int probeTop = host.Bounds.Bottom + DungeonworldMetrics.RoomPadding;
            if (probeTop >= bottomLimit) {
                return false;
            }
            List<(int top, int bottom)> gaps = ctx.Grid.FreeGaps(
                shaftX - 1, DungeonworldMetrics.StairWellWidth + 2,
                probeTop, bottomLimit, MinGapRows);
            //第一段空档不紧贴宿主=下方另有内容,这个宿主不面向死带,不许穿别人的房下去
            if (gaps.Count == 0 || gaps[0].top > probeTop + 4) {
                return false;
            }
            (gapTop, gapBottom) = gaps[0];
            return gapBottom - gapTop - GapTopMargin >= TierRowsMin;
        }

        //===单簇:自宿主地板向下,逐层"井→廊→挂房"===
        private static bool GrowCluster(LayerBuildContext ctx, InfillSkin skin, UnifiedRandom rand,
            RoomNode host, int shaftX, int gapTop, int gapBottom,
            int xLeft, int xRight, ref Report report) {
            int tiers = System.Math.Min(MaxTiers, (gapBottom - gapTop - GapTopMargin) / TierRowsMin);
            int prevFloor = host.Bounds.Bottom;      //上一级的井刻画起点行
            RoomNode prevNode = host;
            int cursor = gapTop + GapTopMargin;
            bool any = false;

            for (int t = 0; t < tiers; t++) {
                int tierRows = rand.Next(TierRowsMin, TierRowsMax + 1);
                int corridorFloor = cursor + tierRows;
                if (corridorFloor + CorridorBandRows - CorridorBandTop > gapBottom) {
                    break;
                }

                //井段足印:起点要避开上一级自己的占用,否则第一格就判占用、一簇也建不成
                //宿主是RoomPadding外扩带(Bottom+2),上一级服务廊是它的9行预留带(Floor+3)。
                //刻画起点仍取prevFloor,那两三行本来就在上一级的账里
                int shaftTop = t == 0
                    ? host.Bounds.Bottom + DungeonworldMetrics.RoomPadding
                    : prevFloor + CorridorBandRows - CorridorBandTop;
                int shaftBottom = corridorFloor - CorridorBandTop;
                var shaftRect = new Rectangle(shaftX - 1, shaftTop,
                    DungeonworldMetrics.StairWellWidth + 2, shaftBottom - shaftTop);
                if (shaftRect.Height <= 0 || !ctx.Grid.CanReserve(shaftRect, 0)) {
                    break;
                }

                //服务廊横向生长:自井列向两侧探,撞上已有足印即停
                if (!GrowSpan(ctx.Grid, rand, shaftX, corridorFloor, xLeft, xRight,
                    out int spanL, out int spanR)) {
                    break;
                }

                //两处足印一起落账,再动tile(先规划后刻画,§1.5-1)
                ctx.Grid.MarkUnchecked(shaftRect);
                ctx.Grid.MarkUnchecked(new Rectangle(spanL, corridorFloor - CorridorBandTop,
                    spanR - spanL, CorridorBandRows));

                //刻画:先廊后井,井体末端直落进廊内膛
                InfillRooms.Tally tally = InfillRooms.ServiceCorridor(
                    spanL, spanR, corridorFloor, skin, rand);
                report.FurnPlaced += tally.Placed;
                report.FurnRejected += tally.Rejected;

                if (t == 0) {
                    InfillRooms.MaintenanceShaft(host, DungeonworldMetrics.RoomShellThick,
                        corridorFloor, skin);
                }
                else {
                    InfillRooms.BareShaft(shaftX, prevFloor, corridorFloor, skin);
                }
                report.Shafts++;

                //服务廊入图:名义节点,让P80的nodes计数与洪泛断言都认得它
                var corridor = new RoomNode {
                    Bounds = new Rectangle(spanL, corridorFloor - InfillRooms.CorridorClearance
                        - DungeonworldMetrics.RoomShellThick, spanR - spanL,
                        InfillRooms.CorridorClearance + DungeonworldMetrics.RoomShellThick * 2),
                };
                int corridorIndex = ctx.Graph.Rooms.Count;
                ctx.Graph.Rooms.Add(corridor);
                ctx.Graph.Edges.Add(new RoomEdge(IndexOf(ctx.Graph, prevNode), corridorIndex,
                    SocketKind.PlatformGap, EdgeForm.StairWell));
                report.Corridors++;
                report.Tiers++;
                any = true;

                //挂房:廊上方1~3间,各自开落口直落本廊
                int hungFloor = corridorFloor - RoomHang;
                int roomTop = cursor + 2;
                int maxH = hungFloor - roomTop - DungeonworldMetrics.RoomShellThick;
                HangRooms(ctx, skin, rand, corridorIndex, spanL, spanR, hungFloor, maxH,
                    corridorFloor, ref report);

                prevFloor = corridorFloor;
                prevNode = corridor;
                cursor = corridorFloor + CorridorBandRows - CorridorBandTop;
            }

            if (any) {
                report.Clusters++;
            }
            return any;
        }

        //===服务廊横向生长:自井列向两侧各探半程,撞足印即停===
        private static bool GrowSpan(OccupancyGrid grid, UnifiedRandom rand, int shaftX,
            int corridorFloor, int xLeft, int xRight, out int spanL, out int spanR) {
            int top = corridorFloor - CorridorBandTop;
            spanL = shaftX - 2;
            spanR = shaftX + DungeonworldMetrics.StairWellWidth + 2;
            if (spanL < xLeft || spanR > xRight
                || !grid.CanReserve(new Rectangle(spanL, top, spanR - spanL, CorridorBandRows), 0)) {
                return false;
            }

            int half = rand.Next(CorridorSpanMin, CorridorSpanMax + 1) / 2;
            while (shaftX - spanL < half && spanL - GrowStep >= xLeft
                && grid.CanReserve(new Rectangle(spanL - GrowStep, top, GrowStep, CorridorBandRows), 0)) {
                spanL -= GrowStep;
            }
            while (spanR - shaftX < half && spanR + GrowStep <= xRight
                && grid.CanReserve(new Rectangle(spanR, top, GrowStep, CorridorBandRows), 0)) {
                spanR += GrowStep;
            }
            return spanR - spanL >= CorridorSpanMin;
        }

        //===挂房:功能间为主,废墟按掷骰混入;每间必开落口,不留孤房===
        private static void HangRooms(LayerBuildContext ctx, InfillSkin skin, UnifiedRandom rand,
            int corridorIndex, int spanL, int spanR, int hungFloor, int maxH,
            int corridorFloor, ref Report report) {
            if (maxH < InfillRooms.RoomMinClearance) {
                return;
            }
            //房数按廊长给:短廊一间,长廊最多三间
            int want = System.Math.Clamp((spanR - spanL) / 42, 1, 3);
            for (int i = 0; i < want; i++) {
                bool rubble = maxH >= 8 && rand.NextBool(3);
                Point size = rubble ? InfillRooms.RubbleInteriorSize(rand)
                    : InfillRooms.UtilityInteriorSize(rand);
                size.Y = System.Math.Min(size.Y, maxH);
                if (size.Y < InfillRooms.RoomMinClearance) {
                    continue;
                }
                RoomNode room = RoomPlacer.TryPlace(ctx.Grid, rand, spanL + 2, spanR - 2,
                    hungFloor, size, size, retries: 8);
                if (room == null) {
                    continue;
                }

                InfillRooms.Tally tally = rubble
                    ? InfillRooms.BuildRubble(room, skin, rand)
                    : InfillRooms.BuildUtility(room, skin, rand);
                report.FurnPlaced += tally.Placed;
                report.FurnRejected += tally.Rejected;

                int roomIndex = ctx.Graph.Rooms.Count;
                ctx.Graph.Rooms.Add(room);
                InfillRooms.MaintenanceShaft(room, DungeonworldMetrics.RoomShellThick,
                    corridorFloor, skin);
                ctx.Graph.Edges.Add(new RoomEdge(roomIndex, corridorIndex,
                    SocketKind.PlatformGap, EdgeForm.StairWell));
                report.Shafts++;
                if (rubble) {
                    report.Rubbles++;
                }
                else {
                    report.Utilities++;
                }
            }
        }

        private static int IndexOf(RoomGraph graph, RoomNode node) {
            for (int i = graph.Rooms.Count - 1; i >= 0; i--) {
                if (ReferenceEquals(graph.Rooms[i], node)) {
                    return i;
                }
            }
            return 0;
        }
    }
}
