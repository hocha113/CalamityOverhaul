using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Common
{
    /// <summary>同步转阶段，汇聚+换形</summary>
    [InnoVault.StateMachines.VaultState((int)TwinsStateIndex.TwinsPhaseTransition, typeof(TwinsStateContext))]
    internal class TwinsPhaseTransitionState : TwinsStateBase
    {
        public override string StateName => "TwinsPhaseTransition";
        public override TwinsStateIndex StateIndex => TwinsStateIndex.TwinsPhaseTransition;

        /// <summary>集合</summary>
        private const int GatherPhase = 60;

        /// <summary>对峙</summary>
        private const int ConfrontPhase = 40;

        /// <summary>收缩蓄力</summary>
        private const int ContractPhase = 50;

        /// <summary>爆发</summary>
        private const int BurstPhase = 45;

        /// <summary>分离</summary>
        private const int SeparatePhase = 50;

        /// <summary>恢复</summary>
        private const int RecoveryPhase = 30;

        private const int TotalDuration = GatherPhase + ConfrontPhase + ContractPhase + BurstPhase + SeparatePhase + RecoveryPhase;

        private TwinsStateContext Context;
        private Vector2 gatherPoint;
        private Vector2 originalPosition;
        private float shakeIntensity;
        private bool hasBurst;
        private bool hasPlayedGatherSound;
        private NPC partnerNpc;

        public TwinsPhaseTransitionState() {
        }

        public override void OnEnter(TwinsStateContext context) {
            base.OnEnter(context);
            Context = context;
            context.IsInPhaseTransition = true;
            originalPosition = context.Npc.Center;
            shakeIntensity = 0f;
            hasBurst = false;
            hasPlayedGatherSound = false;

            partnerNpc = TwinsStateContext.GetPartnerNpc(context.Npc.type);

            context.Npc.dontTakeDamage = true;
        }

        public override ITwinsState OnUpdate(TwinsStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            Timer++;

            gatherPoint = player.Center + new Vector2(0, -350);

            if (Timer <= GatherPhase) {
                ExecuteGatherPhase(npc, player);
            }
            else if (Timer <= GatherPhase + ConfrontPhase) {
                ExecuteConfrontPhase(npc, player);
            }
            else if (Timer <= GatherPhase + ConfrontPhase + ContractPhase) {
                ExecuteContractPhase(npc, player);
            }
            else if (Timer <= GatherPhase + ConfrontPhase + ContractPhase + BurstPhase) {
                ExecuteBurstPhase(npc, player);
            }
            else if (Timer <= GatherPhase + ConfrontPhase + ContractPhase + BurstPhase + SeparatePhase) {
                ExecuteSeparatePhase(npc, player);
            }
            else {
                ExecuteRecoveryPhase(npc, player);
            }

            if (Timer >= TotalDuration) {
                context.IsInPhaseTransition = false;
                return GetPhase2InitialState();
            }

            return null;
        }

        /// <summary>集合移动</summary>
        private void ExecuteGatherPhase(NPC npc, Player player) {
            float progress = Timer / (float)GatherPhase;

            float sideOffset = Context.IsSpazmatism ? -120f : 120f;
            Vector2 targetPos = gatherPoint + new Vector2(sideOffset, 0);

            float speed = 20f - progress * 10f;
            MoveTo(npc, targetPos, speed, 0.12f);

            FaceTarget(npc, player.Center);

            npc.position += player.velocity * 0.5f;

            if (!hasPlayedGatherSound && Timer == 1) {
                hasPlayedGatherSound = true;
                SoundEngine.PlaySound(SoundID.Item103 with { Pitch = -0.3f, Volume = 0.8f }, npc.Center);
            }

            if (!VaultUtils.isServer && Timer % 2 == 0) {
                int dustType = Context.IsSpazmatism ? DustID.SolarFlare : DustID.Vortex;
                Vector2 dustPos = npc.Center + Main.rand.NextVector2Circular(15, 15);
                Dust dust = Dust.NewDustDirect(dustPos, 1, 1, dustType, -npc.velocity.X * 0.3f, -npc.velocity.Y * 0.3f, 100, default, 1.3f);
                dust.noGravity = true;
            }

            Context.SetChargeState(11, progress * 0.2f);
        }

        /// <summary>对峙</summary>
        private void ExecuteConfrontPhase(NPC npc, Player player) {
            int phaseTimer = Timer - GatherPhase;
            float progress = phaseTimer / (float)ConfrontPhase;

            float sideOffset = Context.IsSpazmatism ? -120f : 120f;
            Vector2 targetPos = gatherPoint + new Vector2(sideOffset, 0);
            npc.Center = Vector2.Lerp(npc.Center, targetPos, 0.1f);
            npc.velocity *= 0.9f;

            npc.position += player.velocity;

            if (partnerNpc != null && partnerNpc.active) {
                FaceTarget(npc, partnerNpc.Center);
            }
            else {
                FaceTarget(npc, player.Center);
            }

            //对峙能量连线
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

            if (!VaultUtils.isServer && phaseTimer % 4 == 0) {
                int dustType = Context.IsSpazmatism ? DustID.Torch : DustID.PurpleTorch;
                Vector2 dustPos = npc.Center + Main.rand.NextVector2Circular(npc.width, npc.height);
                Dust dust = Dust.NewDustDirect(dustPos, 1, 1, dustType, 0, 0, 100, default, 1.3f);
                dust.noGravity = true;
                dust.velocity = Vector2.Zero;
            }

            if (phaseTimer == 1) {
                SoundEngine.PlaySound(SoundID.NPCHit4 with { Pitch = -0.5f }, npc.Center);
            }

            Context.SetChargeState(11, 0.2f + progress * 0.2f);
        }

        /// <summary>收缩蓄力</summary>
        private void ExecuteContractPhase(NPC npc, Player player) {
            int phaseTimer = Timer - GatherPhase - ConfrontPhase;
            float progress = phaseTimer / (float)ContractPhase;

            if (phaseTimer == 1) {
                originalPosition = npc.Center;
            }

            originalPosition += player.velocity;

            shakeIntensity = progress * 10f;
            Vector2 shake = Main.rand.NextVector2Circular(shakeIntensity, shakeIntensity);
            npc.Center = originalPosition + shake;
            npc.velocity = Vector2.Zero;

            npc.scale = 1f - progress * 0.2f;

            if (partnerNpc != null && partnerNpc.active) {
                FaceTarget(npc, partnerNpc.Center);
            }

            Context.SetChargeState(11, 0.4f + progress * 0.6f);

            if (!VaultUtils.isServer && phaseTimer % 2 == 0) {
                int dustType = Context.IsSpazmatism ? DustID.SolarFlare : DustID.Vortex;
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float dist = 100f - progress * 70f;
                Vector2 dustPos = npc.Center + angle.ToRotationVector2() * dist;
                Dust dust = Dust.NewDustDirect(dustPos, 1, 1, dustType, 0, 0, 100, default, 1.8f + progress);
                dust.noGravity = true;
                dust.velocity = (npc.Center - dustPos).SafeNormalize(Vector2.Zero) * (6f + progress * 5f);
            }

            //双眼高压电弧
            if (!VaultUtils.isServer && phaseTimer % 2 == 0 && partnerNpc != null && partnerNpc.active) {
                float t = Main.rand.NextFloat();
                Vector2 linePos = Vector2.Lerp(npc.Center, partnerNpc.Center, t);
                Vector2 flowDir = (partnerNpc.Center - npc.Center).SafeNormalize(Vector2.Zero);
                if (!Context.IsSpazmatism) {
                    flowDir = -flowDir;
                }
                //连线能量束
                Vector2 perp = flowDir.RotatedBy(MathHelper.PiOver2) * (float)Math.Sin(t * 14f + Main.GlobalTimeWrappedHourly * 10f) * 9f;
                PRTLoader.NewParticle<PRT_TwinsSpark>(linePos + perp, flowDir * 6f,
                    Color.White, Main.rand.NextFloat(1f, 1.6f) * (0.7f + progress * 0.5f))?
                    .Configure(12, Context.IsSpazmatism ? 1 : 0);
            }

            if (phaseTimer % 16 == 0 && !VaultUtils.isServer) {
                TwinsMotion.Shake(npc.Center, 1.5f + progress * 3f, 8);
            }

            if (phaseTimer == 1) {
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.2f, Volume = 0.9f }, npc.Center);
            }
        }

        /// <summary>爆发</summary>
        private void ExecuteBurstPhase(NPC npc, Player player) {
            int phaseTimer = Timer - GatherPhase - ConfrontPhase - ContractPhase;
            float progress = phaseTimer / (float)BurstPhase;

            originalPosition += player.velocity;

            //爆发，咆哮+殉爆+冲击环+强震
            if (!hasBurst) {
                hasBurst = true;

                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.1f, Volume = 1.3f }, npc.Center);
                SoundEngine.PlaySound(SoundID.Item62 with { Volume = 1.2f }, npc.Center);

                //扭曲环，魔焰侧生成
                if (Context.IsSpazmatism && !VaultUtils.isClient) {
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                        ModContent.ProjectileType<TwinsSupernovaBlast>(), 0, 0f, Main.myPlayer, 1f, 2f);
                }

                if (!VaultUtils.isServer) {
                    Color themeColor = Context.IsSpazmatism ? TwinsMotion.SpazColor : TwinsMotion.RetinColor;

                    PRTLoader.NewParticle<PRT_MechExplosion>(npc.Center, Vector2.Zero, themeColor, 1.8f)?
                        .Configure(30, themeColor);

                    PRTLoader.NewParticle<PRT_DWave>(npc.Center, Vector2.Zero, themeColor, 0.25f)?
                        .Configure(Vector2.One, Main.rand.NextFloat(MathHelper.TwoPi), 1.7f, 22);
                    PRTLoader.NewParticle<PRT_DWave>(npc.Center, Vector2.Zero, Color.White * 0.8f, 0.15f)?
                        .Configure(Vector2.One, Main.rand.NextFloat(MathHelper.TwoPi), 1f, 16);

                    for (int i = 0; i < 18; i++) {
                        PRTLoader.NewParticle<PRT_TwinsSpark>(npc.Center, VaultUtils.RandVr(7, 16),
                            Color.White, Main.rand.NextFloat(1.3f, 2.2f))?.Configure(22, Context.IsSpazmatism ? 1 : 0);
                    }

                    int dustType = Context.IsSpazmatism ? DustID.SolarFlare : DustID.Vortex;
                    for (int i = 0; i < 40; i++) {
                        float angle = MathHelper.TwoPi / 40f * i;
                        Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(10f, 20f);
                        Dust dust = Dust.NewDustDirect(npc.Center, 1, 1, dustType, vel.X, vel.Y, 100, default, 2.5f);
                        dust.noGravity = true;
                    }

                    //强震，仅魔焰侧一次
                    if (Context.IsSpazmatism) {
                        TwinsMotion.Shake(npc.Center, 11f, 24);
                    }
                }

                npc.Center = originalPosition;
            }

            float bounce = (float)Math.Sin(progress * MathHelper.Pi * 2f) * 0.1f * (1f - progress);
            npc.scale = 0.8f + progress * 0.2f + bounce;

            Context.ResetChargeState();

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

            float smallShake = (1f - progress) * 4f;
            npc.Center = originalPosition + Main.rand.NextVector2Circular(smallShake, smallShake);
        }

        /// <summary>分离</summary>
        private void ExecuteSeparatePhase(NPC npc, Player player) {
            int phaseTimer = Timer - GatherPhase - ConfrontPhase - ContractPhase - BurstPhase;
            float progress = phaseTimer / (float)SeparatePhase;

            npc.scale = 1f;

            float sideOffset = Context.IsSpazmatism ? -400f : 400f;
            float vertOffset = Context.IsSpazmatism ? 100f : -100f;
            Vector2 separateTarget = player.Center + new Vector2(sideOffset, vertOffset);

            MoveTo(npc, separateTarget, 12f, 0.08f);

            FaceTarget(npc, player.Center);

            if (!VaultUtils.isServer && phaseTimer % 3 == 0) {
                int dustType = Context.IsSpazmatism ? DustID.SolarFlare : DustID.Vortex;
                Vector2 dustPos = npc.Center + Main.rand.NextVector2Circular(20, 20);
                Dust dust = Dust.NewDustDirect(dustPos, 1, 1, dustType, -npc.velocity.X * 0.2f, -npc.velocity.Y * 0.2f, 100, default, 1.2f);
                dust.noGravity = true;
            }

            if (phaseTimer == 1) {
                SoundEngine.PlaySound(SoundID.Item8 with { Pitch = 0.3f }, npc.Center);
            }
        }

        /// <summary>恢复</summary>
        private void ExecuteRecoveryPhase(NPC npc, Player player) {
            int phaseTimer = Timer - GatherPhase - ConfrontPhase - ContractPhase - BurstPhase - SeparatePhase;
            float progress = phaseTimer / (float)RecoveryPhase;

            npc.scale = 1f;
            npc.velocity *= 0.95f;
            FaceTarget(npc, player.Center);

            if (phaseTimer == 5) {
                npc.dontTakeDamage = false;
            }

            if (!VaultUtils.isServer && phaseTimer % 5 == 0) {
                int dustType = Context.IsSpazmatism ? DustID.SolarFlare : DustID.Vortex;
                Vector2 dustPos = npc.Center + Main.rand.NextVector2Circular(25, 25);
                Dust dust = Dust.NewDustDirect(dustPos, 1, 1, dustType, 0, -2, 100, default, 1f);
                dust.noGravity = true;
            }
        }

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

            //退出恢复 scale/无敌/标记
            context.Npc.scale = 1f;
            context.Npc.dontTakeDamage = false;
            context.IsInPhaseTransition = false;
        }

        private TwinsStateContext context => Context;
    }
}
