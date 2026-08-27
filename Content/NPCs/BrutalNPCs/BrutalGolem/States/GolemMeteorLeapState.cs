using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Projectiles;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.States
{
    /// <summary>陨落重压（二阶段）：巨跳升空 → 落点标记收缩锁定 → 天坠重砸 → 尖刺环起爆</summary>
    [InnoVault.StateMachines.VaultState((int)GolemStateIndex.MeteorLeap, typeof(GolemStateContext))]
    internal class GolemMeteorLeapState : GolemStateBase
    {
        public override string StateName => "MeteorLeap";
        public override GolemStateIndex StateIndex => GolemStateIndex.MeteorLeap;

        private enum Step : int
        {
            Squat = 0,
            Rise = 1,
            Hang = 2,
            Slam = 3,
            Recover = 4,
        }

        private Step step;
        private int stepTimer;

        public override void OnEnter(GolemStateContext context) {
            base.OnEnter(context);
            step = Step.Squat;
            stepTimer = 0;
            context.LockPoint = Vector2.Zero;
        }

        public override IGolemState OnUpdate(GolemStateContext context) {
            NPC npc = context.Npc;

            switch (step) {
                case Step.Squat: {
                    context.FrameMode = 1;
                    GroundBrake(npc);
                    context.SetChargeState(1, stepTimer / (float)Tempo(context, 22));
                    if (++stepTimer >= Tempo(context, 22)) {
                        stepTimer = 0;
                        step = Step.Rise;
                        LaunchJump(context, MathHelper.Clamp((context.Target.Center.X - npc.Center.X) / 40f, -10f, 10f), -27f);
                        if (!VaultUtils.isServer) {
                            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.3f }, npc.Center);
                            GolemScreenEffects.Shake(4f);
                        }
                        if (!VaultUtils.isClient) {
                            npc.netUpdate = true;
                        }
                    }
                    break;
                }
                case Step.Rise: {
                    context.FrameMode = 2;
                    npc.damage = 0;
                    //升空期减速上飘
                    npc.velocity.Y *= 0.985f;
                    npc.velocity.X *= 0.99f;
                    if (++stepTimer >= 34) {
                        stepTimer = 0;
                        step = Step.Hang;
                    }
                    break;
                }
                case Step.Hang: {
                    context.FrameMode = 2;
                    npc.damage = 0;
                    npc.velocity *= 0.9f;

                    int hang = Tempo(context, 52);

                    //落点早锁：第8帧定格并落环，之后标记不再追踪（公平阀，环即落点）
                    if (!VaultUtils.isClient) {
                        if (stepTimer < 8) {
                            float groundY = GolemHookSwingState.FindGroundY(context.Target);
                            context.LockPoint = new Vector2(context.Target.Center.X + context.Target.velocity.X * 16f, groundY);
                        }
                        if (stepTimer == 8) {
                            GolemTelegraph.SpawnRing(npc, context.LockPoint, 190f, hang - 8 + 18);
                            npc.netUpdate = true;
                        }
                        //横向缓移到锁点上空
                        if (context.LockPoint.LengthSquared() > 1f) {
                            float dx = context.LockPoint.X - npc.Center.X;
                            npc.velocity.X = MathHelper.Clamp(dx * 0.03f, -16f, 16f);
                        }
                    }

                    if (++stepTimer >= hang) {
                        stepTimer = 0;
                        step = Step.Slam;
                        if (!VaultUtils.isClient) {
                            //对齐锁点正上方后直坠
                            Vector2 lockPoint = context.LockPoint;
                            if (lockPoint.LengthSquared() > 1f) {
                                npc.Bottom = new Vector2(lockPoint.X, npc.Bottom.Y);
                            }
                            npc.velocity = new Vector2(0f, 34f);
                            npc.noTileCollide = true;
                            npc.netUpdate = true;
                        }
                        if (!VaultUtils.isServer) {
                            SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.6f, Volume = 1f }, npc.Center);
                        }
                    }
                    break;
                }
                case Step.Slam: {
                    context.FrameMode = 2;
                    npc.damage = npc.defDamage;
                    npc.velocity.X = 0f;
                    npc.velocity.Y = Math.Max(npc.velocity.Y, 30f);

                    //穿越锁点高度后恢复碰撞
                    Vector2 lockPoint = context.LockPoint;
                    if (lockPoint.LengthSquared() > 1f && npc.Bottom.Y > lockPoint.Y - 60f) {
                        npc.noTileCollide = false;
                    }
                    RestoreTileCollide(context);

                    if (npc.velocity.Y == 0f) {
                        stepTimer = 0;
                        step = Step.Recover;
                        OnImpact(context);
                    }
                    //坠落兜底
                    if (++stepTimer > 140) {
                        stepTimer = 0;
                        step = Step.Recover;
                    }
                    break;
                }
                case Step.Recover: {
                    context.FrameMode = 0;
                    npc.damage = 0;
                    GroundBrake(npc, 0.7f);
                    if (++stepTimer >= Tempo(context, 42)) {
                        Counter++;
                        //死亡模式二连跳
                        if (Counter < (context.DeathMode ? 2 : 1)) {
                            stepTimer = 0;
                            step = Step.Squat;
                        }
                        else if (!VaultUtils.isClient) {
                            return new GolemConnectorState();
                        }
                    }
                    break;
                }
            }

            Timer++;
            //全局兜底
            if (Timer > 720 && !VaultUtils.isClient) {
                return new GolemConnectorState();
            }
            return null;
        }

        /// <summary>触地冲击：震屏 + 双冲击波 + 尖刺环序列起爆</summary>
        private void OnImpact(GolemStateContext context) {
            NPC npc = context.Npc;

            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.6f, Volume = 1.2f }, npc.Center);
                SoundEngine.PlaySound(SoundID.NPCHit4 with { Pitch = -0.8f }, npc.Center);
                GolemScreenEffects.Shake(8f);
                GolemScreenEffects.PushShockRing(npc.Bottom, 1f, 760f);
                for (int l = (int)npc.position.X - 40; l < (int)npc.position.X + npc.width + 60; l += 20) {
                    Dust dust = Dust.NewDustDirect(new Vector2(npc.position.X - 40f, npc.position.Y + npc.height),
                        npc.width + 60, 6, DustID.Smoke, 0f, 0f, 100, default, 1.8f);
                    dust.velocity *= 0.3f;
                }
            }

            if (VaultUtils.isClient) {
                return;
            }

            //双向冲击波
            int waveDamage = ScaleDamage(context, GolemDirector.ShockwaveDamage);
            for (int dir = -1; dir <= 1; dir += 2) {
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Bottom + new Vector2(dir * 40f, -14f),
                    new Vector2(dir * 11.5f, 0f), ModContent.ProjectileType<GolemShockWave>(),
                    waveDamage, 0f, Main.myPlayer);
            }

            //尖刺环：以落点为心向外序列起爆
            int spikeDamage = ScaleDamage(context, GolemDirector.SpikeDamage);
            int rings = context.DeathMode ? 4 : 3;
            for (int r = 1; r <= rings; r++) {
                for (int dir = -1; dir <= 1; dir += 2) {
                    float x = npc.Bottom.X + dir * r * 150f;
                    GolemTrapUnit.PlantOnGround(npc, x, npc.Bottom.Y - 20f,
                        GolemTrapUnit.TrapKind.Spike, 16 + r * 8, spikeDamage);
                }
            }
            npc.netUpdate = true;
        }
    }
}
