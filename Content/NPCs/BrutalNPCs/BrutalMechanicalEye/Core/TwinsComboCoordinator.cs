using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Common;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core
{
    /// <summary>合击协调，先到 RequestCombo，搭档 TryFollowSignal</summary>
    internal static class TwinsComboCoordinator
    {
        /// <summary>大招血量阈值，任一低于则剪刀死光</summary>
        private const float UltimateLifeRatio = 0.4f;

        /// <summary>广播合击信号并返回合击态</summary>
        public static ITwinsState InitiateCombo(TwinsStateContext context, TwinsStateIndex comboIndex, int comboStep) {
            TwinsStateContext.RequestCombo(comboIndex, comboStep);
            return CreateComboState(comboIndex, comboStep);
        }

        /// <summary>大招节点合击，低血剪刀否则交叉冲</summary>
        public static ITwinsState InitiateUltimateOrCrossDash(TwinsStateContext context, int comboStep) {
            TwinsStateIndex comboIndex = UltimateUnlocked(context)
                ? TwinsStateIndex.TwinsScissorRay
                : TwinsStateIndex.TwinsCrossDash;
            return InitiateCombo(context, comboIndex, comboStep);
        }

        /// <summary>任一眼是否低于大招阈值</summary>
        public static bool UltimateUnlocked(TwinsStateContext context) {
            NPC self = context.Npc;
            bool selfLow = self.Alives() && self.life < self.lifeMax * UltimateLifeRatio;
            NPC partner = TwinsStateContext.GetPartnerNpc(self.type);
            bool partnerLow = partner.Alives() && partner.life < partner.lifeMax * UltimateLifeRatio;
            return selfLow || partnerLow;
        }

        /// <summary>锚点跟进合击信号，无则 null</summary>
        public static ITwinsState TryFollowSignal(TwinsStateContext context) {
            int signal = TwinsStateContext.ComboSignal;
            if (signal < 0) {
                return null;
            }
            //已在该合击中
            if ((int)context.Npc.ai[1] == signal) {
                return null;
            }
            //搭档死则清信号
            NPC partner = TwinsStateContext.GetPartnerNpc(context.Npc.type);
            if (!partner.Alives()) {
                TwinsStateContext.ClearComboSignal();
                return null;
            }
            return CreateComboState((TwinsStateIndex)signal, TwinsStateContext.ComboSharedStep);
        }

        /// <summary>按索引建合击态</summary>
        public static ITwinsState CreateComboState(TwinsStateIndex comboIndex, int comboStep) {
            return comboIndex switch {
                TwinsStateIndex.TwinsCrossDash => new TwinsCrossDashState(comboStep),
                TwinsStateIndex.TwinsTetherSweep => new TwinsTetherSweepState(comboStep),
                TwinsStateIndex.TwinsScissorRay => new TwinsScissorRayState(comboStep),
                _ => new TwinsCombinedAttackState(comboStep),
            };
        }
    }
}
