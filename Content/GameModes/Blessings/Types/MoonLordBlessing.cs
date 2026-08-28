using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.Blessings.Types
{
    /// <summary>月球领主·星核共鸣：星核共振，造成的一切伤害提高</summary>
    internal sealed class MoonLordBlessing : Blessing
    {
        public override int ProgressOrder => 180;

        public override int[] AnchorNPCTypes => [NPCID.MoonLordCore];

        public override string SigilPath =>
            "M50,32 Q68,50 50,68 Q32,50 50,32 Z M50,16 L50,26 M50,74 L50,84 M16,50 L26,50 M74,50 L84,50 M28,28 L36,36 M72,28 L64,36";

        public override void UpdateEquips(BlessingPlayer bp)
            => bp.Player.GetDamage(DamageClass.Generic) += BlessingTuning.MoonLordDamage;
    }
}
