using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.BossRooms
{
    //泄洪堂选址(WAVE2-MINIBOSS.md §3.9):水牢层L4,每世界1间,镜像GaolBossRoomSiting全套
    //
    //垂直=内联跨脊:内膛地板行(rel 42)对齐L4脊地板行,门槽(3深x4高,底沿即室内地板)
    //恰与脊走廊底部4行重合。房间是后写方(P45后盖章),足印内的脊通道被字符画整体覆写,
    //"泄洪堂钉在水牢最底的必经路上",零新增几何,洪泛断言构造性通过。
    //
    //时序:PickOrigin由P30 LayerPlanPass在禁室定点之后调用(R4账:定点消耗第3、4次),
    //足印+padding随即预留进L4占用栅格(L4Content组5落房与后续填充构造性避开);
    //P45后的FloodGalleryPass只消费LastOrigin盖章,不再自行选址。
    //选址前扣除触及L4的隔离带楼梯井禁带(井位P20已定,禁带口径见VerticalLinks.ExcludeZones)
    internal static class FloodGallerySiting
    {
        //本次生成的落位,盖章与看守消费;ShouldSave=false回放制下每次生成重算
        internal static Point? LastOrigin;

        //水平安全区间:距主竖井≥30格、房间足印避开出生列±10、离可达区边缘≥4(禁室同款)
        private const int ShaftKeepAway = 30;
        private const int SpawnKeepAway = 10;

        internal static Point? PickOrigin() {
            LastOrigin = null;
            LayerBand l4 = DungeonworldMetrics.Bands[3];

            //内膛地板行rel=门槽顶+门高(底沿齐平契约),对齐L4脊地板行
            int floorRel = FloodGalleryRoom.LeftDoorOffset.Y + FloodGalleryRoom.DoorHeight;
            int originY = l4.SpineFloorTop - floorRel;

            //放不下=硬错误跳过,不静默偏移出带(层带表若改动这里要响)。
            //R4:跳过也要把本函数的2次账掷满,否则退化种子上全链路随机流错位
            if (originY < l4.Top + 2 || originY + FloodGalleryRoom.Height > l4.Bottom) {
                BurnRolls(2);
                CWRMod.Instance.Logger.Error(
                    $"[Dungeonworld] 泄洪堂垂直放不进L4带[{l4.Top},{l4.Bottom}),originY={originY},本次跳过");
                return null;
            }

            //左区间:出生列左侧;右区间:主竖井右侧;先扣触井禁带再选侧取点。
            //genRand先选侧再取点(决定论F22),随机消耗恒2次(NextBool+Next),
            //在LayerPlanPass的R4注释账上登记为"禁室之后第3、4次定点消耗";
            //一切退化分支用BurnRolls补掷凑满,恒2次无条件成立
            int leftMin = DungeonworldMetrics.PlayLeft + 4;
            int leftMax = DungeonworldMetrics.SpawnX - SpawnKeepAway - FloodGalleryRoom.Width;
            int rightMin = DungeonworldMetrics.ShaftLeft + DungeonworldMetrics.ShaftWidth + ShaftKeepAway;
            int rightMax = DungeonworldMetrics.PlayRight - 4 - FloodGalleryRoom.Width;
            var leftSegs = new List<(int min, int max)> { (leftMin, leftMax) };
            var rightSegs = new List<(int min, int max)> { (rightMin, rightMax) };
            VerticalLinks.ExcludeZones(3, FloodGalleryRoom.Width, leftSegs);
            VerticalLinks.ExcludeZones(3, FloodGalleryRoom.Width, rightSegs);

            bool pickLeft = WorldGen.genRand.NextBool();
            List<(int min, int max)> side = pickLeft ? leftSegs : rightSegs;
            if (VerticalLinks.SegLength(side) <= 0) {
                //每口井禁带折算约80候选列,扣不空900列级区间;真到这步=常量被改坏
                CWRMod.Instance.Logger.Warn("[Dungeonworld] 泄洪堂所选侧被井位禁带扣空,换侧落位");
                side = pickLeft ? rightSegs : leftSegs;
            }
            //PickFromSegments对空段表零消耗直接返回-1,该分支由下方补掷找平
            int originX = VerticalLinks.PickFromSegments(side);
            if (originX < 0) {
                BurnRolls(1);
                CWRMod.Instance.Logger.Error(
                    "[Dungeonworld] 泄洪堂两侧均无合法落位,本次跳过,责任=常量表/井位禁带");
                return null;
            }

            LastOrigin = new Point(originX, originY);
            CWRMod.Instance.Logger.Info(
                $"[Dungeonworld] 泄洪堂落位 origin=({originX},{originY})"
                + $" 门槽行={originY + FloodGalleryRoom.LeftDoorOffset.Y}..{originY + floorRel - 1} 脊地板={l4.SpineFloorTop}");
            return LastOrigin;
        }

        /// <summary>退化分支补掷:把本函数的genRand消耗凑满恒定值(R4账),掷出的数弃用</summary>
        private static void BurnRolls(int count) {
            for (int i = 0; i < count; i++) {
                WorldGen.genRand.Next(100);
            }
        }
    }
}
