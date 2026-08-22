using System.Collections.Generic;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Rooms
{
    //占用栅格(§3.2-3):房间落位前预留,失败重试/缩房/放弃,
    //不做任何"重叠后合并"，重叠在本表示法里不该发生
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

        /// <summary>管辖矩形,填充器据此收敛扫描范围</summary>
        internal Rectangle Area => _area;

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

        //===空闲区查询(填充体系专用)===
        //主内容全部落位之后,"还空着的地方"才是填充器的定义域。三个查询一律只读,
        //不动计数器,扫描范围由调用方收敛在层带内,不存在全图开销(R5)。

        /// <summary>
        /// 扫描行带 [top,top+height) 内的连续空闲横段(半开区间)。
        /// 窄于 minWidth 的碎段直接丢弃，比一间最小房还窄的缝填不出东西。
        /// </summary>
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

        /// <summary>
        /// 扫描列带 [left,left+width) 内的连续空闲竖段,用于探测主结构之间的纵向死带。
        /// </summary>
        internal List<(int top, int bottom)> FreeGaps(int left, int width, int yFrom, int yTo, int minHeight) {
            var gaps = new List<(int, int)>();
            int runStart = -1;
            for (int y = yFrom; y <= yTo; y++) {
                if (y < yTo && CanReserve(new Rectangle(left, y, width, 1), 0)) {
                    if (runStart < 0) {
                        runStart = y;
                    }
                    continue;
                }
                if (runStart >= 0 && y - runStart >= minHeight) {
                    gaps.Add((runStart, y));
                }
                runStart = -1;
            }
            return gaps;
        }

        /// <summary>区内空闲格数,填充报告用的分母;越界部分不计</summary>
        internal long CountFree(Rectangle rect) {
            int left = System.Math.Max(rect.Left, _area.Left);
            int top = System.Math.Max(rect.Top, _area.Top);
            int right = System.Math.Min(rect.Right, _area.Right);
            int bottom = System.Math.Min(rect.Bottom, _area.Bottom);
            long free = 0;
            for (int x = left; x < right; x++) {
                for (int y = top; y < bottom; y++) {
                    if (!_used[x - _area.Left, y - _area.Top]) {
                        free++;
                    }
                }
            }
            return free;
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
