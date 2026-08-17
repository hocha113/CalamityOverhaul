using System.Diagnostics;
using Terraria.IO;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Scenarios.OldNet.Gen.Passes
{
    //计时装饰器：包住任意GenPass记录耗时进OldNetGenClock，不改被包pass
    //（ShouldSave=false 每次深潜重生成，生成耗时=玩家等待时间，预算必须可见）
    internal sealed class OldNetTimedPass(GenPass inner) : GenPass(inner.Name, inner.Weight)
    {
        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration) {
            var watch = Stopwatch.StartNew();
            inner.Apply(progress, configuration);
            OldNetGenClock.Record(inner.Name, watch.ElapsedMilliseconds);
        }
    }
}
