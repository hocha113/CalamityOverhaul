using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Common;
using CalamityOverhaul.Content.TimeFreezes;
using InnoVault.Cinematics;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core
{
    /// <summary>合击协调，先到 RequestCombo，搭档 TryFollowSignal</summary>
    internal static class TwinsComboCoordinator
    {
        /// <summary>大招血量阈值，任一低于则剪刀死光</summary>
        private const float UltimateLifeRatio = 0.4f;

        /// <summary>钳形投技解锁血量阈值，任一低于即可</summary>
        private const float PincerLifeRatio = 0.5f;

        /// <summary>钳形投技冷却，帧</summary>
        private const uint PincerCooldownTicks = 1500;

        /// <summary>广播合击信号并返回合击态</summary>
        public static ITwinsState InitiateCombo(TwinsStateContext context, TwinsStateIndex comboIndex, int comboStep) {
            TwinsStateContext.RequestCombo(comboIndex, comboStep);
            return CreateComboState(comboIndex, comboStep);
        }

        /// <summary>大招节点合击，投技就绪优先，其次低血剪刀，否则交叉冲</summary>
        public static ITwinsState InitiateUltimateOrCrossDash(TwinsStateContext context, int comboStep) {
            TwinsStateIndex comboIndex;
            if (PincerGrabReady(context)) {
                comboIndex = TwinsStateIndex.TwinsPincerGrab;
            }
            else if (UltimateUnlocked(context)) {
                comboIndex = TwinsStateIndex.TwinsScissorRay;
            }
            else {
                comboIndex = TwinsStateIndex.TwinsCrossDash;
            }
            return InitiateCombo(context, comboIndex, comboStep);
        }

        /// <summary>
        /// 钳形投技是否就绪：二阶段、非独眼、任一眼低于阈值、冷却完、
        /// 搭档存活且处于可跟进的锚点态、无时停、无演出接管
        /// </summary>
        public static bool PincerGrabReady(TwinsStateContext context) {
            if (!context.IsSecondPhase || context.IsSoloRageMode || context.IsInPhaseTransition) {
                return false;
            }

            NPC self = context.Npc;
            NPC partner = TwinsStateContext.GetPartnerNpc(self.type);
            if (!self.Alives() || !partner.Alives()) {
                return false;
            }

            //解锁血线：任一眼低于 50%
            bool selfLow = self.life < self.lifeMax * PincerLifeRatio;
            bool partnerLow = partner.life < partner.lifeMax * PincerLifeRatio;
            if (!selfLow && !partnerLow) {
                return false;
            }

            //冷却，扑空减半鼓励重试
            uint cooldown = TwinsStateContext.PincerLastWasWhiff
                ? PincerCooldownTicks / 2 : PincerCooldownTicks;
            if (TwinsStateContext.PincerLastEndUpdate != 0
                && Main.GameUpdateCount - TwinsStateContext.PincerLastEndUpdate < cooldown) {
                return false;
            }

            //搭档须在二阶段锚点态(能 TryFollowSignal 立即跟进)，否则钳形缺一颚
            int partnerState = (int)partner.ai[1];
            bool partnerAtAnchor = partnerState == (int)TwinsStateIndex.SpazmatismFlameChase
                || partnerState == (int)TwinsStateIndex.RetinazerVerticalBarrage;
            if (!partnerAtAnchor) {
                return false;
            }

            //时停期间不得触发
            if (TimeFreezeSystem.IsFrozen(self) || TimeFreezeSystem.IsFrozen(partner)) {
                return false;
            }

            //单机端权威且有演出在播时避让(服务端 CurrentClip 恒 null)
            if (!Main.dedServ && CutsceneDirector.CurrentClip != null) {
                return false;
            }

            return true;
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
                TwinsStateIndex.TwinsPincerGrab => new TwinsPincerGrabState(comboStep),
                _ => new TwinsCombinedAttackState(comboStep),
            };
        }
    }
}
