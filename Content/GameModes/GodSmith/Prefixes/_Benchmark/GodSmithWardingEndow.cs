using CalamityOverhaul.Content.GameModes.GodSmith.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Prefixes._Benchmark
{
    /// <summary>
    /// 【范例·被动型神赋】壁垒誓言：覆盖饰品防御系全档词缀（坚硬/防卫/装甲/守护），
    /// 佩戴时小幅受击减伤。被动型神赋 = 只在 UpdateAccessory 写数值；
    /// 数值随词缀档位回缩，是 <see cref="GodSmithEndow.TierScaleFor"/> 约定的标准范例，
    /// 描述里的数字用 DescFormatArgs 跟着档位走
    /// </summary>
    internal class GodSmithWardingEndow : GodSmithEndow
    {
        /// <summary>顶级档（守护）的受击减伤比例</summary>
        internal const float BaseDamageReduction = 0.02f;

        public override int[] CoveredPrefixes => [PrefixID.Hard, PrefixID.Guarding, PrefixID.Armored, PrefixID.Warding];

        public override float TierScaleFor(int prefixId) => prefixId switch {
            PrefixID.Warding => 1f,
            PrefixID.Armored => 0.8f,
            PrefixID.Guarding => 0.6f,
            _ => 0.4f,
        };

        protected override string EndowNameFallback => "Bulwark Oath";

        protected override string EndowDescFallback => "Reduces damage taken by {0}%";

        public override object[] DescFormatArgs(Item item)
            => [(BaseDamageReduction * 100f * TierScaleFor(item.prefix)).ToString("0.#")];

        public override void UpdateAccessory(Item item, Player player, bool hideVisual, GodSmithPlayer state, float tierScale)
            => player.endurance += BaseDamageReduction * tierScale;
    }
}
