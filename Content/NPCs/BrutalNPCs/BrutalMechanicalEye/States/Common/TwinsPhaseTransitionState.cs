using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Common
{
    /// <summary>
    /// 双子魔眼转阶段动画状态
    /// 用于一阶段到二阶段的过渡演出
    /// </summary>
    internal class TwinsPhaseTransitionState : TwinsStateBase
    {
        public override string StateName => "TwinsPhaseTransition";

        /// <summary>
        /// 停顿阶段时长
        /// </summary>
        private const int PausePhase = 30;

        /// <summary>
        /// 收缩阶段时长
        /// </summary>
        private const int ContractPhase = 45;

        /// <summary>
        /// 爆发阶段时长
        /// </summary>
        private const int BurstPhase = 60;

        /// <summary>
        /// 恢复阶段时长
        /// </summary>
        private const int RecoveryPhase = 40;

        /// <summary>
        /// 总时长
        /// </summary>
        private const int TotalDuration = PausePhase + ContractPhase + BurstPhase + RecoveryPhase;

        private TwinsStateContext Context;
        private Vector2 originalPosition;
        private float shakeIntensity;
        private bool hasBurst;

        public override void OnEnter(TwinsStateContext context) {
            base.OnEnter(context);
            Context = context;
            originalPosition = context.Npc.Center;
            shakeIntensity = 0f;
            hasBurst = false;

            //进入转阶段时设置无敌
            context.Npc.dontTakeDamage = true;
            context.Npc.velocity = Vector2.Zero;

            //播放预警音效
            SoundEngine.PlaySound(SoundID.NPCHit4 with { Pitch = -0.5f }, context.Npc.Center);
        }

        public override ITwinsState OnUpdate(TwinsStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            Timer++;

            //阶段1: 停顿，面向玩家
            if (Timer <= PausePhase) {
                ExecutePausePhase(npc, player);
            }
            //阶段2: 收缩蓄力，身体颤抖
            else if (Timer <= PausePhase + ContractPhase) {
                ExecuteContractPhase(npc, player);
            }
            //阶段3: 爆发，释放能量波
            else if (Timer <= PausePhase + ContractPhase + BurstPhase) {
                ExecuteBurstPhase(npc, player);
            }
            //阶段4: 恢复，进入二阶段
            else {
                ExecuteRecoveryPhase(npc);
            }

            //转阶段完成
            if (Timer >= TotalDuration) {
                return GetPhase2InitialState();
            }

            return null;
        }

        /// <summary>
        /// 停顿阶段
        /// </summary>
        private void ExecutePausePhase(NPC npc, Player player) {
            npc.velocity *= 0.9f;
            FaceTarget(npc, player.Center);

            //设置蓄力状态用于绘制特效
            float progress = Timer / (float)PausePhase;
            Context.SetChargeState(5, progress * 0.3f);

            //产生警告粒子
            if (Timer % 5 == 0 && !VaultUtils.isServer) {
                Color dustColor = Context.IsSpazmatism ? Color.OrangeRed : Color.BlueViolet;
                for (int i = 0; i < 2; i++) {
                    int dustType = Context.IsSpazmatism ? DustID.Torch : DustID.PurpleTorch;
                    Vector2 dustPos = npc.Center + Main.rand.NextVector2Circular(npc.width, npc.height);
                    Dust dust = Dust.NewDustDirect(dustPos, 1, 1, dustType, 0, 0, 100, default, 1.2f);
                    dust.noGravity = true;
                    dust.velocity = Vector2.Zero;
                }
            }
        }

        /// <summary>
        /// 收缩蓄力阶段
        /// </summary>
        private void ExecuteContractPhase(NPC npc, Player player) {
            int phaseTimer = Timer - PausePhase;
            float progress = phaseTimer / (float)ContractPhase;

            //逐渐增强的颤抖
            shakeIntensity = progress * 8f;
            Vector2 shake = Main.rand.NextVector2Circular(shakeIntensity, shakeIntensity);
            npc.Center = originalPosition + shake;

            //缩小效果
            npc.scale = 1f - progress * 0.15f;

            //设置蓄力状态
            Context.SetChargeState(5, 0.3f + progress * 0.7f);

            //产生向内聚集的粒子
            if (!VaultUtils.isServer && phaseTimer % 2 == 0) {
                int dustType = Context.IsSpazmatism ? DustID.SolarFlare : DustID.Vortex;
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float dist = 120f - progress * 80f;
                Vector2 dustPos = npc.Center + angle.ToRotationVector2() * dist;
                Dust dust = Dust.NewDustDirect(dustPos, 1, 1, dustType, 0, 0, 100, default, 2f);
                dust.noGravity = true;
                dust.velocity = (npc.Center - dustPos).SafeNormalize(Vector2.Zero) * (6f + progress * 4f);
            }

            //蓄力音效
            if (phaseTimer == 1) {
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.3f }, npc.Center);
            }
        }

        /// <summary>
        /// 爆发阶段
        /// </summary>
        private void ExecuteBurstPhase(NPC npc, Player player) {
            int phaseTimer = Timer - PausePhase - ContractPhase;
            float progress = phaseTimer / (float)BurstPhase;

            //爆发瞬间
            if (!hasBurst) {
                hasBurst = true;

                //播放爆发音效
                SoundEngine.PlaySound(SoundID.Roar, npc.Center);
                SoundEngine.PlaySound(SoundID.Item62, npc.Center);

                //产生爆发粒子
                if (!VaultUtils.isServer) {
                    int dustType = Context.IsSpazmatism ? DustID.SolarFlare : DustID.Vortex;
                    for (int i = 0; i < 50; i++) {
                        float angle = MathHelper.TwoPi / 50f * i;
                        Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(8f, 16f);
                        Dust dust = Dust.NewDustDirect(npc.Center, 1, 1, dustType, vel.X, vel.Y, 100, default, 2.5f);
                        dust.noGravity = true;
                    }

                    //产生环形光波效果的粒子
                    for (int i = 0; i < 30; i++) {
                        float angle = MathHelper.TwoPi / 30f * i;
                        Vector2 vel = angle.ToRotationVector2() * 20f;
                        int glowDust = Context.IsSpazmatism ? DustID.Torch : DustID.PurpleTorch;
                        Dust dust = Dust.NewDustDirect(npc.Center, 1, 1, glowDust, vel.X, vel.Y, 0, default, 3f);
                        dust.noGravity = true;
                        dust.fadeIn = 1.5f;
                    }
                }

                //恢复位置和大小
                npc.Center = originalPosition;
            }

            //逐渐放大到正常
            npc.scale = 0.85f + progress * 0.15f + (float)System.Math.Sin(progress * MathHelper.Pi) * 0.1f;

            //停止蓄力特效
            Context.ResetChargeState();

            //后续的能量波动粒子
            if (!VaultUtils.isServer && phaseTimer % 4 == 0) {
                int dustType = Context.IsSpazmatism ? DustID.SolarFlare : DustID.Vortex;
                float waveRadius = progress * 200f;
                for (int i = 0; i < 8; i++) {
                    float angle = MathHelper.TwoPi / 8f * i + progress * MathHelper.Pi;
                    Vector2 dustPos = npc.Center + angle.ToRotationVector2() * waveRadius;
                    Dust dust = Dust.NewDustDirect(dustPos, 1, 1, dustType, 0, 0, 100, default, 1.5f);
                    dust.noGravity = true;
                    dust.velocity = angle.ToRotationVector2() * 2f;
                }
            }

            //轻微晃动
            float smallShake = (1f - progress) * 3f;
            npc.Center = originalPosition + Main.rand.NextVector2Circular(smallShake, smallShake);
        }

        /// <summary>
        /// 恢复阶段
        /// </summary>
        private void ExecuteRecoveryPhase(NPC npc) {
            int phaseTimer = Timer - PausePhase - ContractPhase - BurstPhase;
            float progress = phaseTimer / (float)RecoveryPhase;

            //恢复正常
            npc.scale = 1f;
            npc.Center = originalPosition;

            //逐渐恢复可受伤状态
            if (phaseTimer == RecoveryPhase - 10) {
                npc.dontTakeDamage = false;
            }

            //残余粒子
            if (!VaultUtils.isServer && phaseTimer % 6 == 0) {
                int dustType = Context.IsSpazmatism ? DustID.SolarFlare : DustID.Vortex;
                Vector2 dustPos = npc.Center + Main.rand.NextVector2Circular(30, 30);
                Dust dust = Dust.NewDustDirect(dustPos, 1, 1, dustType, 0, -2, 100, default, 1.2f);
                dust.noGravity = true;
            }
        }

        /// <summary>
        /// 获取二阶段初始状态
        /// </summary>
        private ITwinsState GetPhase2InitialState() {
            if (Context.IsSpazmatism) {
                return new Spazmatism.SpazmatismFlameChaseState();
            }
            else {
                return new Retinazer.RetinazerVerticalBarrageState();
            }
        }

        public override void OnExit(TwinsStateContext context) {
            base.OnExit(context);

            //确保退出时恢复正常状态
            context.Npc.scale = 1f;
            context.Npc.dontTakeDamage = false;
        }
    }
}
