using Terraria.Utilities;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Rooms
{
    //房间落位:预留-失败-重试-缩房-放弃(§3.2-3),纯数据零tile写入(§1.5-1)
    //随机全走传入的rand(gen期=WorldGen.genRand),保持决定论纪律(F22)
    internal static class RoomPlacer
    {
        /// <summary>
        /// 在[xMin,xMax)内随机落一间地板首行=floorTop的房。
        /// interior尺寸区间为内膛净尺寸(不含壳);前半程尝试随机尺寸,
        /// 后半程强制最小尺寸(缩房),全部失败返回null并留在栅格拒绝计数里。
        /// </summary>
        internal static RoomNode TryPlace(OccupancyGrid grid, UnifiedRandom rand,
            int xMin, int xMax, int floorTop, Point interiorMin, Point interiorMax, int retries = 12) {
            int shell = DungeonworldMetrics.RoomShellThick;
            for (int attempt = 0; attempt < retries; attempt++) {
                bool shrink = attempt >= retries / 2;
                int iw = shrink ? interiorMin.X : rand.Next(interiorMin.X, interiorMax.X + 1);
                int ih = shrink ? interiorMin.Y : rand.Next(interiorMin.Y, interiorMax.Y + 1);
                int totalW = iw + shell * 2;
                int totalH = ih + shell * 2;
                if (xMin + totalW > xMax) {
                    return null;
                }
                int left = rand.Next(xMin, xMax - totalW + 1);
                //Bounds含壳:内膛[Top+shell,floorTop)即净高ih
                var bounds = new Rectangle(left, floorTop - ih - shell, totalW, totalH);
                if (grid.TryReserve(bounds, DungeonworldMetrics.RoomPadding)) {
                    return new RoomNode { Bounds = bounds };
                }
            }
            return null;
        }
    }
}
