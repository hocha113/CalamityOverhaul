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
    /// 种子加特林：钩爪绷紧锁体→怒转起火→两轮弹幕软管压制，
    /// 逐发后坐累积弹簧回弹，弹幕带追踪迟滞可走位
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)PlanteraStateIndex.SeedGatling, typeof(PlanteraStateContext))]
    internal class PlanteraSeedGatlingState : PlanteraStateBase
    {
        public override string StateName => "SeedGatling";
        public override PlanteraStateIndex StateIndex => PlanteraStateIndex.SeedGatling;

        private int WindupTime(PlanteraStateContext ctx) => (int)(46 * PlanteraDirector.TimeScale(ctx));
        private const int FireTime = 75;
        private const int GapTime = 30;
        private const int VolleyCount = 2;
        //激怒射速翻倍：3→2 / 死亡模式 2→1
        private int FireInterval(PlanteraStateContext ctx) {
            int interval = ctx.IsDeathMode ? 2 : 3;
            return ctx.IsEnraged ? Math.Max(interval / 2, 1) : interval;
        }

        private Vector2 lockPoint;
        private Vector2 recoilOffset;
        private float aimAngle;

        public PlanteraSeedGatlingState() {
        }

        public override void OnEnter(PlanteraStateContext context) {
            base.OnEnter(context);
            context.SkipDefaultMovement = true;
            lockPoint = context.Npc.Center;
            recoilOffset = Vector2.Zero;
            aimAngle = (context.Target.Center - context.Npc.Center).ToRotation();
            //锁体应力声
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Pitch = -0.7f, Volume = 0.8f }, context.Npc.Center);
        }

        public override IPlanteraState OnUpdate(PlanteraStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            int windup = WindupTime(context);

            context.SkipDefaultMovement = true;
            context.RotationMode = 2;

            //追踪迟滞：慢慢咬向玩家，可以横向甩开
            float desiredAngle = (player.Center - npc.Center).ToRotation();
            float trackRate = Timer < windup ? 0.12f : 0.045f;
            aimAngle = aimAngle.AngleLerp(desiredAngle, trackRate);
            Vector2 aim = aimAngle.ToRotationVector2();
            npc.rotation = aimAngle + MathHelper.PiOver2;

            //后坐弹簧回弹
            recoilOffset *= 0.82f;
            npc.velocity = lockPoint + recoilOffset - npc.Center;

            Timer++;

            //------ 怒转起火 ------
            if (Timer <= windup) {
                float t = Timer / (float)windup;
                context.SetChargeState(2, t);
                context.GlowPulse = 0.25f + t * 0.5f;
                //反向缩身蓄势
                recoilOffset = -aim * (float)Math.Pow(t, 4) * 22f;

                if (!VaultUtils.isServer) {
                    PlanteraRenderHelper.SpawnChargeIntake(context, t);
                    foreach (var hook in context.Hooks) {
                        PlanteraVineRenderer.PushPulse(hook.whoAmI, 0.25f + t * 0.45f);
                    }
                    //转轮咔哒加速
                    int tick = (int)MathHelper.Lerp(12f, 4f, t);
                    if (Timer % Math.Max(tick, 3) == 0) {
                        SoundEngine.PlaySound(SoundID.Item17 with {
                            Volume = 0.35f + t * 0.3f,
                            Pitch = -0.6f + t * 0.7f,
                            MaxInstances = 6
                        }, npc.Center);
                    }
                }
                return null;
            }

            //------ 弹幕轮 ------
            int cycleTimer = Timer - windup;
            int cycleLength = FireTime + GapTime;
            int volleyIndex = cycleTimer / cycleLength;
            int inCycle = cycleTimer % cycleLength;

            if (volleyIndex >= VolleyCount) {
                //收招：过热排气
                if (!VaultUtils.isServer && cycleTimer == VolleyCount * cycleLength + 1) {
                    PlanteraRenderHelper.SpawnSporePuff(npc.Center + aim * 40f, 1.2f);
                    SoundEngine.PlaySound(SoundID.NPCDeath1 with { Volume = 0.5f, Pitch = 0.5f }, npc.Center);
                }
                if (cycleTimer > VolleyCount * cycleLength + 14 && !VaultUtils.isClient) {
                    return new PlanteraCanopyState();
                }
                return null;
            }

            if (inCycle < FireTime) {
                UpdateFiring(context, aim, inCycle, volleyIndex);
            }
            else {
                //轮间歇：荧光降温，重新咬定
                context.GlowPulse = 0.3f;
                if (inCycle == FireTime + 1 && !VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.NPCHit1 with { Pitch = -0.5f, Volume = 0.6f }, npc.Center);
                }
                //二阶段轮间歇甩双荆棘球
                if (inCycle == FireTime + 6 && context.IsPhase2 && !VaultUtils.isClient) {
                    for (int i = -1; i <= 1; i += 2) {
                        Vector2 vel = aim.RotatedBy(i * 0.35f) * 11f + new Vector2(0f, -3f);
                        Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + aim * 40f, vel,
                            ModContent.ProjectileType<PlanteraThornBall>(), PlanteraThornBall.GetDamage(npc), 0f, Main.myPlayer);
                    }
                }
            }

            return null;
        }

        private void UpdateFiring(PlanteraStateContext context, Vector2 aim, int inCycle, int volleyIndex) {
            NPC npc = context.Npc;
            context.GlowPulse = 0.65f;

            //新轮起步射速热身(公平阀)
            int interval = FireInterval(context);
            if (inCycle < 18) {
                interval *= 2;
            }

            if (inCycle % Math.Max(interval, 1) != 0) {
                return;
            }

            Vector2 muzzle = npc.Center + aim * 46f;
            float speed = context.IsPhase2 ? 25f : 23f;
            float spread = context.IsPhase2 ? 0.15f : 0.10f;

            //权威端出弹
            if (!VaultUtils.isClient) {
                Vector2 vel = aim.RotatedBy(Main.rand.NextFloat(-spread, spread)) * speed
                    * Main.rand.NextFloat(0.94f, 1.06f);
                Projectile.NewProjectile(npc.GetSource_FromAI(), muzzle, vel,
                    ModContent.ProjectileType<PlanteraSeed>(), PlanteraSeed.GetDamage(npc), 0f, Main.myPlayer);

                //每16帧混一发毒种抛射
                if (inCycle % 16 == 0) {
                    Vector2 lobVel = aim * 13f + new Vector2(0f, -4.5f);
                    Projectile.NewProjectile(npc.GetSource_FromAI(), muzzle, lobVel,
                        ModContent.ProjectileType<PlanteraPoisonSeed>(), PlanteraPoisonSeed.GetDamage(npc), 0f, Main.myPlayer);
                }
                //死亡模式轮中带荆棘球
                if (context.IsDeathMode && inCycle == 40) {
                    Projectile.NewProjectile(npc.GetSource_FromAI(), muzzle, aim * 12f - Vector2.UnitY * 2f,
                        ModContent.ProjectileType<PlanteraThornBall>(), PlanteraThornBall.GetDamage(npc), 0f, Main.myPlayer);
                }
            }

            //逐发后坐累积
            recoilOffset -= aim * 2.3f;
            if (recoilOffset.Length() > 28f) {
                recoilOffset = recoilOffset.SafeNormalize(Vector2.Zero) * 28f;
            }

            //各端本地：枪口闪+射击声
            if (!VaultUtils.isServer) {
                InnoVault.PRT.PRTLoader.NewParticle<PRT_PlanteraSporeMote>(muzzle,
                    aim * 3f, PlanteraRenderHelper.GlowByPhase(context.IsPhase2), 1.4f)?.SetLife(10);
                SoundEngine.PlaySound(SoundID.Item17 with {
                    Volume = 0.55f,
                    Pitch = 0.15f + volleyIndex * 0.1f + Main.rand.NextFloat(0.08f),
                    MaxInstances = 8
                }, muzzle);
            }
        }

        public override void OnExit(PlanteraStateContext context) {
            base.OnExit(context);
            context.SkipDefaultMovement = false;
        }
    }
}
