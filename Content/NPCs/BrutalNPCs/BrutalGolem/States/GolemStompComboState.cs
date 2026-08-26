using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.States
{
    /// <summary>重踏跳跃连段：两记快跳压近 + 一记蓄力重踏放冲击波</summary>
    [InnoVault.StateMachines.VaultState((int)GolemStateIndex.StompCombo, typeof(GolemStateContext))]
    internal class GolemStompComboState : GolemStateBase
    {
        public override string StateName => "StompCombo";
        public override GolemStateIndex StateIndex => GolemStateIndex.StompCombo;

        private enum Step : int
        {
            Squat = 0,
            Air = 1,
            Land = 2,
        }

        private Step step;
        private int stepTimer;
        //Counter = 已完成跳数；最后一跳为重踏

        public override void OnEnter(GolemStateContext context) {
            base.OnEnter(context);
            step = Step.Squat;
            stepTimer = 0;
        }

        public override IGolemState OnUpdate(GolemStateContext context) {
            NPC npc = context.Npc;
            int totalHops = context.DeathMode ? 4 : 3;
            bool isHeavy = Counter == totalHops - 1;

            switch (step) {
                case Step.Squat: {
                    context.FrameMode = 1;
                    GroundBrake(npc);
                    int squat = Tempo(context, isHeavy ? 24 : 12);
                    if (isHeavy) {
                        context.SetChargeState(1, stepTimer / (float)squat);
                    }
                    if (++stepTimer >= squat && OnGround(npc)) {
                        stepTimer = 0;
                        step = Step.Air;
                        LaunchHop(context, isHeavy);
                    }
                    break;
                }
                case Step.Air: {
                    context.FrameMode = 2;
                    npc.damage = npc.defDamage;
                    AirSteer(context, isHeavy ? 0.22f : 0.16f, isHeavy ? 15f : 11f);

                    //落地判定
                    if (++stepTimer > 12 && npc.velocity.Y == 0f) {
                        stepTimer = 0;
                        step = Step.Land;
                        OnLand(context, isHeavy);
                    }
                    //空中兜底
                    if (stepTimer > 180) {
                        stepTimer = 0;
                        step = Step.Land;
                    }
                    break;
                }
                case Step.Land: {
                    context.FrameMode = 0;
                    GroundBrake(npc, 0.7f);
                    npc.damage = 0;
                    //快跳几乎无落地停顿，连段紧凑
                    int rest = Tempo(context, isHeavy ? 30 : 8);
                    if (++stepTimer >= rest) {
                        stepTimer = 0;
                        Counter++;
                        step = Step.Squat;
                    }
                    break;
                }
            }

            Timer++;
            if ((Counter >= totalHops || Timer > 660) && !VaultUtils.isClient) {
                return new GolemConnectorState();
            }
            return null;
        }

        private void LaunchHop(GolemStateContext context, bool isHeavy) {
            NPC npc = context.Npc;
            float dx = context.Target.Center.X - npc.Center.X;
            float vx = MathHelper.Clamp(dx / (isHeavy ? 34f : 44f), -14f, 14f);
            float vy = isHeavy ? -16.5f : -10.5f;
            if (context.Enraged) {
                vx *= 1.3f;
                vy *= 1.12f;
            }
            LaunchJump(npc, vx, vy);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.WormDig with { Pitch = -0.3f, Volume = 0.7f }, npc.Center);
            }
            if (!VaultUtils.isClient) {
                npc.netUpdate = true;
            }
        }

        private void OnLand(GolemStateContext context, bool isHeavy) {
            NPC npc = context.Npc;

            //落地表现：尘幕 + 碎屑
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item14 with { Pitch = isHeavy ? -0.5f : -0.1f }, npc.Center);
                GolemScreenEffects.Shake(isHeavy ? 6.5f : 3f);
                if (isHeavy) {
                    GolemScreenEffects.PushShockRing(npc.Bottom, 0.9f, 640f);
                }
                for (int l = (int)npc.position.X - 20; l < (int)npc.position.X + npc.width + 40; l += 20) {
                    for (int m = 0; m < 3; m++) {
                        Dust dust = Dust.NewDustDirect(new Vector2(npc.position.X - 20f, npc.position.Y + npc.height),
                            npc.width + 20, 4, DustID.Smoke, 0f, 0f, 100, default, 1.5f);
                        dust.velocity *= 0.2f;
                    }
                    int gore = Gore.NewGore(npc.GetSource_FromAI(),
                        new Vector2(l - 20, npc.position.Y + npc.height - 8f), default, Main.rand.Next(61, 64));
                    Main.gore[gore].velocity *= 0.4f;
                }
            }

            //落地碎石扇：快跳小扇、重踏大扇，压制头顶空域（服务端）
            if (!VaultUtils.isClient) {
                int shards = isHeavy ? 5 : (context.Sundered ? 3 : 2);
                if (context.DeathMode) {
                    shards++;
                }
                SpawnLandingShards(context, shards);
            }

            //重踏：双向地行冲击波（服务端）
            if (isHeavy && !VaultUtils.isClient) {
                int damage = ScaleDamage(context, GolemDirector.ShockwaveDamage);
                for (int dir = -1; dir <= 1; dir += 2) {
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Bottom + new Vector2(dir * 40f, -14f),
                        new Vector2(dir * 10.5f, 0f), ModContent.ProjectileType<GolemShockWave>(),
                        damage, 0f, Main.myPlayer);
                }
                //二阶段追加升空余烬
                if (context.Sundered) {
                    int emberDamage = ScaleDamage(context, GolemDirector.EmberDamage);
                    for (int i = 0; i < 4; i++) {
                        Vector2 vel = new(Main.rand.NextFloat(-4.5f, 4.5f), Main.rand.NextFloat(-11f, -7f));
                        Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Top, vel,
                            ModContent.ProjectileType<GolemSunMortar>(), emberDamage, 0f, Main.myPlayer,
                            1f, 0f);
                    }
                }
                npc.netUpdate = true;
            }
        }
    }
}
