using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Rendering;
using CalamityOverhaul.Content.TimeFreezes;
using InnoVault.Cinematics;
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

        public KingSlimeHopState() : this(2) {
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
                    //落地缓冲拍：短暂滞留(收紧，呼吸口保留)
                    npc.velocity.X *= 0.78f;
                    phaseTimer++;
                    int rest = context.IsPhase2 ? 4 : 7;
                    if (Grounded(npc) && phaseTimer >= rest) {
                        //吞没投技优先：P2冷却好且目标在压制带内，超级砸落开吞
                        IKingSlimeState engulf = TryEngulf(context);
                        if (engulf != null) {
                            return engulf;
                        }
                        //中距液化掠近：把本次跳跃逼近换成潮汐位移(签名招兼位移工具)
                        IKingSlimeState travel = TryTideTravel(context);
                        if (travel != null) {
                            return travel;
                        }
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

        /// <summary>
        /// 手作出招环：定序天然防背靠背复读且免RNG同步，弃洗牌袋。
        /// P1 潮汐两倍频教学；P2 八槽按"水平威胁→零区威胁→弹道威胁→点威胁"轮换，
        /// 签名招(潮汐/立塔/质心抛掷)各双槽，通用招(天坠/迫击)各单槽
        /// </summary>
        private static readonly KingSlimeStateIndex[] RingP1 = [
            KingSlimeStateIndex.TideRush,
            KingSlimeStateIndex.CrownSlam,
            KingSlimeStateIndex.TideRush,
            KingSlimeStateIndex.GelMortar,
        ];

        private static readonly KingSlimeStateIndex[] RingP2 = [
            KingSlimeStateIndex.TideRush,
            KingSlimeStateIndex.TowerCollapse,
            KingSlimeStateIndex.MassEject,
            KingSlimeStateIndex.CrownSlam,
            KingSlimeStateIndex.TideRush,
            KingSlimeStateIndex.GelMortar,
            KingSlimeStateIndex.TowerCollapse,
            KingSlimeStateIndex.MassEject,
        ];

        /// <summary>窥视环上下一招(不推进索引)，液化掠近防潮汐复读用</summary>
        internal static KingSlimeStateIndex PeekNextAttack(KingSlimeStateContext context) {
            KingSlimeStateIndex[] ring = context.IsPhase2 ? RingP2 : RingP1;
            return ring[context.AttackPhaseIndex % ring.Length];
        }

        /// <summary>取环上下一招并推进索引；只服务端调用(潮汐位移重组后也直入此处)</summary>
        internal static IKingSlimeState ChooseNextAttack(KingSlimeStateContext context) {
            KingSlimeStateIndex next = PeekNextAttack(context);
            context.AttackPhaseIndex++;
            return CreateAttack(next);
        }

        private static IKingSlimeState CreateAttack(KingSlimeStateIndex index) => index switch {
            KingSlimeStateIndex.CrownSlam => new KingSlimeCrownSlamState(),
            KingSlimeStateIndex.GelMortar => new KingSlimeGelMortarState(),
            KingSlimeStateIndex.TowerCollapse => new KingSlimeTowerCollapseState(),
            KingSlimeStateIndex.MassEject => new KingSlimeMassEjectState(),
            KingSlimeStateIndex.TideRush => new KingSlimeTideRushState(),
            //环表外索引(新招忘挂case)：回连接器自愈，不静默错招
            _ => new KingSlimeHopState(),
        };

        /// <summary>
        /// 吞没投技判定(服务端)：P2解锁+长冷却+目标在压制带(90~700px、近同层)+有视线，
        /// 且不处于狂暴/时停/运镜中。全条件过→超级砸落开吞
        /// </summary>
        private static IKingSlimeState TryEngulf(KingSlimeStateContext context) {
            if (VaultUtils.isClient || !context.Phase2Started || context.EngulfCooldown > 0) {
                return null;
            }
            Player player = context.Target;
            if (!player.Alives()) {
                return null;
            }
            NPC npc = context.Npc;
            //狂暴期免伤增压，不叠投技；时停/演出期禁触发(公平阀)
            if (context.Host != null && context.Host.ai[4] == 1f) {
                return null;
            }
            if (TimeFreezeSystem.IsFrozen(npc) || TimeFreezeSystem.IsAnyGlobalFreezeActive) {
                return null;
            }
            if (CutsceneDirector.CurrentClip != null) {
                return null;
            }
            float dx = Math.Abs(player.Center.X - npc.Center.X);
            float dy = player.Center.Y - npc.Center.Y;
            //压制带：不贴脸(留可读起跳弧线)也不超远；目标大致同层或略低
            if (dx < 90f || dx > 700f || dy < -260f || dy > 420f) {
                return null;
            }
            if (!Collision.CanHitLine(npc.Center, 0, 0, player.Center, 0, 0)) {
                return null;
            }
            return new KingSlimeEngulfState();
        }

        /// <summary>
        /// 中距液化掠近判定(服务端)：目标在中距带(720~1500px)且大致同层时，
        /// 用潮汐冲刷代替干跳逼近——更远交给追击阀潜地(1900+)，冷却防连锁液化。
        /// 环上下一招是潮汐时让位(防复读)
        /// </summary>
        private static IKingSlimeState TryTideTravel(KingSlimeStateContext context) {
            if (VaultUtils.isClient || context.TideTravelCooldown > 0) {
                return null;
            }
            Player player = context.Target;
            if (!player.Alives() || PeekNextAttack(context) == KingSlimeStateIndex.TideRush) {
                return null;
            }
            NPC npc = context.Npc;
            float dx = Math.Abs(player.Center.X - npc.Center.X);
            float dy = Math.Abs(player.Center.Y - npc.Center.Y);
            if (dx < 720f || dx > 1500f || dy > 280f) {
                return null;
            }
            context.TideTravelCooldown = 620;
            context.TideTravelActive = true;
            return new KingSlimeTideRushState();
        }
    }
}
