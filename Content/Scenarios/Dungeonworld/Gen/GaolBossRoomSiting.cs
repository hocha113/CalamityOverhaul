using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.BossRooms;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen
{
    //深牢禁室选址(BOSS-DeepGaolWraith.md §3.3):牢狱层L2,每世界1间
    //
    //与脊走廊的覆盖关系裁决:**内联跨脊**而非避开——垂直落位取"内膛地板与L2脊
    //地板齐平",门槽(Archway 3深x4高,底沿即室内地板)恰与脊走廊底部4行重合。
    //pass顺序在MacroRoutePass之后,房间是后写方:足印内的脊通道被C的字符画整体
    //覆写,内膛不会被脊切坏;房间成为脊上的"内联关卡",左右门天然接脊贯通,
    //零新增几何,洪泛断言构造性通过。副作用是脊在此处收窄为4高门洞——
    //≥F1底线3,且正是Archway语义;演出上"禁室钉在必经之路上"优于旁挂彩蛋房。
    //(避开方案=整房抬到脊上方,需额外接头走廊+楼梯,弃)
    //
    //时序(Wave-1定论):PickOrigin由P30 LayerPlanPass在规划期调用定点,
    //足印+padding随即预留进L2占用栅格(层内容房间构造性避开);
    //P45 GaolBossRoomPass只消费LastOrigin盖章,不再自行选址
    //Wave-2追加:选址前扣除触及L2的隔离带楼梯井禁带(井位P20已定先于本定点,
    //R4顺序=先竖直后逐层,禁室是避让方),禁带口径见VerticalLinks.ExcludeZones
    internal static class GaolBossRoomSiting
    {
        //本次生成的落位,P45盖章与ValidatePass报告消费;ShouldSave=false回放制下每次生成重算
        internal static Point? LastOrigin;

        //水平安全区间:距主竖井≥30格、房间足印避开出生列±10、离可达区边缘≥4
        private const int ShaftKeepAway = 30;
        private const int SpawnKeepAway = 10;

        internal static Point? PickOrigin() {
            LastOrigin = null;
            LayerBand l2 = DungeonworldMetrics.Bands[1];

            //内膛地板行rel=门槽顶+门高(底沿齐平契约),对齐L2脊地板行
            int floorRel = GaolBossRoom.LeftDoorOffset.Y + GaolBossRoom.DoorHeight;
            int originY = l2.SpineFloorTop - floorRel;

            //放不下=硬错误跳过,不静默偏移出带(层带表若改动这里要响)
            if (originY < l2.Top + 2 || originY + GaolBossRoom.Height > l2.Bottom) {
                CWRMod.Instance.Logger.Error(
                    $"[Dungeonworld] 深牢禁室垂直放不进L2带[{l2.Top},{l2.Bottom}),originY={originY},本次跳过");
                return null;
            }

            //左区间:出生列左侧;右区间:主竖井右侧(闭区间,均为originX候选);
            //Wave-2:先扣除触及L2的隔离带楼梯井禁带(井0整柱下行+井1脊口穿透,
            //R4顺序=先竖直后逐层,避让方向=禁室避井);genRand先选侧再取点(决定论F22),
            //随机消耗恒2次(NextBool+Next)与Wave-1一致
            int leftMin = DungeonworldMetrics.PlayLeft + 4;
            int leftMax = DungeonworldMetrics.SpawnX - SpawnKeepAway - GaolBossRoom.Width;
            int rightMin = DungeonworldMetrics.ShaftLeft + DungeonworldMetrics.ShaftWidth + ShaftKeepAway;
            int rightMax = DungeonworldMetrics.PlayRight - 4 - GaolBossRoom.Width;
            var leftSegs = new List<(int min, int max)> { (leftMin, leftMax) };
            var rightSegs = new List<(int min, int max)> { (rightMin, rightMax) };
            VerticalLinks.ExcludeZones(1, GaolBossRoom.Width, leftSegs);
            VerticalLinks.ExcludeZones(1, GaolBossRoom.Width, rightSegs);

            //左区间右缘到竖井左缘的距离由SpawnKeepAway+SpawnX/ShaftLeft差保证≥30,静态成立
            bool pickLeft = WorldGen.genRand.NextBool();
            List<(int min, int max)> side = pickLeft ? leftSegs : rightSegs;
            if (VerticalLinks.SegLength(side) <= 0) {
                //每口井禁带折算约70候选列,扣不空900列级区间;真到这步=常量被改坏
                CWRMod.Instance.Logger.Warn("[Dungeonworld] 深牢禁室所选侧被井位禁带扣空,换侧落位");
                side = pickLeft ? rightSegs : leftSegs;
            }
            int originX = VerticalLinks.PickFromSegments(side);
            if (originX < 0) {
                CWRMod.Instance.Logger.Error(
                    "[Dungeonworld] 深牢禁室两侧均无合法落位,本次跳过,责任=常量表/井位禁带");
                return null;
            }

            LastOrigin = new Point(originX, originY);
            CWRMod.Instance.Logger.Info(
                $"[Dungeonworld] 深牢禁室落位 origin=({originX},{originY})"
                + $" 门槽行={originY + GaolBossRoom.LeftDoorOffset.Y}..{originY + floorRel - 1} 脊地板={l2.SpineFloorTop}");
            return LastOrigin;
        }
    }
}
