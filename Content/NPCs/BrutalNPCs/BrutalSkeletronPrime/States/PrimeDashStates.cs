using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core;
using CalamityOverhaul.Content.Projectiles.Boss.SkeletronPrime;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.States
{
    /// <summary>
    /// 旋转冲撞：late-snap 后仰蓄势 → 单帧设速瞬发 → 硬刹收势，2~3 段连冲。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)PrimeStateIndex.SpinDash, typeof(PrimeStateContext))]
    internal class PrimeSpinDashState : PrimeStateBase
    {
        public override string StateName => "SpinDash";
        public override PrimeStateIndex StateIndex => PrimeStateIndex.SpinDash;

        private const int DashActive = 10;
        private const int DriftFrames = 12;

        private int cyclePhase;
        private int phaseTimer;

        public override void OnEnter(PrimeStateContext context) {
            base.OnEnter(context);
            cyclePhase = 0;
            phaseTimer = 0;
        }

        public override IPrimeState OnUpdate(PrimeStateContext context) {
            NPC npc = context.Npc;
            context.FrameMode = 1;

            switch (cyclePhase) {
                case 0: UpdateTelegraph(context); break;
                case 1: UpdateDash(context); break;
                default: UpdateDrift(context); break;
            }

            phaseTimer++;
            Timer++;

            int maxDashes = 3 + (context.DeathMode ? 1 : 0) + (context.BossRush ? 1 : 0);
            if (Counter >= maxDashes && cyclePhase != 1 && !VaultUtils.isClient) {
                npc.damage = npc.defDamage;
                npc.defense = npc.defDefense;
                return new PrimeCommandSequenceState();
            }
            return null;
        }

        private void UpdateTelegraph(PrimeStateContext context) {
            NPC npc = context.Npc;
            npc.damage = 0;
            Vector2 aim = (context.Target.Center + context.Target.velocity * 8f - npc.Center).SafeNormalize(Vector2.UnitY);
            context.DashDirection = aim;

            float t = phaseTimer / (float)PrimeDirector.DashTelegraphFrames;
            float windup = (float)System.Math.Pow(t, 8);
            context.SetChargeState(1, windup);
            npc.velocity = Vector2.Lerp(npc.velocity, -aim * (4f + windup * 6f), 0.14f);

            if (!VaultUtils.isClient && phaseTimer == 1) {
                PrimeTelegraphLine.SpawnLine(npc, npc.Center, aim.ToRotation(), PrimeDirector.DashTelegraphFrames);
            }
            if (phaseTimer == 4 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.ForceRoar with { Volume = Counter == 0 ? 1f : 0.65f }, npc.Center);
            }
            if (phaseTimer >= PrimeDirector.DashTelegraphFrames) {
                LaunchDash(context, aim);
            }
        }

        private void LaunchDash(PrimeStateContext context, Vector2 aim) {
            NPC npc = context.Npc;
            cyclePhase = 1;
            phaseTimer = 0;
            context.ResetChargeState();
            float speed = Main.masterMode ? 17f : 14.5f;
            if (context.DeathMode) speed += 2f;
            if (context.BossRush) speed *= 1.3f;
            npc.velocity = aim * speed;
            if (!VaultUtils.isServer) {
                PrimeScreenEffects.PushHeatWake(npc.Center, npc.velocity.ToRotation(), 1f);
                SoundEngine.PlaySound("CalamityMod/Sounds/Custom/ExoMechs/AresEnraged".GetSound() with { Pitch = 1.18f, Volume = 0.75f }, npc.Center);
            }
        }

        private void UpdateDash(PrimeStateContext context) {
            NPC npc = context.Npc;
            float speed = npc.velocity.Length();
            npc.damage = speed > PrimeDirector.DashContactSpeedThreshold ? npc.defDamage * 2 : 0;
            npc.defense = (int)(npc.defDefense * 1.25f);
            SpinRotation(npc, 0.34f);
            npc.velocity *= 0.65f;

            if (!VaultUtils.isServer) {
                PrimeScreenEffects.PushHeatWake(npc.Center, npc.velocity.ToRotation(),
                    MathHelper.Clamp(speed / 20f, 0.3f, 1f));
            }

            if (phaseTimer >= DashActive) {
                cyclePhase = 2;
                phaseTimer = 0;
                Counter++;
            }
        }

        private void UpdateDrift(PrimeStateContext context) {
            NPC npc = context.Npc;
            npc.damage = 0;
            npc.velocity *= 0.82f;
            if (phaseTimer >= DriftFrames) {
                cyclePhase = 0;
                phaseTimer = 0;
            }
        }
    }

    /// <summary>
    /// 狂暴闪现贯穿：预警 → 闪现至远侧 → 直线贯穿 → 越界再闪现，三连无回程死时间。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)PrimeStateIndex.RageDash, typeof(PrimeStateContext))]
    internal class PrimeRageDashState : PrimeStateBase
    {
        public override string StateName => "RageDash";
        public override PrimeStateIndex StateIndex => PrimeStateIndex.RageDash;

        private int phase;
        private int phaseTimer;
        private Vector2 dashDir;
        private Vector2 flashFrom;

        public override void OnEnter(PrimeStateContext context) {
            base.OnEnter(context);
            phase = 0;
            phaseTimer = 0;
        }

        public override IPrimeState OnUpdate(PrimeStateContext context) {
            NPC npc = context.Npc;
            context.FrameMode = 2;

            switch (phase) {
                case 0: Telegraph(context); break;
                case 1: FlashReposition(context); break;
                default: LineDash(context); break;
            }

            phaseTimer++;
            Timer++;

            int maxHits = 3 + (context.DeathMode ? 1 : 0);
            if (Counter >= maxHits && phase == 2 && phaseTimer > 6 && !VaultUtils.isClient) {
                return new PrimeRageConnectorState();
            }
            return null;
        }

        private void Telegraph(PrimeStateContext context) {
            NPC npc = context.Npc;
            npc.damage = 0;
            dashDir = DirectionToTarget(context);
            context.SetChargeState(1, phaseTimer / (float)PrimeDirector.DashTelegraphFrames);
            npc.velocity *= 0.9f;

            if (!VaultUtils.isClient && phaseTimer == 1) {
                PrimeTelegraphLine.SpawnLine(npc, npc.Center, dashDir.ToRotation(), PrimeDirector.DashTelegraphFrames);
            }
            if (phaseTimer >= PrimeDirector.DashTelegraphFrames) {
                phase = 1;
                phaseTimer = 0;
            }
        }

        private void FlashReposition(PrimeStateContext context) {
            NPC npc = context.Npc;
            Player target = context.Target;
            flashFrom = npc.Center;
            Vector2 far = target.Center - dashDir * 520f;
            npc.Center = far;
            npc.velocity = Vector2.Zero;
            context.ResetChargeState();

            if (!VaultUtils.isServer) {
                for (int i = 0; i < 8; i++) {
                    Dust dust = Dust.NewDustDirect(flashFrom, 1, 1, DustID.Electric, 0, 0, 100, Color.Cyan, 1.6f);
                    dust.noGravity = true;
                }
                SoundEngine.PlaySound(SoundID.Item8 with { Pitch = 0.4f, Volume = 0.7f }, npc.Center);
            }

            phase = 2;
            phaseTimer = 0;
        }

        private void LineDash(PrimeStateContext context) {
            NPC npc = context.Npc;
            float speed = Main.masterMode ? 22f : 19f;
            if (context.BossRush) speed *= 1.2f;
            npc.velocity = dashDir * speed;
            float vel = npc.velocity.Length();
            npc.damage = vel > PrimeDirector.DashContactSpeedThreshold ? npc.defDamage * 2 : 0;
            SpinRotation(npc, 0.42f);

            if (!VaultUtils.isServer) {
                PrimeScreenEffects.PushHeatWake(npc.Center, npc.velocity.ToRotation(), 1f);
            }

            bool outOfBounds = Vector2.Distance(npc.Center, context.Target.Center) > 900f
                || phaseTimer > 28;
            if (outOfBounds) {
                Counter++;
                phase = 0;
                phaseTimer = 0;
            }
        }
    }
}
