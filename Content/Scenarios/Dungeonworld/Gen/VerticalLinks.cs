using System.Collections.Generic;
using System.Text;
using Terraria;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen
{
    //垂直连接清单·第二通道族(蓝图§1.4:每相邻层对≥2条贯穿通道,Wave-2补全):
    //主竖井(P20既有,几何连续贯穿)之外,每个隔离带开一口楼梯井式穿透
    //上层脊地板穿透下行(全宽平台桥防断路),之字平台语法与主竖井一致,
    //井底落在下层脊地板;水平取位genRand在安全区间取,与主竖井/出生列/
    //教堂对齐带互斥,相邻隔离带井位互错(防两口井同列串成一落到底)。
    //
    //分工三段(随机消耗顺序纪律R4:先竖直连接后逐层,全链路定序):
    //  1.P20 MacroRoutePass:PickAll()取位+井身刻画，全管线第一组genRand
    //    消耗点,每隔离带至多1次Next,先于P30禁室定点与P50层内容;
    //  2.P30 LayerPlanPass:ReserveInto()把足印预留进相邻两带ctx.Grid
    //    (零随机),层代理TryPlace构造性避开;
    //  3.P30 GaolBossRoomSiting:ExcludeZones()扣掉触井禁带后再选址(禁室避井)。
    //
    //L7→深渊带裁决【Wave-2定论】:不开贯穿口，深渊带底200行恒为地狱判定带
    //(F21)且蓝图§1.2明言不放可通行内容;"倒吊教堂悬在深渊上方"的演出语义由
    //视觉悬空(四周≥20格空隙)与L7层内容的垂钟龛(向下探入深渊上部,ROOMS-L7
    //§1-5)表达,均属层内容/演出波职责;垂直连接清单终止于L6→L7隔离带。
    internal static class VerticalLinks
    {
        //井净宽:D表ROOMS-INDEX §4次级通道井宽档5~8取下限,与主竖井同宽
        internal const int WellWidth = 5;
        //足印预留侧壁padding,镜像P30主竖井±2登记
        internal const int ReservePad = 2;
        //与主竖井错开的水平间距(禁室ShaftKeepAway=30先例上再放宽)
        private const int ShaftKeepAway = 40;
        //出生列两侧保护(井口不打在出生点脚下)
        private const int SpawnKeepAway = 10;
        //相邻隔离带井位互错:上下两口井同列=玩家一落到底,那是主竖井的职能
        private const int StaggerKeepAway = 12;

        //每隔离带一口井,索引i=连接Bands[i]与Bands[i+1];值=井左缘列,-1=未选定
        internal static readonly int[] WellLeft = new int[DungeonworldMetrics.Bands.Length - 1];

        //每对相邻层的取位半宽(以SpawnX为心),取两带活跃宽度档的较小半宽
        //(D表ROOMS-INDEX §5:L1±400/L2±500/L3~L5全幅取±800/L6±700/L7±250);
        //[0]仅作兜底,W1正常走L1井口房窗口(见PickAll)
        private static readonly int[] PairHalfWidth = [400, 500, 800, 800, 700, 250];

        static VerticalLinks() {
            System.Array.Fill(WellLeft, -1);
        }

        /// <summary>
        /// P20调用:为全部隔离带取井位。随机消耗=自上而下每口至多1次Next
        /// (段表为空则0次;空与否只由常量与先序井位决定,决定论F22不破)。
        /// </summary>
        internal static void PickAll() {
            //L1教堂群落对齐带:主教堂+扩建占x∈cathLeft+[-24,+160](L1Content布局表),
            //cathLeft由主竖井对齐推导为常量;全部井位统一避开本带，既防W1穿教堂
            //地板,也给镜像同一竖井对齐惯例的深层演出建筑(L7倒吊中殿)留同列净空
            int cathLeft = DungeonworldMetrics.ShaftLeft - Layers.L1.L1CathedralPrefab.ShaftArtLeft;
            int cathZoneMin = cathLeft - 24;
            int cathZoneMax = cathLeft + 160;

            for (int i = 0; i < WellLeft.Length; i++) {
                WellLeft[i] = -1;

                //基准区间(井左缘候选,闭区间):W1(L1→L2)钉在L1井口房Stairhead
                //窗口近旁(cathLeft+[300,348]起排、房宽≤20,L1Content布局表)
                //兑现Wave-1"口部预留"叙事:玩家自井口房落口下到脊,穿透井就在近旁;
                //其余隔离带以SpawnX为心按活跃宽度档取
                int baseMin, baseMax;
                if (i == 0) {
                    baseMin = cathLeft + 294;
                    baseMax = cathLeft + 370 - WellWidth;
                }
                else {
                    baseMin = DungeonworldMetrics.SpawnX - PairHalfWidth[i];
                    baseMax = DungeonworldMetrics.SpawnX + PairHalfWidth[i] - WellWidth;
                }
                var segs = new List<(int min, int max)> { (baseMin, baseMax) };

                //禁带逐个扣除(参数=被占用列的闭区间)
                SubtractOccupied(segs, DungeonworldMetrics.ShaftLeft - ShaftKeepAway,
                    DungeonworldMetrics.ShaftLeft + DungeonworldMetrics.ShaftWidth - 1 + ShaftKeepAway);
                SubtractOccupied(segs, DungeonworldMetrics.SpawnX - SpawnKeepAway,
                    DungeonworldMetrics.SpawnX + SpawnKeepAway);
                SubtractOccupied(segs, cathZoneMin, cathZoneMax);
                if (i > 0 && WellLeft[i - 1] >= 0) {
                    SubtractOccupied(segs, WellLeft[i - 1] - StaggerKeepAway,
                        WellLeft[i - 1] + WellWidth - 1 + StaggerKeepAway);
                }

                int pick = PickFromSegments(segs);
                if (pick < 0) {
                    //常量推演下不可达(最窄的W6扣完仍余约300候选列);
                    //真到这步=层带/常量被改坏,fail loud,该层对退回仅主竖井
                    CWRMod.Instance.Logger.Error(
                        $"[Dungeonworld] VerticalLinks 隔离带{i + 1}(L{i + 1}→L{i + 2})无合法井位,"
                        + "该层对退回仅主竖井,责任=常量表/活跃宽度档");
                }
                WellLeft[i] = pick;
            }
        }

        /// <summary>
        /// P30调用:井足印预留进相邻两带ctx.Grid(层代理构造性避开,零随机)。
        /// 上带=脊地板穿透条(本就在P30脊预留带内,双保险登记);下带=整柱(镜像主竖井±2)。
        /// </summary>
        internal static void ReserveInto() {
            for (int i = 0; i < WellLeft.Length; i++) {
                if (WellLeft[i] < 0) {
                    continue;
                }
                int left = WellLeft[i] - ReservePad;
                int width = WellWidth + ReservePad * 2;
                LayerBand upper = DungeonworldMetrics.Bands[i];
                LayerBand lower = DungeonworldMetrics.Bands[i + 1];
                LayerPlans.ByIndex(i)?.Grid.MarkUnchecked(new Rectangle(
                    left, upper.SpineInteriorTop - 1, width, upper.Bottom - (upper.SpineInteriorTop - 1)));
                LayerPlans.ByIndex(i + 1)?.Grid.MarkUnchecked(new Rectangle(
                    left, lower.Top, width, lower.Bottom - lower.Top));
            }
        }

        /// <summary>
        /// 把触及bandIndex带的井柱禁带从候选段表(某结构左缘,闭区间)中扣除。
        /// 触井规则:井(band-1)整柱穿过本带下行,井(band)在本带脊地板开口
        /// 两者都不许被跨脊足印(如禁室)吞掉封死。
        /// </summary>
        internal static void ExcludeZones(int bandIndex, int footprintWidth, List<(int min, int max)> segs) {
            ExcludeOne(bandIndex - 1);
            ExcludeOne(bandIndex);

            void ExcludeOne(int wellIndex) {
                if (wellIndex < 0 || wellIndex >= WellLeft.Length || WellLeft[wellIndex] < 0) {
                    return;
                }
                int zoneMin = WellLeft[wellIndex] - ReservePad;
                int zoneMax = WellLeft[wellIndex] + WellWidth - 1 + ReservePad;
                SubtractRange(segs, zoneMin - footprintWidth + 1, zoneMax);
            }
        }

        //===区间工具(闭区间语义,供本类与禁室选址共用)===

        /// <summary>候选左缘段表扣掉禁位[banMin,banMax](闭区间)</summary>
        internal static void SubtractRange(List<(int min, int max)> segs, int banMin, int banMax) {
            for (int k = segs.Count - 1; k >= 0; k--) {
                (int min, int max) = segs[k];
                if (banMax < min || banMin > max) {
                    continue;
                }
                segs.RemoveAt(k);
                if (banMax < max) {
                    segs.Insert(k, (banMax + 1, max));
                }
                if (banMin > min) {
                    segs.Insert(k, (min, banMin - 1));
                }
            }
        }

        /// <summary>段表总候选位计数</summary>
        internal static long SegLength(List<(int min, int max)> segs) {
            long total = 0;
            foreach ((int min, int max) in segs) {
                total += max - min + 1;
            }
            return total;
        }

        /// <summary>段表内均匀取一点(恰1次Next);空表返回-1且不消耗随机</summary>
        internal static int PickFromSegments(List<(int min, int max)> segs) {
            long total = SegLength(segs);
            if (total <= 0) {
                return -1;
            }
            int roll = WorldGen.genRand.Next((int)total);
            foreach ((int min, int max) in segs) {
                int len = max - min + 1;
                if (roll < len) {
                    return min + roll;
                }
                roll -= len;
            }
            return -1;
        }

        //宽度W的井体[s,s+W)与占用列[occMin,occMax](闭)相交的左缘s全部禁用
        private static void SubtractOccupied(List<(int min, int max)> segs, int occMin, int occMax)
            => SubtractRange(segs, occMin - WellWidth + 1, occMax);

        /// <summary>井位一行摘要,进P20日志与P80 GenReport(多种子回归比对项)</summary>
        internal static string Summary() {
            var sb = new StringBuilder();
            for (int i = 0; i < WellLeft.Length; i++) {
                if (sb.Length > 0) {
                    sb.Append(',');
                }
                sb.Append('L').Append(i + 1).Append(">L").Append(i + 2).Append('@').Append(WellLeft[i]);
            }
            return sb.ToString();
        }
    }
}
