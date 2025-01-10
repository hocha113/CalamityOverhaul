using System;

namespace CalamityOverhaul.Content.UIs.SupertableUIs
{
    public class RecipeData
    {
        public int Target { get; set; }
        public string[] Values { get; set; }
        private int? _cachedHashCode;

        #region Operator
        public static bool operator ==(RecipeData left, RecipeData right) {
            if (ReferenceEquals(left, right)) {
                return true;
            }
            if (ReferenceEquals(left, null) || ReferenceEquals(right, null)) {
                return false;
            }
            return left.Equals(right);
        }

        public static bool operator !=(RecipeData left, RecipeData right) => !(left == right);

        public override bool Equals(object obj) {
            if (obj is not RecipeData other) {
                return false;
            }

            // 快速检查 Target 和数组长度
            if (Target != other.Target || Values.Length != other.Values.Length) {
                return false;
            }

            // 按需逐项比较 Values
            for (int i = 0; i < Values.Length; i++) {
                if (!string.Equals(Values[i], other.Values[i])) {
                    return false;
                }
            }

            return true;
        }

        public override int GetHashCode() {
            if (_cachedHashCode.HasValue) {
                return _cachedHashCode.Value;
            }

            int hash = Target.GetHashCode();

            // 用固定范围减少数组哈希计算开销
            const int MaxHashElements = 5; // 比较最多 5 个元素
            for (int i = 0; i < Math.Min(Values.Length, MaxHashElements); i++) {
                hash = hash * 31 + (Values[i]?.GetHashCode() ?? 0);
            }
            _cachedHashCode = hash;
            return hash;
        }

        #endregion
    }
}
