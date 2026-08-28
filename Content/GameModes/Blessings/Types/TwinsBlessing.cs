using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.Blessings.Types
{
    /// <summary>双子魔眼·双瞳协奏：连续命中同一目标时伤害逐层递增</summary>
    internal sealed class TwinsBlessing : Blessing
    {
        public override int ProgressOrder => 100;

        public override int[] AnchorNPCTypes => [NPCID.Retinazer, NPCID.Spazmatism];

        public override string SigilPath =>
            "M24,50 Q24,34 40,34 Q56,34 56,50 Q56,66 40,66 Q24,66 24,50 Z M44,50 Q44,34 60,34 Q76,34 76,50 Q76,66 60,66 Q44,66 44,50 Z";

        //一眼倒下时另一眼犹在则不算讨伐成功
        public override bool IsBossFullyDown(NPC npc) {
            int other = npc.type == NPCID.Retinazer ? NPCID.Spazmatism : NPCID.Retinazer;
            foreach (NPC alive in Main.ActiveNPCs) {
                if (alive.type == other && alive.life > 0) {
                    return false;
                }
            }
            return true;
        }

        //槽位：0=当前目标 whoAmI+1（0=无） 1=层数
        public override int StateSlots => 2;

        public override void ModifyHitNPC(BlessingPlayer bp, NPC target, ref NPC.HitModifiers modifiers) {
            float[] state = bp.StateOf(this);
            if ((int)state[0] == target.whoAmI + 1 && state[1] > 0f) {
                modifiers.FinalDamage *= 1f + state[1] * BlessingTuning.TwinsStackDamage;
            }
        }

        public override void OnHitNPC(BlessingPlayer bp, NPC target, in NPC.HitInfo hit, int damageDone) {
            float[] state = bp.StateOf(this);
            if ((int)state[0] == target.whoAmI + 1) {
                state[1] = Math.Min(state[1] + 1f, BlessingTuning.TwinsMaxStacks);
            }
            else {
                state[0] = target.whoAmI + 1;
                state[1] = 0f;
            }
        }
    }
}
