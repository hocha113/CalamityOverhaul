using System.Collections.Generic;
using System.Text;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen
{
    //逐pass耗时记录,P80 GenReport输出(R5:进世界预算<3min,多种子回归基线)
    //gen线程单线程使用;P10入口Reset,每次生成重记
    internal static class GenClock
    {
        private static readonly List<(string name, long ms)> _records = [];

        internal static void Reset() => _records.Clear();

        internal static void Record(string name, long ms) => _records.Add((name, ms));

        internal static string Summary() {
            var sb = new StringBuilder();
            foreach ((string name, long ms) in _records) {
                if (sb.Length > 0) {
                    sb.Append(',');
                }
                sb.Append(name).Append('=').Append(ms).Append("ms");
            }
            return sb.Length > 0 ? sb.ToString() : "none";
        }
    }
}
