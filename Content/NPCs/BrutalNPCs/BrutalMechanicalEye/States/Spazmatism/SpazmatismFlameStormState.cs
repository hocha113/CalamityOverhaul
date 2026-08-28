using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Spazmatism
{
    /// <summary>火焰风暴，上升→预警→蓄力→旋转火弹风暴</summary>
    [InnoVault.StateMachines.VaultState((int)TwinsStateIndex.SpazmatismFlameStorm, typeof(TwinsStateContext))]
    internal class SpazmatismFlameStormState : TwinsStateBase
    {
        public override string StateName => "SpazmatismFlameStorm";
        public override TwinsStateIndex StateIndex => TwinsStateIndex.SpazmatismFlameStorm;

        private int RisePhase => Context.IsAsuraMode ? 35 : 45;

        private int WarningPhase => Context.IsAsuraMode ? 40 : 50;

        private int ChargePhase => Context.IsAsuraMode ? 45 : 55;

        private int StormPhase => Context.IsAsuraMode ? 130 : 120;

        private int RecoveryPhase => Context.IsAsuraMode ? 25 : 30;

        private int TotalDuration => RisePhase + WarningPhase + ChargePhase + StormPhase + RecoveryPhase;

        private float RotSpeed => Context.IsAsuraMode ? 0.075f : 0.06f;

        private int FireRate => Context.IsAsuraMode ? 8 : 10;

        private TwinsStateContext Context;
        private Vector2 stormCenter;
        private float stormRotation;
        private float stormRadius;
        private bool hasStartedStorm;
        private bool hasPlayedWarningSound;
        private int comboStep;

        public SpazmatismFlameStormState() : this(0) {
        }

        public SpazmatismFlameStormState(int currentComboStep) {
            comboStep = currentComboStep;
        }

        public override void OnEnter(TwinsStateContext context) {
            base.OnEnter(context);
            Context = context;
            stormRotation = 0f;
            stormRadius = 350f;
            hasStartedStorm = false;
            hasPlayedWarningSound = false;
        }

        public override ITwinsState OnUpdate(TwinsStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            Timer++;

            if (Timer <= RisePhase) {
                ExecuteRisePhase(npc, player);
            }
            else if (Timer <= RisePhase + WarningPhase) {
                ExecuteWarningPhase(npc, player);
            }
            else if (Timer <= RisePhase + WarningPhase + ChargePhase) {
                ExecuteChargePhase(npc, player);
            }
            else if (Timer <= RisePhase + WarningPhase + ChargePhase + StormPhase) {
                ExecuteStormPhase(npc, player);
            }
            else {
                ExecuteRecoveryPhase(npc, player);
            }

            if (Timer >= TotalDuration) {
                if (context.IsSoloRageMode) {
                    return new SpazmatismSoloRageState();
                }
                return new SpazmatismFlameChaseState(comboStep);
            }

            return null;
        }

        private void ExecuteRisePhase(NPC npc, Player player) {
            float progress = Timer / (float)RisePhase;

            Vector2 targetPos = player.Center + new Vector2(0, -400);
            MoveTo(npc, targetPos, 16f, 0.1f);
            FaceTarget(npc, player.Center);

            context.SetChargeState(9, progress * 0.15f);

            if (!VaultUtils.isServer && Timer % 2 == 0) {
                Dust dust = Dust.NewDustDirect(npc.Center + Main.rand.NextVector2Circular(15, 15), 1, 1, DustID.SolarFlare, 0, 3, 100, default, 1.3f);
                dust.noGravity = true;
            }
        }

        /// <summary>预警，标风暴范围</summary>
        private void ExecuteWarningPhase(NPC npc, Player player) {
            int phaseTimer = Timer - RisePhase;
            float progress = phaseTimer / (float)WarningPhase;

            //锁定风暴中心位置
            if (phaseTimer == 1) {
                stormCenter = player.Center;
            }

            Vector2 targetPos = stormCenter + new Vector2(0, -400);
            npc.Center = Vector2.Lerp(npc.Center, targetPos, 0.08f);
            npc.velocity *= 0.9f;
            FaceTarget(npc, stormCenter);

            context.SetChargeState(9, 0.15f + progress * 0.25f);

            if (!hasPlayedWarningSound) {
                hasPlayedWarningSound = true;
                SoundEngine.PlaySound(SoundID.Item20 with { Pitch = -0.5f, Volume = 1.0f }, npc.Center);
            }

            //显示完整的预警圆环
            if (!VaultUtils.isServer) {
                int ringPoints = 24;
                float displayRadius = stormRadius * progress;

                if (phaseTimer % 3 == 0) {
                    for (int i = 0; i < ringPoints; i++) {
                        float angle = MathHelper.TwoPi / ringPoints * i + phaseTimer * 0.02f;
                        Vector2 ringPos = stormCenter + angle.ToRotationVector2() * displayRadius;

                        Dust dust = Dust.NewDustDirect(ringPos, 1, 1, DustID.Torch, 0, 0, 100, default, 1.2f + progress * 0.5f);
                        dust.noGravity = true;
                        dust.velocity = angle.ToRotationVector2() * 0.5f;
                    }
                }

                if (progress > 0.5f && phaseTimer % 4 == 0) {
                    for (int i = 0; i < ringPoints; i++) {
                        float angle = MathHelper.TwoPi / ringPoints * i;
                        Vector2 ringPos = stormCenter + angle.ToRotationVector2() * stormRadius;

                        Dust dust = Dust.NewDustDirect(ringPos, 1, 1, DustID.SolarFlare, 0, 0, 150, default, 1.5f);
                        dust.noGravity = true;
                        dust.velocity = Vector2.Zero;
                    }
                }

                if (phaseTimer % 5 == 0) {
                    Dust centerDust = Dust.NewDustDirect(stormCenter + Main.rand.NextVector2Circular(20, 20), 1, 1, DustID.Torch, 0, 0, 100, default, 2f);
                    centerDust.noGravity = true;
                    centerDust.velocity = Vector2.Zero;
                }

                if (progress > 0.7f && phaseTimer % 8 < 4) {
                    for (int i = 0; i < 8; i++) {
                        float angle = MathHelper.TwoPi / 8 * i;
                        Vector2 flashPos = stormCenter + angle.ToRotationVector2() * stormRadius;
                        Dust dust = Dust.NewDustDirect(flashPos, 1, 1, DustID.Torch, 0, 0, 0, default, 2.5f);
                        dust.noGravity = true;
                        dust.velocity = Vector2.Zero;
                    }
                }
            }
        }

        private void ExecuteChargePhase(NPC npc, Player player) {
            int phaseTimer = Timer - RisePhase - WarningPhase;
            float progress = phaseTimer / (float)ChargePhase;

            npc.velocity *= 0.9f;
            FaceTarget(npc, stormCenter);

            context.SetChargeState(9, 0.4f + progress * 0.6f);

            if (!VaultUtils.isServer) {
                if (phaseTimer % 2 == 0) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    float dist = 100f - progress * 60f;
                    Vector2 dustPos = npc.Center + angle.ToRotationVector2() * dist;
                    Dust dust = Dust.NewDustDirect(dustPos, 1, 1, DustID.SolarFlare, 0, 0, 100, default, 1.6f + progress);
                    dust.noGravity = true;
                    dust.velocity = (npc.Center - dustPos).SafeNormalize(Vector2.Zero) * (5f + progress * 3f);
                }

                if (phaseTimer % 3 == 0) {
                    int ringPoints = 20;
                    for (int i = 0; i < ringPoints; i++) {
                        float angle = MathHelper.TwoPi / ringPoints * i + phaseTimer * 0.03f;
                        Vector2 ringPos = stormCenter + angle.ToRotationVector2() * stormRadius;
                        Dust dust = Dust.NewDustDirect(ringPos, 1, 1, DustID.SolarFlare, 0, 0, 100, default, 1.3f);
                        dust.noGravity = true;
                        dust.velocity = (angle + MathHelper.PiOver2).ToRotationVector2() * 2f;
                    }
                }

                if (phaseTimer == 1) {
                    SoundEngine.PlaySound(SoundID.Item45 with { Pitch = -0.4f, Volume = 0.8f }, npc.Center);
                }

                if (phaseTimer == ChargePhase - 3) {
                    SoundEngine.PlaySound(SoundID.Item62 with { Pitch = 0.2f }, npc.Center);
                }
            }
        }

        private void ExecuteStormPhase(NPC npc, Player player) {
            int phaseTimer = Timer - RisePhase - WarningPhase - ChargePhase;
            float progress = phaseTimer / (float)StormPhase;

            context.ResetChargeState();

            //风暴音爆入轨
            if (!hasStartedStorm) {
                hasStartedStorm = true;
                SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.2f, Volume = 1.3f }, npc.Center);
                Vector2 launchTangent = (stormRotation + MathHelper.PiOver2).ToRotationVector2();
                TwinsMotion.SonicBoom(npc.Center, launchTangent, spazTheme: true, strength: 1.1f);
            }

            //stormCenter = Vector2.Lerp(stormCenter, player.Center, 0.015f);

            stormRotation += RotSpeed;
            Vector2 orbitPos = stormCenter + stormRotation.ToRotationVector2() * stormRadius;
            npc.Center = Vector2.Lerp(npc.Center, orbitPos, 0.15f);
            Context.PushDashVisuals(0.45f, 0.6f);

            Vector2 tangent = (stormRotation + MathHelper.PiOver2).ToRotationVector2();
            npc.rotation = tangent.ToRotation() - MathHelper.PiOver2;

            //发射火球
            if (phaseTimer % FireRate == 0 && !VaultUtils.isClient) {
                Vector2 toCenterDir = (stormCenter - npc.Center).SafeNormalize(Vector2.Zero);
                Projectile.NewProjectile(
                    npc.GetSource_FromAI(),
                    npc.Center,
                    toCenterDir * 10f,
                    ModContent.ProjectileType<Fireball>(),
                    24,
                    0f,
                    Main.myPlayer
                );

                if (phaseTimer % (FireRate * 2) == 0) {
                    Projectile.NewProjectile(
                        npc.GetSource_FromAI(),
                        npc.Center,
                        tangent * 8f,
                        ModContent.ProjectileType<Fireball>(),
                        20,
                        0f,
                        Main.myPlayer
                    );
                }
            }

            if (!VaultUtils.isServer) {
                if (phaseTimer % 2 == 0) {
                    int wallPoints = 12;
                    for (int i = 0; i < wallPoints; i++) {
                        float angle = MathHelper.TwoPi / wallPoints * i + stormRotation * 0.5f;
                        float radius = stormRadius * (0.3f + Main.rand.NextFloat(0.7f));
                        Vector2 dustPos = stormCenter + angle.ToRotationVector2() * radius;

                        Vector2 rotVel = (angle + MathHelper.PiOver2).ToRotationVector2() * 3f;
                        Dust dust = Dust.NewDustDirect(dustPos, 1, 1, DustID.SolarFlare, rotVel.X, rotVel.Y, 100, default, 1.4f);
                        dust.noGravity = true;
                    }
                }

                for (int i = 0; i < 2; i++) {
                    Dust dust = Dust.NewDustDirect(npc.Center + Main.rand.NextVector2Circular(20, 20), 1, 1, DustID.SolarFlare, -tangent.X * 2, -tangent.Y * 2, 100, default, 1.6f);
                    dust.noGravity = true;
                }
            }

            if (progress > 0.8f) {
                float fadeProgress = (progress - 0.8f) / 0.2f;
                stormRadius = 350f - fadeProgress * 100f;
            }
        }

        private void ExecuteRecoveryPhase(NPC npc, Player player) {
            npc.velocity *= 0.9f;
            FaceTarget(npc, player.Center);

            if (!VaultUtils.isServer && Timer % 5 == 0) {
                Dust dust = Dust.NewDustDirect(npc.Center + Main.rand.NextVector2Circular(25, 25), 1, 1, DustID.SolarFlare, 0, -2, 100, default, 0.8f);
                dust.noGravity = true;
            }
        }

        private TwinsStateContext context => Context;
    }
}
