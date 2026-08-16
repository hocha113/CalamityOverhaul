using System.Collections.Generic;
using System.Text;

namespace CalamityOverhaul.Content.Scenarios.OldNet.Gen
{
    //逐pass耗时记录，P80 GenReport输出（ShouldSave=false 每次深潜重生成，生成耗时=玩家等待时间）
    //gen线程单线程使用；P10入口Reset，每次生成重记。镜像 Dungeonworld GenClock，不引用
    internal static class OldNetGenClock
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
