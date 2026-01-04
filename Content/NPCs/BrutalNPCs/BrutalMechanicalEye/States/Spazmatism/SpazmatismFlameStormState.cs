using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using CalamityOverhaul.Content.Projectiles.Boss.MechanicalEye;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Spazmatism
{
    /// <summary>
    /// 魔焰眼二阶段火焰风暴状态
    /// 在玩家周围制造旋转的火焰风暴
    /// </summary>
    internal class SpazmatismFlameStormState : TwinsStateBase
    {
        public override string StateName => "SpazmatismFlameStorm";

        /// <summary>
        /// 上升阶段
        /// </summary>
        private const int RisePhase = 35;

        /// <summary>
        /// 蓄力阶段
        /// </summary>
        private const int ChargePhase = 45;

        /// <summary>
        /// 风暴阶段
        /// </summary>
        private const int StormPhase = 120;

        /// <summary>
        /// 恢复阶段
        /// </summary>
        private const int RecoveryPhase = 30;

        /// <summary>
        /// 总时长
        /// </summary>
        private const int TotalDuration = RisePhase + ChargePhase + StormPhase + RecoveryPhase;

        private TwinsStateContext Context;
        private Vector2 stormCenter;
        private float stormRotation;
        private float stormRadius;
        private bool hasStartedStorm;

        public override void OnEnter(TwinsStateContext context) {
            base.OnEnter(context);
            Context = context;
            stormRotation = 0f;
            stormRadius = 350f;
            hasStartedStorm = false;
        }

        public override ITwinsState OnUpdate(TwinsStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            Timer++;

            //阶段1: 上升到玩家上方
            if (Timer <= RisePhase) {
                ExecuteRisePhase(npc, player);
            }
            //阶段2: 蓄力
            else if (Timer <= RisePhase + ChargePhase) {
                ExecuteChargePhase(npc, player);
            }
            //阶段3: 火焰风暴
            else if (Timer <= RisePhase + ChargePhase + StormPhase) {
                ExecuteStormPhase(npc, player);
            }
            //阶段4: 恢复
            else {
                ExecuteRecoveryPhase(npc, player);
            }

            //状态结束
            if (Timer >= TotalDuration) {
                return new SpazmatismFlameChaseState();
            }

            return null;
        }

        /// <summary>
        /// 上升阶段
        /// </summary>
        private void ExecuteRisePhase(NPC npc, Player player) {
            float progress = Timer / (float)RisePhase;

            //快速上升到玩家上方
            Vector2 targetPos = player.Center + new Vector2(0, -400);
            MoveTo(npc, targetPos, 18f, 0.12f);
            FaceTarget(npc, player.Center);

            //设置蓄力状态
            context.SetChargeState(9, progress * 0.2f);

            //上升轨迹粒子
            if (!VaultUtils.isServer && Timer % 2 == 0) {
                Dust dust = Dust.NewDustDirect(npc.Center + Main.rand.NextVector2Circular(15, 15), 1, 1, DustID.SolarFlare, 0, 3, 100, default, 1.3f);
                dust.noGravity = true;
            }
        }

        /// <summary>
        /// 蓄力阶段
        /// </summary>
        private void ExecuteChargePhase(NPC npc, Player player) {
            int phaseTimer = Timer - RisePhase;
            float progress = phaseTimer / (float)ChargePhase;

            //记录风暴中心
            stormCenter = player.Center;

            //悬停
            npc.velocity *= 0.9f;
            FaceTarget(npc, player.Center);

            //设置蓄力状态
            context.SetChargeState(9, 0.2f + progress * 0.8f);

            //蓄力特效
            if (!VaultUtils.isServer) {
                //环绕能量聚集
                if (phaseTimer % 2 == 0) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    float dist = 100f - progress * 60f;
                    Vector2 dustPos = npc.Center + angle.ToRotationVector2() * dist;
                    Dust dust = Dust.NewDustDirect(dustPos, 1, 1, DustID.SolarFlare, 0, 0, 100, default, 1.6f + progress);
                    dust.noGravity = true;
                    dust.velocity = (npc.Center - dustPos).SafeNormalize(Vector2.Zero) * (5f + progress * 3f);
                }

                //预警圆环
                if (phaseTimer % 4 == 0 && progress > 0.3f) {
                    int ringPoints = 16;
                    float ringRadius = stormRadius * (progress - 0.3f) / 0.7f;
                    for (int i = 0; i < ringPoints; i++) {
                        float angle = MathHelper.TwoPi / ringPoints * i;
                        Vector2 ringPos = stormCenter + angle.ToRotationVector2() * ringRadius;
                        Dust dust = Dust.NewDustDirect(ringPos, 1, 1, DustID.Torch, 0, 0, 150, default, 1f);
                        dust.noGravity = true;
                        dust.velocity = Vector2.Zero;
                    }
                }

                //蓄力音效
                if (phaseTimer == 1) {
                    SoundEngine.PlaySound(SoundID.Item45 with { Pitch = -0.4f, Volume = 0.8f }, npc.Center);
                }

                //蓄力完成
                if (phaseTimer == ChargePhase - 3) {
                    SoundEngine.PlaySound(SoundID.Item62 with { Pitch = 0.2f }, npc.Center);
                }
            }
        }

        /// <summary>
        /// 风暴阶段
        /// </summary>
        private void ExecuteStormPhase(NPC npc, Player player) {
            int phaseTimer = Timer - RisePhase - ChargePhase;
            float progress = phaseTimer / (float)StormPhase;

            //停止蓄力特效
            context.ResetChargeState();

            //风暴开始音效
            if (!hasStartedStorm) {
                hasStartedStorm = true;
                SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.2f, Volume = 1.3f }, npc.Center);
            }

            //更新风暴中心跟随玩家
            stormCenter = Vector2.Lerp(stormCenter, player.Center, 0.02f);

            //本体绕着风暴中心旋转
            float rotSpeed = Context.IsMachineRebellion ? 0.08f : 0.06f;
            stormRotation += rotSpeed;
            Vector2 orbitPos = stormCenter + stormRotation.ToRotationVector2() * stormRadius;
            npc.Center = Vector2.Lerp(npc.Center, orbitPos, 0.15f);

            //面向运动方向
            Vector2 tangent = (stormRotation + MathHelper.PiOver2).ToRotationVector2();
            npc.rotation = tangent.ToRotation() - MathHelper.PiOver2;

            //发射火球
            int fireRate = Context.IsMachineRebellion ? 6 : 10;
            if (phaseTimer % fireRate == 0 && !VaultUtils.isClient) {
                //向中心发射
                Vector2 toCenterDir = (stormCenter - npc.Center).SafeNormalize(Vector2.Zero);
                Projectile.NewProjectile(
                    npc.GetSource_FromAI(),
                    npc.Center,
                    toCenterDir * 10f,
                    ModContent.ProjectileType<Fireball>(),
                    32,
                    0f,
                    Main.myPlayer
                );

                //沿切线方向也发射
                if (phaseTimer % (fireRate * 2) == 0) {
                    Projectile.NewProjectile(
                        npc.GetSource_FromAI(),
                        npc.Center,
                        tangent * 8f,
                        ModContent.ProjectileType<Fireball>(),
                        28,
                        0f,
                        Main.myPlayer
                    );
                }
            }

            //风暴粒子特效
            if (!VaultUtils.isServer) {
                //旋转火焰墙
                if (phaseTimer % 2 == 0) {
                    int wallPoints = 12;
                    for (int i = 0; i < wallPoints; i++) {
                        float angle = MathHelper.TwoPi / wallPoints * i + stormRotation * 0.5f;
                        float radius = stormRadius * (0.3f + Main.rand.NextFloat(0.7f));
                        Vector2 dustPos = stormCenter + angle.ToRotationVector2() * radius;

                        //旋转方向的速度
                        Vector2 rotVel = (angle + MathHelper.PiOver2).ToRotationVector2() * 3f;
                        Dust dust = Dust.NewDustDirect(dustPos, 1, 1, DustID.SolarFlare, rotVel.X, rotVel.Y, 100, default, 1.4f);
                        dust.noGravity = true;
                    }
                }

                //本体火焰轨迹
                for (int i = 0; i < 2; i++) {
                    Dust dust = Dust.NewDustDirect(npc.Center + Main.rand.NextVector2Circular(20, 20), 1, 1, DustID.SolarFlare, -tangent.X * 2, -tangent.Y * 2, 100, default, 1.6f);
                    dust.noGravity = true;
                }
            }

            //风暴减弱
            if (progress > 0.8f) {
                float fadeProgress = (progress - 0.8f) / 0.2f;
                stormRadius = 350f - fadeProgress * 100f;
            }
        }

        /// <summary>
        /// 恢复阶段
        /// </summary>
        private void ExecuteRecoveryPhase(NPC npc, Player player) {
            //逐渐恢复
            npc.velocity *= 0.9f;
            FaceTarget(npc, player.Center);

            //残余火焰
            if (!VaultUtils.isServer && Timer % 5 == 0) {
                Dust dust = Dust.NewDustDirect(npc.Center + Main.rand.NextVector2Circular(25, 25), 1, 1, DustID.SolarFlare, 0, -2, 100, default, 0.8f);
                dust.noGravity = true;
            }
        }

        private TwinsStateContext context => Context;
    }
}
