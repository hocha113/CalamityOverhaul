using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.Blessings.Types
{
    /// <summary>机械骷髅王·过载骨架：机件过载，使用速度提升</summary>
    internal sealed class SkeletronPrimeBlessing : Blessing
    {
        public override int ProgressOrder => 120;

        public override int[] AnchorNPCTypes => [NPCID.SkeletronPrime];

        public override string SigilPath =>
            "M36,30 L64,30 L64,58 L36,58 Z M36,30 L28,22 M64,30 L72,22 M42,58 L42,70 L58,70 L58,58 M44,42 L48,46 M60,42 L56,46";

        public override float UseSpeedMultiplier(BlessingPlayer bp, Item item)
            => BlessingTuning.PrimeUseSpeedMult;
    }
}
