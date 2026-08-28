using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.Blessings.Types
{
    /// <summary>光之女皇·昼光裁决：白昼之下，暴击伤害提高</summary>
    internal sealed class EmpressBlessing : Blessing
    {
        public override int ProgressOrder => 160;

        public override int[] AnchorNPCTypes => [NPCID.HallowBoss];

        public override string SigilPath =>
            "M50,58 Q30,28 24,50 Q28,68 50,60 Z M50,58 Q70,28 76,50 Q72,68 50,60 Z M50,40 L50,70 M50,40 L44,30 M50,40 L56,30";

        public override void ModifyHitNPC(BlessingPlayer bp, NPC target, ref NPC.HitModifiers modifiers) {
            if (Main.dayTime) {
                modifiers.CritDamage += BlessingTuning.EmpressDayCritDamage;
            }
        }
    }
}
