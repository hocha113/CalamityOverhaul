using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Common
{
    /// <summary>
    /// 双子魔眼同步转阶段动画状态
    /// 用于一阶段到二阶段的过渡演出，包含移动动画确保玩家可见
    /// </summary>
    internal class TwinsPhaseTransitionState : TwinsStateBase
    {
        public override string StateName => "TwinsPhaseTransition";

        /// <summary>
        /// 集合移动阶段
        /// </summary>
        private const int GatherPhase = 60;

        /// <summary>
        /// 对峙阶段
        /// </summary>
        private const int ConfrontPhase = 40;

        /// <summary>
        /// 收缩蓄力阶段
        /// </summary>
        private const int ContractPhase = 50;

        /// <summary>
        /// 爆发阶段
        /// </summary>
        private const int BurstPhase = 45;

        /// <summary>
        /// 分离阶段
        /// </summary>
        private const int SeparatePhase = 50;

        /// <summary>
        /// 恢复阶段
        /// </summary>
        private const int RecoveryPhase = 30;

        /// <summary>
        /// 总时长
        /// </summary>
        private const int TotalDuration = GatherPhase + ConfrontPhase + ContractPhase + BurstPhase + SeparatePhase + RecoveryPhase;

        private TwinsStateContext Context;
        private Vector2 gatherPoint;
        private Vector2 originalPosition;
        private float shakeIntensity;
        private bool hasBurst;
        private bool hasPlayedGatherSound;
        private NPC partnerNpc;

        public override void OnEnter(TwinsStateContext context) {
            base.OnEnter(context);
            Context = context;
            context.IsInPhaseTransition = true;
            originalPosition = context.Npc.Center;
            shakeIntensity = 0f;
            hasBurst = false;
            hasPlayedGatherSound = false;

            //寻找另一只眼睛
            partnerNpc = TwinsStateContext.GetPartnerNpc(context.Npc.type);

            //进入转阶段时设置无敌
            context.Npc.dontTakeDamage = true;
        }

        public override ITwinsState OnUpdate(TwinsStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            Timer++;

            //计算集合点(玩家上方)
            gatherPoint = player.Center + new Vector2(0, -350);

            //阶段1: 集合移动
            if (Timer <= GatherPhase) {
                ExecuteGatherPhase(npc, player);
            }
            //阶段2: 对峙
            else if (Timer <= GatherPhase + ConfrontPhase) {
                ExecuteConfrontPhase(npc, player);
            }
            //阶段3: 收缩蓄力
            else if (Timer <= GatherPhase + ConfrontPhase + ContractPhase) {
                ExecuteContractPhase(npc, player);
            }
            //阶段4: 爆发
            else if (Timer <= GatherPhase + ConfrontPhase + ContractPhase + BurstPhase) {
                ExecuteBurstPhase(npc, player);
            }
            //阶段5: 分离
            else if (Timer <= GatherPhase + ConfrontPhase + ContractPhase + BurstPhase + SeparatePhase) {
                ExecuteSeparatePhase(npc, player);
            }
            //阶段6: 恢复
            else {
                ExecuteRecoveryPhase(npc, player);
            }

            //转阶段完成
            if (Timer >= TotalDuration) {
                context.IsInPhaseTransition = false;
                return GetPhase2InitialState();
            }

            return null;
        }

        /// <summary>
        /// 集合移动阶段 - 两只眼睛飞向玩家上方集合
        /// </summary>
        private void ExecuteGatherPhase(NPC npc, Player player) {
            float progress = Timer / (float)GatherPhase;

            //计算各自的目标位置(玩家上方两侧)
            float sideOffset = Context.IsSpazmatism ? -120f : 120f;
            Vector2 targetPos = gatherPoint + new Vector2(sideOffset, 0);

            //快速移动到集合点
            float speed = 20f - progress * 10f;
            MoveTo(npc, targetPos, speed, 0.12f);

            //始终面向玩家
            FaceTarget(npc, player.Center);

            //跟随玩家移动
            npc.position += player.velocity * 0.5f;

            //集合音效
            if (!hasPlayedGatherSound && Timer == 1) {
                hasPlayedGatherSound = true;
                SoundEngine.PlaySound(SoundID.Item103 with { Pitch = -0.3f, Volume = 0.8f }, npc.Center);
            }

            //移动轨迹粒子
            if (!VaultUtils.isServer && Timer % 2 == 0) {
                int dustType = Context.IsSpazmatism ? DustID.SolarFlare : DustID.Vortex;
                Vector2 dustPos = npc.Center + Main.rand.NextVector2Circular(15, 15);
                Dust dust = Dust.NewDustDirect(dustPos, 1, 1, dustType, -npc.velocity.X * 0.3f, -npc.velocity.Y * 0.3f, 100, default, 1.3f);
                dust.noGravity = true;
            }

            //设置蓄力状态
            Context.SetChargeState(11, progress * 0.2f);
        }

        /// <summary>
        /// 对峙阶段 - 两只眼睛面对面悬停
        /// </summary>
        private void ExecuteConfrontPhase(NPC npc, Player player) {
            int phaseTimer = Timer - GatherPhase;
            float progress = phaseTimer / (float)ConfrontPhase;

            //悬停在位置
            float sideOffset = Context.IsSpazmatism ? -120f : 120f;
            Vector2 targetPos = gatherPoint + new Vector2(sideOffset, 0);
            npc.Center = Vector2.Lerp(npc.Center, targetPos, 0.1f);
            npc.velocity *= 0.9f;

            //跟随玩家
            npc.position += player.velocity;

            //面向对方(如果有)
            if (partnerNpc != null && partnerNpc.active) {
                FaceTarget(npc, partnerNpc.Center);
            }
            else {
                FaceTarget(npc, player.Center);
            }

            //对峙特效 - 两眼之间的能量连接
            if (!VaultUtils.isServer && phaseTimer % 3 == 0 && partnerNpc != null && partnerNpc.active) {
                Vector2 midPoint = (npc.Center + partnerNpc.Center) / 2f;
                int segments = 5;
                for (int i = 0; i < segments; i++) {
                    float t = i / (float)(segments - 1);
                    Vector2 linePos = Vector2.Lerp(npc.Center, partnerNpc.Center, t);
                    linePos += Main.rand.NextVector2Circular(5, 5);
                    Dust dust = Dust.NewDustDirect(linePos, 1, 1, DustID.Electric, 0, 0, 100, default, 1f + progress * 0.5f);
                    dust.noGravity = true;
                    dust.velocity = Vector2.Zero;
                }
            }

            //警告粒子
            if (!VaultUtils.isServer && phaseTimer % 4 == 0) {
                int dustType = Context.IsSpazmatism ? DustID.Torch : DustID.PurpleTorch;
                Vector2 dustPos = npc.Center + Main.rand.NextVector2Circular(npc.width, npc.height);
                Dust dust = Dust.NewDustDirect(dustPos, 1, 1, dustType, 0, 0, 100, default, 1.3f);
                dust.noGravity = true;
                dust.velocity = Vector2.Zero;
            }

            //对峙音效
            if (phaseTimer == 1) {
                SoundEngine.PlaySound(SoundID.NPCHit4 with { Pitch = -0.5f }, npc.Center);
            }

            //设置蓄力状态
            Context.SetChargeState(11, 0.2f + progress * 0.2f);
        }

        /// <summary>
        /// 收缩蓄力阶段
        /// </summary>
        private void ExecuteContractPhase(NPC npc, Player player) {
            int phaseTimer = Timer - GatherPhase - ConfrontPhase;
            float progress = phaseTimer / (float)ContractPhase;

            //记录当前位置作为震动基准
            if (phaseTimer == 1) {
                originalPosition = npc.Center;
            }

            //跟随玩家
            originalPosition += player.velocity;

            //逐渐增强的颤抖
            shakeIntensity = progress * 10f;
            Vector2 shake = Main.rand.NextVector2Circular(shakeIntensity, shakeIntensity);
            npc.Center = originalPosition + shake;
            npc.velocity = Vector2.Zero;

            //缩小效果
            npc.scale = 1f - progress * 0.2f;

            //面向对方
            if (partnerNpc != null && partnerNpc.active) {
                FaceTarget(npc, partnerNpc.Center);
            }

            //设置蓄力状态
            Context.SetChargeState(11, 0.4f + progress * 0.6f);

            //能量聚集粒子
            if (!VaultUtils.isServer && phaseTimer % 2 == 0) {
                int dustType = Context.IsSpazmatism ? DustID.SolarFlare : DustID.Vortex;
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float dist = 100f - progress * 70f;
                Vector2 dustPos = npc.Center + angle.ToRotationVector2() * dist;
                Dust dust = Dust.NewDustDirect(dustPos, 1, 1, dustType, 0, 0, 100, default, 1.8f + progress);
                dust.noGravity = true;
                dust.velocity = (npc.Center - dustPos).SafeNormalize(Vector2.Zero) * (6f + progress * 5f);
            }

            //两眼之间的能量流动
            if (!VaultUtils.isServer && phaseTimer % 2 == 0 && partnerNpc != null && partnerNpc.active) {
                float t = Main.rand.NextFloat();
                Vector2 linePos = Vector2.Lerp(npc.Center, partnerNpc.Center, t);
                Vector2 flowDir = (partnerNpc.Center - npc.Center).SafeNormalize(Vector2.Zero);
                if (!Context.IsSpazmatism) {
                    flowDir = -flowDir;
                }
                int dustType = Context.IsSpazmatism ? DustID.SolarFlare : DustID.Vortex;
                Dust dust = Dust.NewDustDirect(linePos, 1, 1, dustType, flowDir.X * 4, flowDir.Y * 4, 100, default, 1.3f);
                dust.noGravity = true;
            }

            //蓄力音效
            if (phaseTimer == 1) {
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.2f, Volume = 0.9f }, npc.Center);
            }
        }

        /// <summary>
        /// 爆发阶段
        /// </summary>
        private void ExecuteBurstPhase(NPC npc, Player player) {
            int phaseTimer = Timer - GatherPhase - ConfrontPhase - ContractPhase;
            float progress = phaseTimer / (float)BurstPhase;

            //跟随玩家
            originalPosition += player.velocity;

            //爆发瞬间
            if (!hasBurst) {
                hasBurst = true;

                //播放爆发音效
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.1f, Volume = 1.3f }, npc.Center);
                SoundEngine.PlaySound(SoundID.Item62 with { Volume = 1.2f }, npc.Center);

                //产生爆发粒子
                if (!VaultUtils.isServer) {
                    int dustType = Context.IsSpazmatism ? DustID.SolarFlare : DustID.Vortex;
                    for (int i = 0; i < 60; i++) {
                        float angle = MathHelper.TwoPi / 60f * i;
                        Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(10f, 20f);
                        Dust dust = Dust.NewDustDirect(npc.Center, 1, 1, dustType, vel.X, vel.Y, 100, default, 2.5f);
                        dust.noGravity = true;
                    }

                    //环形光波
                    for (int i = 0; i < 40; i++) {
                        float angle = MathHelper.TwoPi / 40f * i;
                        Vector2 vel = angle.ToRotationVector2() * 25f;
                        int glowDust = Context.IsSpazmatism ? DustID.Torch : DustID.PurpleTorch;
                        Dust dust = Dust.NewDustDirect(npc.Center, 1, 1, glowDust, vel.X, vel.Y, 0, default, 3f);
                        dust.noGravity = true;
                        dust.fadeIn = 1.5f;
                    }
                }

                //恢复位置
                npc.Center = originalPosition;
            }

            //逐渐放大并有弹跳效果
            float bounce = (float)Math.Sin(progress * MathHelper.Pi * 2f) * 0.1f * (1f - progress);
            npc.scale = 0.8f + progress * 0.2f + bounce;

            //停止蓄力特效
            Context.ResetChargeState();

            //能量波动粒子
            if (!VaultUtils.isServer && phaseTimer % 3 == 0) {
                int dustType = Context.IsSpazmatism ? DustID.SolarFlare : DustID.Vortex;
                float waveRadius = progress * 250f;
                for (int i = 0; i < 10; i++) {
                    float angle = MathHelper.TwoPi / 10f * i + progress * MathHelper.TwoPi;
                    Vector2 dustPos = npc.Center + angle.ToRotationVector2() * waveRadius;
                    Dust dust = Dust.NewDustDirect(dustPos, 1, 1, dustType, 0, 0, 100, default, 1.5f * (1f - progress * 0.5f));
                    dust.noGravity = true;
                    dust.velocity = angle.ToRotationVector2() * 3f;
                }
            }

            //轻微震动
            float smallShake = (1f - progress) * 4f;
            npc.Center = originalPosition + Main.rand.NextVector2Circular(smallShake, smallShake);
        }

        /// <summary>
        /// 分离阶段 - 两只眼睛分开，各自移动到战斗位置
        /// </summary>
        private void ExecuteSeparatePhase(NPC npc, Player player) {
            int phaseTimer = Timer - GatherPhase - ConfrontPhase - ContractPhase - BurstPhase;
            float progress = phaseTimer / (float)SeparatePhase;

            //恢复大小
            npc.scale = 1f;

            //计算分离目标位置
            float sideOffset = Context.IsSpazmatism ? -400f : 400f;
            float vertOffset = Context.IsSpazmatism ? 100f : -100f;
            Vector2 separateTarget = player.Center + new Vector2(sideOffset, vertOffset);

            //移动到分离位置
            MoveTo(npc, separateTarget, 12f, 0.08f);

            //面向玩家
            FaceTarget(npc, player.Center);

            //分离轨迹粒子
            if (!VaultUtils.isServer && phaseTimer % 3 == 0) {
                int dustType = Context.IsSpazmatism ? DustID.SolarFlare : DustID.Vortex;
                Vector2 dustPos = npc.Center + Main.rand.NextVector2Circular(20, 20);
                Dust dust = Dust.NewDustDirect(dustPos, 1, 1, dustType, -npc.velocity.X * 0.2f, -npc.velocity.Y * 0.2f, 100, default, 1.2f);
                dust.noGravity = true;
            }

            //分离音效
            if (phaseTimer == 1) {
                SoundEngine.PlaySound(SoundID.Item8 with { Pitch = 0.3f }, npc.Center);
            }
        }

        /// <summary>
        /// 恢复阶段
        /// </summary>
        private void ExecuteRecoveryPhase(NPC npc, Player player) {
            int phaseTimer = Timer - GatherPhase - ConfrontPhase - ContractPhase - BurstPhase - SeparatePhase;
            float progress = phaseTimer / (float)RecoveryPhase;

            //恢复正常
            npc.scale = 1f;
            npc.velocity *= 0.95f;
            FaceTarget(npc, player.Center);

            //恢复可受伤状态
            if (phaseTimer == 5) {
                npc.dontTakeDamage = false;
            }

            //残余粒子
            if (!VaultUtils.isServer && phaseTimer % 5 == 0) {
                int dustType = Context.IsSpazmatism ? DustID.SolarFlare : DustID.Vortex;
                Vector2 dustPos = npc.Center + Main.rand.NextVector2Circular(25, 25);
                Dust dust = Dust.NewDustDirect(dustPos, 1, 1, dustType, 0, -2, 100, default, 1f);
                dust.noGravity = true;
            }
        }

        /// <summary>
        /// 获取二阶段初始状态
        /// </summary>
        private ITwinsState GetPhase2InitialState() {
            if (Context.IsSpazmatism) {
                return new Spazmatism.SpazmatismFlameChaseState(0);
            }
            else {
                return new Retinazer.RetinazerVerticalBarrageState(0);
            }
        }

        public override void OnExit(TwinsStateContext context) {
            base.OnExit(context);

            //确保退出时恢复正常状态
            context.Npc.scale = 1f;
            context.Npc.dontTakeDamage = false;
            context.IsInPhaseTransition = false;
        }

        private TwinsStateContext context => Context;
    }
}
