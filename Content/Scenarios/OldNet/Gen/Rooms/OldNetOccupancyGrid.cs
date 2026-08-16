using System.Collections.Generic;

namespace CalamityOverhaul.Content.Scenarios.OldNet.Gen.Rooms
{
    //占用栅格：房间/锚位落位前预留，失败重试/缩房/放弃，不做"重叠后合并"
    //区域局部坐标存储，构造时给定管辖矩形（=某分带的全高内膛）
    //镜像 Dungeonworld OccupancyGrid，不引用
    internal sealed class OldNetOccupancyGrid
    {
        private readonly Rectangle _area;
        private readonly bool[,] _used;

        internal long ReserveOk;
        internal long ReserveReject;

        internal OldNetOccupancyGrid(Rectangle area) {
            _area = area;
            _used = new bool[area.Width, area.Height];
        }

        /// <summary>管辖矩形，消费方据此收敛扫描范围</summary>
        internal Rectangle Area => _area;

        //越界视为不可预留：分带边界约束在这里对接
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

        //预留成功才允许刻画；padding也一并标记，保证结构间距≥padding
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

        /// <summary>扫描行带 [top,top+height) 内的连续空闲横段（半开），窄于 minWidth 的碎段丢弃</summary>
        internal List<(int left, int right)> FreeSpans(int top, int height, int xFrom, int xTo, int minWidth) {
            var spans = new List<(int, int)>();
            int runStart = -1;
            for (int x = xFrom; x <= xTo; x++) {
                if (x < xTo && CanReserve(new Rectangle(x, top, 1, height), 0)) {
                    if (runStart < 0) {
                        runStart = x;
                    }
                    continue;
                }
                if (runStart >= 0 && x - runStart >= minWidth) {
                    spans.Add((runStart, x));
                }
                runStart = -1;
            }
            return spans;
        }

        //既成事实登记（竖井/平台厅等先落位的宏观结构），不参与成败计数
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
