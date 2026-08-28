using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.Blessings.Types
{
    /// <summary>血肉之墙·血肉盟约：受创后一部分血肉缓慢归还</summary>
    internal sealed class WallOfFleshBlessing : Blessing
    {
        public override int ProgressOrder => 80;

        public override int[] AnchorNPCTypes => [NPCID.WallofFlesh];

        public override string SigilPath =>
            "M32,18 Q42,34 32,50 Q22,66 32,82 M56,40 Q72,50 56,60 Q48,50 56,40 Z";

        //槽位：0=待还血池 1=每帧流速
        public override int StateSlots => 2;

        public override void PostHurt(BlessingPlayer bp, in Player.HurtInfo info) {
            float[] state = bp.StateOf(this);
            state[0] += info.Damage * BlessingTuning.WallFleshRefundRatio;
            state[1] = state[0] / BlessingTuning.WallFleshRefundDuration;
        }

        public override void PostUpdate(BlessingPlayer bp) {
            float[] state = bp.StateOf(this);
            if (state[0] <= 0f) {
                return;
            }
            state[0] = Math.Max(0f, state[0] - state[1]);
        }

        public override void UpdateLifeRegen(BlessingPlayer bp) {
            float[] state = bp.StateOf(this);
            if (state[0] <= 0f) {
                return;
            }
            //lifeRegen 单位 2=每秒 1 点：流速(点/帧)×120 即等效再生
            bp.Player.lifeRegen += Math.Max(1, (int)(state[1] * 120f));
        }
    }
}
