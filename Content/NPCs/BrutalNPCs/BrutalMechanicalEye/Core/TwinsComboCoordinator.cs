using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Common;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core
{
    /// <summary>双子合击协调：先到节点 RequestCombo 并进入合击，搭档 TryFollowSignal 跟进</summary>
    internal static class TwinsComboCoordinator
    {
        /// <summary>大招解锁血量比例：任一眼低于该比例时合击升级为剪刀死光</summary>
        private const float UltimateLifeRatio = 0.4f;

        /// <summary>发起合击：广播信号并返回合击状态(发起者直接进入)</summary>
        public static ITwinsState InitiateCombo(TwinsStateContext context, TwinsStateIndex comboIndex, int comboStep) {
            TwinsStateContext.RequestCombo(comboIndex, comboStep);
            return CreateComboState(comboIndex, comboStep);
        }

        /// <summary>发起大招节点合击：任一眼血量低于阈值时为剪刀死光，否则为交叉冲刺</summary>
        public static ITwinsState InitiateUltimateOrCrossDash(TwinsStateContext context, int comboStep) {
            TwinsStateIndex comboIndex = UltimateUnlocked(context)
                ? TwinsStateIndex.TwinsScissorRay
                : TwinsStateIndex.TwinsCrossDash;
            return InitiateCombo(context, comboIndex, comboStep);
        }

        /// <summary>任一眼血量是否已低于大招解锁阈值</summary>
        public static bool UltimateUnlocked(TwinsStateContext context) {
            NPC self = context.Npc;
            bool selfLow = self.Alives() && self.life < self.lifeMax * UltimateLifeRatio;
            NPC partner = TwinsStateContext.GetPartnerNpc(self.type);
            bool partnerLow = partner.Alives() && partner.life < partner.lifeMax * UltimateLifeRatio;
            return selfLow || partnerLow;
        }

        /// <summary>锚点状态轮询：存在合击信号且自身尚未进入该合击时，返回要跟进的合击状态；否则返回null</summary>
        public static ITwinsState TryFollowSignal(TwinsStateContext context) {
            int signal = TwinsStateContext.ComboSignal;
            if (signal < 0) {
                return null;
            }
            //已在该合击中(理论上锚点状态不会，但防御一下)
            if ((int)context.Npc.ai[1] == signal) {
                return null;
            }
            //搭档必须存活，否则清掉信号
            NPC partner = TwinsStateContext.GetPartnerNpc(context.Npc.type);
            if (!partner.Alives()) {
                TwinsStateContext.ClearComboSignal();
                return null;
            }
            return CreateComboState((TwinsStateIndex)signal, TwinsStateContext.ComboSharedStep);
        }

        /// <summary>根据索引创建合击状态实例</summary>
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
