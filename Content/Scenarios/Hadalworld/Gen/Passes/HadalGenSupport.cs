using CalamityOverhaul.Content.Scenarios.Hadalworld.Gen.Core;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Terraria.IO;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Scenarios.Hadalworld.Gen.Passes
{
    //逐pass耗时账(镜像Dungeonworld GenClock,所有权隔离自建一份)
    internal static class HadalGenClock
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

    //计时装饰器(镜像Dungeonworld TimedPass):包任意GenPass记录耗时
    internal sealed class HadalTimedPass(GenPass inner) : GenPass(inner.Name, inner.Weight)
    {
        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration) {
            var watch = Stopwatch.StartNew();
            inner.Apply(progress, configuration);
            HadalGenClock.Record(inner.Name, watch.ElapsedMilliseconds);
        }
    }

    //一次生成的共享状态:核心层模型+直写计数,P80收尾释放
    internal static class HadalGenContext
    {
        internal static HadalTerrainModel Model;
        internal static long SolidWrites, WaterWrites, AirWrites, WallWrites;
        //装饰计数(成功/尝试),P80报告用
        internal static readonly Dictionary<string, (int ok, int tries)> Decor = [];

        internal static void ResetForNewGen() {
            Model = null;
            SolidWrites = WaterWrites = AirWrites = WallWrites = 0;
            Decor.Clear();
        }

        internal static void CountDecor(string key, bool ok) {
            (int okCount, int tries) = Decor.TryGetValue(key, out var v) ? v : (0, 0);
            Decor[key] = (okCount + (ok ? 1 : 0), tries + 1);
        }

        internal static string DecorSummary() {
            var sb = new StringBuilder();
            foreach ((string key, (int ok, int tries)) in Decor) {
                if (sb.Length > 0) {
                    sb.Append(',');
                }
                sb.Append(key).Append('=').Append(ok).Append('/').Append(tries);
            }
            return sb.Length > 0 ? sb.ToString() : "none";
        }
    }
}
