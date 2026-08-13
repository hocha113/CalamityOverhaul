namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Rooms
{
    //占用栅格(§3.2-3):房间落位前预留,失败重试/缩房/放弃,
    //不做任何"重叠后合并"——重叠在本表示法里不该发生
    //区域局部坐标存储,构造时给定管辖矩形(通常=某层带内膛)
    internal sealed class OccupancyGrid
    {
        private readonly Rectangle _area;
        private readonly bool[,] _used;

        internal long ReserveOk;
        internal long ReserveReject;

        internal OccupancyGrid(Rectangle area) {
            _area = area;
            _used = new bool[area.Width, area.Height];
        }

        //越界视为不可预留:层带/隔离带边界约束在这里对接(§1.2)
        internal bool CanReserve(Rectangle rect, int padding) {
            Rectangle padded = new(rect.X - padding, rect.Y - padding,
                rect.Width + padding * 2, rect.Height + padding * 2);
            if (padded.Left < _area.Left || padded.Top < _area.Top
                || padded.Right > _area.Right || padded.Bottom > _area.Bottom) {
                return false;
            }
            for (int x = padded.Left; x < padded.Right; x++) {
                for (int y = padded.Top; y < padded.Bottom; y++) {
                    if (_used[x - _area.Left, y - _area.Top]) {
                        return false;
                    }
                }
            }
            return true;
        }

        //预留成功才允许刻画;padding也一并标记,保证房间间距≥padding(§3.2-3)
        internal bool TryReserve(Rectangle rect, int padding) {
            if (!CanReserve(rect, padding)) {
                ReserveReject++;
                return false;
            }
            MarkUnchecked(new Rectangle(rect.X - padding, rect.Y - padding,
                rect.Width + padding * 2, rect.Height + padding * 2));
            ReserveOk++;
            return true;
        }

        //既成事实登记(脊走廊/竖井等先落位的宏观结构),不参与成败计数
        internal void MarkUnchecked(Rectangle rect) {
            int left = System.Math.Max(rect.Left, _area.Left);
            int top = System.Math.Max(rect.Top, _area.Top);
            int right = System.Math.Min(rect.Right, _area.Right);
            int bottom = System.Math.Min(rect.Bottom, _area.Bottom);
            for (int x = left; x < right; x++) {
                for (int y = top; y < bottom; y++) {
                    _used[x - _area.Left, y - _area.Top] = true;
                }
            }
        }
    }
}
