using System;

namespace CalamityOverhaul.Content.UIs.SupertableUIs
{
    /// <summary>
    /// 网格坐标结构，提供索引和坐标之间的转换
    /// </summary>
    public readonly struct GridCoordinate(int x, int y) : IEquatable<GridCoordinate>
    {
        public readonly int X = x;
        public readonly int Y = y;

        public GridCoordinate(Point point) : this(point.X, point.Y) { }

        /// <summary>
        /// 从屏幕位置创建网格坐标
        /// </summary>
        public static GridCoordinate FromScreenPosition(Vector2 screenPos, Vector2 gridTopLeft) {
            Vector2 relativePos = screenPos - gridTopLeft;
            int x = (int)(relativePos.X / SupertableConstants.CELL_WIDTH);
            int y = (int)(relativePos.Y / SupertableConstants.CELL_HEIGHT);
            return new GridCoordinate(x, y);
        }

        /// <summary>
        /// 转换为线性索引
        /// </summary>
        public int ToIndex() => Y * SupertableConstants.GRID_COLUMNS + X;

        /// <summary>
        /// 从线性索引创建坐标
        /// </summary>
        public static GridCoordinate FromIndex(int index) {
            int y = index / SupertableConstants.GRID_COLUMNS;
            int x = index - y * SupertableConstants.GRID_COLUMNS;
            return new GridCoordinate(x, y);
        }

        /// <summary>
        /// 转换为屏幕位置
        /// </summary>
        public Vector2 ToScreenPosition(Vector2 gridTopLeft) {
            return new Vector2(
                X * SupertableConstants.CELL_WIDTH,
                Y * SupertableConstants.CELL_HEIGHT
            ) + gridTopLeft;
        }

        /// <summary>
        /// 检查坐标是否在有效范围内
        /// </summary>
        public bool IsValid() {
            return X >= 0 && X < SupertableConstants.GRID_COLUMNS &&
                   Y >= 0 && Y < SupertableConstants.GRID_ROWS;
        }

        public bool Equals(GridCoordinate other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is GridCoordinate other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y);
        public override string ToString() => $"({X}, {Y})";

        public static bool operator ==(GridCoordinate left, GridCoordinate right) => left.Equals(right);
        public static bool operator !=(GridCoordinate left, GridCoordinate right) => !left.Equals(right);
    }
}
