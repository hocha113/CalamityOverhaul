using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.Blessings.Types
{
    /// <summary>克苏鲁之眼·渊瞳：暴击率提升，入夜后再增</summary>
    internal sealed class EyeOfCthulhuBlessing : Blessing
    {
        public override int ProgressOrder => 20;

        public override int[] AnchorNPCTypes => [NPCID.EyeofCthulhu];

        public override string SigilPath =>
            "M16,50 Q50,18 84,50 Q50,82 16,50 Z M50,38 Q62,50 50,62 Q38,50 50,38 Z";

        public override void UpdateEquips(BlessingPlayer bp) {
            float crit = BlessingTuning.EyeCritBonus;
            if (!Main.dayTime) {
                crit += BlessingTuning.EyeNightCritBonus;
            }
            bp.Player.GetCritChance(DamageClass.Generic) += crit;
        }
    }
}
