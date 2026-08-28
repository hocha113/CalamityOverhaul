using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.BossRooms
{
    //验收堂选址(WAVE2-MINIBOSS.md §4.9):铸造机关层L6,每世界1间,镜像FloodGallerySiting全套
    //
    //垂直=内联跨脊:内膛地板行(rel 30)对齐L6脊地板行,门槽(3深x4高,底沿即室内地板)
    //恰与脊走廊底部4行重合,"验收堂钉在铸造场的出场检票口上"。
    //P30定点+足印预留先于一切L6内容运行:末折布房(L6Content)、巨像装配湾(L6Colossus
    //CanReserve扫描)、渣汽疏泄带(ZonePass消费剩余空间)全部构造性避让本足印。
    //
    //时序:PickOrigin由P30 LayerPlanPass在泄洪堂定点之后调用(R4账:定点消耗第5、6次),
    //genRand恒2次(NextBool选侧+Next取点);P45后的ProofingHallPass只消费LastOrigin盖章
    internal static class ProofingHallSiting
    {
        //本次生成的落位;ShouldSave=false回放制下每次生成重算
        internal static Point? LastOrigin;

        private const int ShaftKeepAway = 30;
        private const int SpawnKeepAway = 10;

        internal static Point? PickOrigin() {
            LastOrigin = null;
            LayerBand l6 = DungeonworldMetrics.Bands[5];

            int floorRel = ProofingHallRoom.LeftDoorOffset.Y + ProofingHallRoom.DoorHeight;
            int originY = l6.SpineFloorTop - floorRel;

            //放不下=硬错误跳过。R4:跳过也要把本函数的2次账掷满,否则退化种子上全链路随机流错位
            if (originY < l6.Top + 2 || originY + ProofingHallRoom.Height > l6.Bottom) {
                BurnRolls(2);
                CWRMod.Instance.Logger.Error(
                    $"[Dungeonworld] 验收堂垂直放不进L6带[{l6.Top},{l6.Bottom}),originY={originY},本次跳过");
                return null;
            }

            int leftMin = DungeonworldMetrics.PlayLeft + 4;
            int leftMax = DungeonworldMetrics.SpawnX - SpawnKeepAway - ProofingHallRoom.Width;
            int rightMin = DungeonworldMetrics.ShaftLeft + DungeonworldMetrics.ShaftWidth + ShaftKeepAway;
            int rightMax = DungeonworldMetrics.PlayRight - 4 - ProofingHallRoom.Width;
            var leftSegs = new List<(int min, int max)> { (leftMin, leftMax) };
            var rightSegs = new List<(int min, int max)> { (rightMin, rightMax) };
            VerticalLinks.ExcludeZones(5, ProofingHallRoom.Width, leftSegs);
            VerticalLinks.ExcludeZones(5, ProofingHallRoom.Width, rightSegs);

            bool pickLeft = WorldGen.genRand.NextBool();
            List<(int min, int max)> side = pickLeft ? leftSegs : rightSegs;
            if (VerticalLinks.SegLength(side) <= 0) {
                CWRMod.Instance.Logger.Warn("[Dungeonworld] 验收堂所选侧被井位禁带扣空,换侧落位");
                side = pickLeft ? rightSegs : leftSegs;
            }
            //PickFromSegments对空段表零消耗直接返回-1,该分支由补掷找平
            int originX = VerticalLinks.PickFromSegments(side);
            if (originX < 0) {
                BurnRolls(1);
                CWRMod.Instance.Logger.Error(
                    "[Dungeonworld] 验收堂两侧均无合法落位,本次跳过,责任=常量表/井位禁带");
                return null;
            }

            LastOrigin = new Point(originX, originY);
            CWRMod.Instance.Logger.Info(
                $"[Dungeonworld] 验收堂落位 origin=({originX},{originY})"
                + $" 门槽行={originY + ProofingHallRoom.LeftDoorOffset.Y}..{originY + floorRel - 1} 脊地板={l6.SpineFloorTop}");
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
