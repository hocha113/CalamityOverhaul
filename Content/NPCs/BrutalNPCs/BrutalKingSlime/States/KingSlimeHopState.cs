using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Rendering;
using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.States
{
    /// <summary>
    /// 连跳压制：蹲缩蓄势→一帧爆发→重落。既是基础压迫也是攻击间连接器，
    /// 跳完由出招环选择下一招；阶段转换/追击阀只在本状态打断
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)KingSlimeStateIndex.Hop, typeof(KingSlimeStateContext))]
    internal class KingSlimeHopState : KingSlimeStateBase
    {
        public override string StateName => "Hop";
        public override KingSlimeStateIndex StateIndex => KingSlimeStateIndex.Hop;
        public override bool Interruptible => true;

        private int AnticipationTime(KingSlimeStateContext ctx) => ctx.IsPhase2 ? 11 : 15;

        private int hopsRemaining;
        /// <summary>0落地等待 1蹲缩蓄势 2空中</summary>
        private int hopPhase;
        private int phaseTimer;

        public KingSlimeHopState() : this(3) {
        }

        public KingSlimeHopState(int hops) {
            hopsRemaining = Math.Max(hops, 1);
        }

        public override void OnEnter(KingSlimeStateContext context) {
            base.OnEnter(context);
            hopPhase = 0;
            phaseTimer = 0;
        }

        public override IKingSlimeState OnUpdate(KingSlimeStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            Timer++;

            //看向目标
            npc.direction = npc.spriteDirection = DirToTarget(context);

            switch (hopPhase) {
                case 0: {
                    //落地缓冲拍：短暂滞留
                    npc.velocity.X *= 0.78f;
                    phaseTimer++;
                    int rest = context.IsPhase2 ? 5 : 9;
                    if (Grounded(npc) && phaseTimer >= rest) {
                        hopPhase = 1;
                        phaseTimer = 0;
                    }
                    break;
                }
                case 1: {
                    //蹲缩蓄势：末段急缩，伤害关闭(公平阀)
                    npc.velocity.X *= 0.7f;
                    context.ContactDamageScale = 0f;
                    phaseTimer++;
                    int anticipation = AnticipationTime(context);
                    float t = phaseTimer / (float)anticipation;
                    //pow末段猛压
                    context.VisualSquash = MathHelper.Lerp(context.VisualSquash, 1f - 0.42f * MathF.Pow(t, 3f), 0.45f);
                    context.AuraMode = 1;
                    context.AuraProgress = t * 0.6f;

                    if (phaseTimer >= anticipation) {
                        LaunchJump(context);
                        hopPhase = 2;
                        phaseTimer = 0;
                    }
                    break;
                }
                case 2: {
                    //空中：轻微追向，下坠更重(主控重力)
                    phaseTimer++;
                    if (player.Alives()) {
                        float steer = MathHelper.Clamp((player.Center.X - npc.Center.X) * 0.0005f, -0.09f, 0.09f);
                        npc.velocity.X += steer;
                    }

                    if (context.JustLanded || (phaseTimer > 12 && Grounded(npc))) {
                        hopsRemaining--;
                        hopPhase = 0;
                        phaseTimer = 0;

                        if (hopsRemaining <= 0 && !VaultUtils.isClient) {
                            return ChooseNextAttack(context);
                        }
                    }
                    break;
                }
            }

            //看门狗：任何异常卡死都回选招
            if (Timer > 540 && !VaultUtils.isClient) {
                return ChooseNextAttack(context);
            }

            return null;
        }

        /// <summary>一帧爆发起跳，末跳更高更远并预定冲击波</summary>
        private void LaunchJump(KingSlimeStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            bool finalHop = hopsRemaining <= 1;

            float lead = finalHop ? 20f : 13f;
            float dx = player.Center.X + player.velocity.X * lead - npc.Center.X;
            float vx;
            float vy;
            if (finalHop) {
                vx = MathHelper.Clamp(dx / 34f, -13.5f, 13.5f);
                vy = -16.5f;
                context.PendingLandingShockwave = 1;
            }
            else {
                vx = MathHelper.Clamp(dx / 40f, -9.5f, 9.5f);
                vy = -12.2f;
                context.PendingLandingShockwave = 0;
            }
            //目标在上方则跳更高
            float dy = player.Center.Y - npc.Center.Y;
            if (dy < -140f) {
                vy -= MathHelper.Clamp(-dy * 0.014f, 0f, 6f);
            }
            if (context.IsDeathMode) {
                vx *= 1.18f;
            }

            LaunchHop(npc, vx, vy);
            context.StretchImpulse(0.34f);
            context.LandingSplashMul = finalHop ? 1.35f : 1f;
            KingSlimeGelFX.SquishSound(npc.Bottom, -0.25f, 0.85f);
        }

        /// <summary>手作出招环，阶段各一张表；只服务端调用</summary>
        private IKingSlimeState ChooseNextAttack(KingSlimeStateContext context) {
            IKingSlimeState[] sequence = context.IsPhase2
                ? [
                    new KingSlimeTowerCollapseState(),
                    new KingSlimeNinjaFlurryState(),
                    new KingSlimeGelMortarState(),
                    new KingSlimeCrownSlamState(),
                    new KingSlimeSplitSwarmState(),
                    new KingSlimeTideRushState(),
                    new KingSlimeNinjaFlurryState(),
                    new KingSlimeGelMortarState(),
                ]
                : [
                    new KingSlimeCrownSlamState(),
                    new KingSlimeGelMortarState(),
                    new KingSlimeTideRushState(),
                    new KingSlimeGelMortarState(),
                ];

            IKingSlimeState next = sequence[context.AttackPhaseIndex % sequence.Length];
            context.AttackPhaseIndex++;
            return next;
        }
    }
}
