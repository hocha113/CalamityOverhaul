using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Rendering;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.States
{
    /// <summary>
    /// 钩爪位移猛扑：双钩先行沿扑线锚定→本体反向拉弓→
    /// 藤蔓弹弓释放，重量级贯穿冲撞，链式两扑
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)PlanteraStateIndex.GrapplePounce, typeof(PlanteraStateContext))]
    internal class PlanteraGrapplePounceState : PlanteraStateBase
    {
        public override string StateName => "GrapplePounce";
        public override PlanteraStateIndex StateIndex => PlanteraStateIndex.GrapplePounce;

        private const int PhaseAim = 0;
        private const int PhaseTravel = 1;
        private const int PhaseBrake = 2;

        private int MaxPounce(PlanteraStateContext ctx) => ctx.IsDeathMode ? 3 : 2;
        private int AimTime(PlanteraStateContext ctx) {
            int t = PlanteraDirector.PounceTelegraphFrames - Counter * 8;
            return Math.Max((int)(t * PlanteraDirector.DeathTimeScale(ctx)), 20);
        }

        private int phase;
        private int phaseTimer;
        private Vector2 pounceDir;
        private float traveled;

        public PlanteraGrapplePounceState() {
        }

        public override void OnEnter(PlanteraStateContext context) {
            base.OnEnter(context);
            context.SkipDefaultMovement = true;
            phase = PhaseAim;
            phaseTimer = 0;
            BeginAim(context);
        }

        /// <summary>起手：锁线+派钩+预警线，权威端裁决方向</summary>
        private void BeginAim(PlanteraStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            pounceDir = (player.Center + player.velocity * 14f - npc.Center).SafeNormalize(Vector2.UnitY);

            if (!VaultUtils.isClient) {
                //双钩沿扑线前置锚定(拉弓的弦)
                for (int i = 0; i < context.Hooks.Count && i < 2; i++) {
                    NPC hook = context.Hooks[i];
                    Vector2 wish = npc.Center + pounceDir * (520f + i * 320f)
                        + pounceDir.RotatedBy(MathHelper.PiOver2) * (i == 0 ? 70f : -70f);
                    Vector2 anchor = PlanteraHookAI.FindAnchorNear(wish, 7f, Vector2.Zero);
                    PlanteraHookAI.Command(hook, anchor);
                }
                PlanteraTelegraphLine.Spawn(npc, npc.Center, pounceDir.ToRotation(), AimTime(context), 2100f);
                npc.netUpdate = true;
            }

            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.9f, Pitch = -0.55f }, npc.Center);
            }
        }

        public override IPlanteraState OnUpdate(PlanteraStateContext context) {
            NPC npc = context.Npc;
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

            //预警期无接触伤害(公平阀)
            npc.damage = 0;
            context.RotationMode = 2;
            npc.rotation = npc.rotation.AngleLerp(pounceDir.ToRotation() + MathHelper.PiOver2, 0.2f);
            context.SetChargeState(1, t);
            context.GlowPulse = 0.3f + t * 0.55f;

            //反向拉弓，pow(t,6)末段猛缩
            float reel = (float)Math.Pow(t, 6) * 8.5f;
            npc.velocity = Vector2.Lerp(npc.velocity, -pounceDir * (1.2f + reel), 0.25f);

            //弦上行波：能量涌向本体
            if (!VaultUtils.isServer) {
                for (int i = 0; i < context.Hooks.Count && i < 2; i++) {
                    PlanteraVineRenderer.PushPulse(context.Hooks[i].whoAmI, 0.3f + t * 0.65f);
                }
                //末12帧咬合静默
                if (phaseTimer == aimTime - 12) {
                    SoundEngine.PlaySound(SoundID.NPCHit1 with { Pitch = -0.3f, Volume = 0.7f }, npc.Center);
                }
            }

            if (phaseTimer >= aimTime) {
                Launch(context);
            }
        }

        /// <summary>弹弓释放帧</summary>
        private void Launch(PlanteraStateContext context) {
            NPC npc = context.Npc;
            phase = PhaseTravel;
            phaseTimer = 0;
            traveled = 0f;
            context.ResetChargeState();

            npc.velocity = pounceDir * PlanteraDirector.PounceSpeedP1 * 1.15f;
            if (!VaultUtils.isClient) {
                npc.netUpdate = true;
            }

            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.ForceRoar with { Pitch = 0.25f, Volume = 1f }, npc.Center);
                PlanteraRenderHelper.SpawnPetalBurst(npc.Center, 12, 7f, context.IsPhase2);
                PlanteraScreenFX.CameraPunch(npc.Center, 6f, 14, "PlanteraPounce", pounceDir);
            }
        }

        private void UpdateTravel(PlanteraStateContext context) {
            NPC npc = context.Npc;
            float speed = npc.velocity.Length();
            traveled += speed;

            context.RotationMode = 1;
            context.GlowPulse = 0.7f;

            //接触伤害只在高速窗口(与视觉一致)
            npc.damage = speed > PlanteraDirector.PounceContactSpeedGate
                ? (int)(npc.defDamage * 1.35f) : 0;

            //冲撞掉瓣
            if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                PlanteraRenderHelper.SpawnPetalBurst(npc.Center, 1, 2f, context.IsPhase2);
            }

            if (traveled > 880f || phaseTimer > 26) {
                phase = PhaseBrake;
                phaseTimer = 0;
            }
        }

        private IPlanteraState UpdateBrake(PlanteraStateContext context) {
            NPC npc = context.Npc;
            npc.velocity *= 0.78f;
            npc.damage = 0;
            context.RotationMode = 0;

            if (phaseTimer < 2 && !VaultUtils.isServer) {
                //急停摆荡：残余动能甩进悬吊摆
                context.SwayPhase += 0.8f;
            }

            if (phaseTimer >= 14) {
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
            //钩爪放回自由狩锚
            if (!VaultUtils.isClient) {
                foreach (var hook in context.Hooks) {
                    PlanteraHookAI.Release(hook);
                }
            }
        }
    }
}
