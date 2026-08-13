using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Rendering;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.States
{
    /// <summary>
    /// 二阶段连环狂扑：三连弹弓猛扑，扑线撒孢子雷，
    /// 刹车点回喷交叉种子——近身处刑的位移压迫
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)PlanteraStateIndex.FrenzyPounce, typeof(PlanteraStateContext))]
    internal class PlanteraFrenzyPounceState : PlanteraStateBase
    {
        public override string StateName => "FrenzyPounce";
        public override PlanteraStateIndex StateIndex => PlanteraStateIndex.FrenzyPounce;

        private const int PhaseAim = 0;
        private const int PhaseTravel = 1;
        private const int PhaseBrake = 2;

        private int MaxPounce(PlanteraStateContext ctx) => ctx.IsDeathMode ? 4 : 3;
        private int AimTime(PlanteraStateContext ctx) => Math.Max(24 - Counter * 3, 16);

        private int phase;
        private int phaseTimer;
        private Vector2 pounceDir;
        private float traveled;
        private float sporeDropAccum;

        public PlanteraFrenzyPounceState() {
        }

        public override void OnEnter(PlanteraStateContext context) {
            base.OnEnter(context);
            context.SkipDefaultMovement = true;
            phase = PhaseAim;
            phaseTimer = 0;
            BeginAim(context);
        }

        private void BeginAim(PlanteraStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            pounceDir = (player.Center + player.velocity * 12f - npc.Center).SafeNormalize(Vector2.UnitY);

            if (!VaultUtils.isClient) {
                for (int i = 0; i < context.Hooks.Count && i < 2; i++) {
                    NPC hook = context.Hooks[i];
                    Vector2 wish = npc.Center + pounceDir * (460f + i * 300f)
                        + pounceDir.RotatedBy(MathHelper.PiOver2) * (i == 0 ? 60f : -60f);
                    PlanteraHookAI.Command(hook, PlanteraHookAI.FindAnchorNear(wish, 6f, Vector2.Zero));
                }
                PlanteraTelegraphLine.Spawn(npc, npc.Center, pounceDir.ToRotation(), AimTime(context), 1900f);
                npc.netUpdate = true;
            }

            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.85f, Pitch = -0.3f }, npc.Center);
            }
        }

        public override IPlanteraState OnUpdate(PlanteraStateContext context) {
            context.SkipDefaultMovement = true;
            phaseTimer++;
            Timer++;

            switch (phase) {
                case PhaseAim:
                    UpdateAim(context);
                    break;
                case PhaseTravel:
                    UpdateTravel(context);
                    break;
                default:
                    return UpdateBrake(context);
            }
            return null;
        }

        private void UpdateAim(PlanteraStateContext context) {
            NPC npc = context.Npc;
            int aimTime = AimTime(context);
            float t = MathHelper.Clamp(phaseTimer / (float)aimTime, 0f, 1f);

            npc.damage = 0;
            context.RotationMode = 2;
            npc.rotation = npc.rotation.AngleLerp(pounceDir.ToRotation() + MathHelper.PiOver2, 0.28f);
            context.SetChargeState(1, t);
            context.GlowPulse = 0.4f + t * 0.6f;

            float reel = (float)Math.Pow(t, 6) * 10f;
            npc.velocity = Vector2.Lerp(npc.velocity, -pounceDir * (1.5f + reel), 0.3f);

            if (!VaultUtils.isServer) {
                for (int i = 0; i < context.Hooks.Count && i < 2; i++) {
                    PlanteraVineRenderer.PushPulse(context.Hooks[i].whoAmI, 0.4f + t * 0.6f);
                }
            }

            if (phaseTimer >= aimTime) {
                phase = PhaseTravel;
                phaseTimer = 0;
                traveled = 0f;
                sporeDropAccum = 0f;
                context.ResetChargeState();

                npc.velocity = pounceDir * PlanteraDirector.PounceSpeedP2 * 1.12f;
                if (!VaultUtils.isClient) {
                    npc.netUpdate = true;
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.ForceRoar with { Pitch = 0.45f, Volume = 1f }, npc.Center);
                    PlanteraRenderHelper.SpawnPetalBurst(npc.Center, 14, 8f, true);
                    PlanteraScreenFX.CameraPunch(npc.Center, 7f, 14, "PlanteraFrenzy", pounceDir);
                }
            }
        }

        private void UpdateTravel(PlanteraStateContext context) {
            NPC npc = context.Npc;
            float speed = npc.velocity.Length();
            traveled += speed;

            context.RotationMode = 1;
            context.GlowPulse = 0.85f;
            npc.damage = speed > PlanteraDirector.PounceContactSpeedGate
                ? (int)(npc.defDamage * 1.45f) : 0;

            //扑线撒雷(权威端，限量)
            sporeDropAccum += speed;
            if (sporeDropAccum > 130f && !VaultUtils.isClient) {
                sporeDropAccum = 0f;
                PlanteraSporeAI.SpawnSpore(npc, npc.Center - pounceDir * 40f,
                    pounceDir.RotatedBy(MathHelper.PiOver2 * (Main.rand.NextBool() ? 1f : -1f)) * 1.5f);
            }

            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                PlanteraRenderHelper.SpawnPetalBurst(npc.Center, 1, 2.5f, true);
            }

            if (traveled > 820f || phaseTimer > 24) {
                phase = PhaseBrake;
                phaseTimer = 0;

                //刹车点回喷交叉种子
                if (!VaultUtils.isClient) {
                    for (int i = -1; i <= 1; i += 2) {
                        Vector2 vel = (-pounceDir).RotatedBy(i * 0.42f) * 19f;
                        Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, vel,
                            ModContent.ProjectileType<PlanteraSeed>(), PlanteraSeed.GetDamage(npc), 0f, Main.myPlayer);
                    }
                }
            }
        }

        private IPlanteraState UpdateBrake(PlanteraStateContext context) {
            NPC npc = context.Npc;
            npc.velocity *= 0.76f;
            npc.damage = 0;
            context.RotationMode = 0;

            if (phaseTimer >= 12) {
                Counter++;
                if (Counter >= MaxPounce(context)) {
                    if (!VaultUtils.isClient) {
                        return new PlanteraCanopyState();
                    }
                    return null;
                }
                phase = PhaseAim;
                phaseTimer = 0;
                BeginAim(context);
            }
            return null;
        }

        public override void OnExit(PlanteraStateContext context) {
            base.OnExit(context);
            context.SkipDefaultMovement = false;
            context.Npc.damage = context.Npc.defDamage;
            if (!VaultUtils.isClient) {
                foreach (var hook in context.Hooks) {
                    PlanteraHookAI.Release(hook);
                }
            }
        }
    }
}
