using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.Blessings.Types
{
    /// <summary>世纪之花·荆棘新生：生命长青，受创后新芽更盛</summary>
    internal sealed class PlanteraBlessing : Blessing
    {
        public override int ProgressOrder => 130;

        public override int[] AnchorNPCTypes => [NPCID.Plantera];

        public override string SigilPath =>
            "M50,24 Q68,28 66,48 Q64,66 50,72 Q36,66 34,48 Q32,28 50,24 Z M50,72 L50,86 M50,78 L40,84 M50,78 L60,84";

        //槽位：0=新芽余时
        public override int StateSlots => 1;

        public override void PostHurt(BlessingPlayer bp, in Player.HurtInfo info)
            => bp.StateOf(this)[0] = BlessingTuning.PlanteraSurgeDuration;

        public override void PostUpdate(BlessingPlayer bp) {
            float[] state = bp.StateOf(this);
            if (state[0] > 0f) {
                state[0]--;
            }
        }

        public override void UpdateLifeRegen(BlessingPlayer bp) {
            int regen = BlessingTuning.PlanteraRegenBase;
            if (bp.StateOf(this)[0] > 0f) {
                regen += BlessingTuning.PlanteraRegenSurge;
            }
            bp.Player.lifeRegen += regen;
        }
    }
}
