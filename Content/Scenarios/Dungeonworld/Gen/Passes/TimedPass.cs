using System.Diagnostics;
using Terraria.IO;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Passes
{
    //计时装饰器:包住任意GenPass记录耗时进GenClock,不改被包pass
    //(尤其是C路的GaolBossRoomPass,所有权禁改)——GenPass.Apply公开(对源核实)
    internal sealed class TimedPass(GenPass inner) : GenPass(inner.Name, inner.Weight)
    {
        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration) {
            var watch = Stopwatch.StartNew();
            inner.Apply(progress, configuration);
            GenClock.Record(inner.Name, watch.ElapsedMilliseconds);
        }
    }
}
