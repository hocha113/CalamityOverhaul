using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.Blessings.Types
{
    /// <summary>克苏鲁之脑·痛觉共感：受击后短暂化痛为势，造成的伤害提高</summary>
    internal sealed class BrainOfCthulhuBlessing : Blessing
    {
        public override int ProgressOrder => 40;

        public override int[] AnchorNPCTypes => [NPCID.BrainofCthulhu];

        public override string SigilPath =>
            "M50,24 Q30,22 28,42 Q26,60 40,66 Q36,78 50,78 Q64,78 60,66 Q74,60 72,42 Q70,22 50,24 Z M45,42 L55,50 L46,58";

        //槽位：0=共感余时
        public override int StateSlots => 1;

        public override void PostHurt(BlessingPlayer bp, in Player.HurtInfo info)
            => bp.StateOf(this)[0] = BlessingTuning.BrainSurgeDuration;

        public override void PostUpdate(BlessingPlayer bp) {
            float[] state = bp.StateOf(this);
            if (state[0] > 0f) {
                state[0]--;
            }
        }

        public override void ModifyHitNPC(BlessingPlayer bp, NPC target, ref NPC.HitModifiers modifiers) {
            if (bp.StateOf(this)[0] > 0f) {
                modifiers.FinalDamage *= 1f + BlessingTuning.BrainSurgeDamage;
            }
        }
    }
}
