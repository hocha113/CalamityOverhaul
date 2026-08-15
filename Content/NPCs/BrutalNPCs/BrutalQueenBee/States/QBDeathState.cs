using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.Rendering;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.States
{
    /// <summary>
    /// 死亡演出：痉挛失控(蜂群四散惊逃)→残翅爬升→顶点失速→坠地蜜爆→<br/>
    /// 幸存蜂群归来结环志哀→散场真死<br/>
    /// 编舞核心：指挥者死了，编队随之瓦解——蜂群的失序就是她的死亡叙事
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)QueenBeeStateIndex.Death, typeof(QueenBeeStateContext))]
    internal class QBDeathState : QueenBeeStateBase
    {
        public override string StateName => "Death";
        public override QueenBeeStateIndex StateIndex => QueenBeeStateIndex.Death;

        #region 节奏常量(运镜共用)
        internal const int ConvulseEnd = 60;
        internal const int ClimbEnd = 136;
        internal const int StallEnd = 150;
        internal const int FallEnd = 226;
        internal const int MournEnd = 300;
        internal const int TotalTime = 334;
        #endregion

        private bool impactDone;
        private Vector2 restPos;

        public override void OnEnter(QueenBeeStateContext context) {
            base.OnEnter(context);
            NPC npc = context.Npc;
            context.DeathPerformanceFinished = false;
            npc.dontTakeDamage = true;
            npc.damage = 0;
            if (npc.life < 1) {
                npc.life = 1;
            }
            impactDone = false;

            //哀鸣
            SoundEngine.PlaySound(SoundID.Zombie125 with { Volume = 1.1f, Pitch = 0.55f }, npc.Center);
            SoundEngine.PlaySound(SoundID.NPCHit18 with { Volume = 0.8f, Pitch = -0.6f }, npc.Center);

            //场上炮台跟着凋亡
            if (!VaultUtils.isClient) {
                int turretType = ModContent.ProjectileType<WaxHiveTurret>();
                foreach (var proj in Main.ActiveProjectiles) {
                    if (proj.type == turretType && proj.timeLeft > 40) {
                        proj.timeLeft = 40;
                        proj.netUpdate = true;
                    }
                }
            }
        }

        public override IQueenBeeState OnUpdate(QueenBeeStateContext context) {
            NPC npc = context.Npc;

            //锁血无伤无害
            npc.dontTakeDamage = true;
            npc.damage = 0;
            if (npc.life < 1) {
                npc.life = 1;
            }

            Timer++;

            if (Timer <= ConvulseEnd) {
                UpdateConvulse(context, npc);
            }
            else if (Timer <= ClimbEnd) {
                UpdateClimb(context, npc);
            }
            else if (Timer <= StallEnd) {
                UpdateStall(context, npc);
            }
            else if (Timer <= FallEnd) {
                UpdateFall(context, npc);
            }
            else if (Timer <= MournEnd) {
                UpdateMourn(context, npc);
            }
            else {
                UpdateFinale(context, npc);
            }

            //服务端/单人放行真死
            if (Timer >= TotalTime && !VaultUtils.isClient) {
                context.DeathPerformanceFinished = true;
                npc.dontTakeDamage = false;
                npc.life = 0;
                npc.HitEffect();
                npc.checkDead();
                npc.netUpdate = true;
            }
            return null;
        }

        /// <summary>幕一 痉挛：编队瓦解，蜂群惊逃</summary>
        private void UpdateConvulse(QueenBeeStateContext context, NPC npc) {
            npc.velocity *= 0.92f;
            //痉挛抖动(确定性正弦，不引入随机速度)
            npc.rotation = (float)Math.Sin(Timer * 1.1f) * 0.14f * (Timer / (float)ConvulseEnd);

            //蜂群惊逃四散
            context.Swarm.Declare(SwarmFormation.Scatter, npc.Center, Vector2.UnitX);

            //蜜血喷洒渐密
            if (!VaultUtils.isServer && Timer % 5 == 0) {
                float ramp = Timer / (float)ConvulseEnd;
                QueenBeeMotion.HoneyBurst(npc.Center + Main.rand.NextVector2Circular(24f, 20f),
                    0.6f + ramp * 0.5f, 3 + (int)(ramp * 4f), Timer % 15 == 0);
            }
            if (Timer % 18 == 0) {
                SoundEngine.PlaySound(SoundID.Zombie125 with {
                    Volume = 0.5f,
                    Pitch = 0.6f + Timer / (float)ConvulseEnd * 0.3f,
                    MaxInstances = 2
                }, npc.Center);
                QueenBeeMotion.Shake(npc.Center, 2.5f, 8);
            }
        }

        /// <summary>幕二 残翅爬升：最后一次扑向天空</summary>
        private void UpdateClimb(QueenBeeStateContext context, NPC npc) {
            float t = (Timer - ConvulseEnd) / (float)(ClimbEnd - ConvulseEnd);
            //爬升力衰减+扑翅喘振
            float sputter = 0.6f + 0.4f * (float)Math.Sin(Timer * 0.5f);
            npc.velocity.Y = MathHelper.Lerp(-3.2f, -0.6f, t) * sputter;
            npc.velocity.X *= 0.96f;
            npc.rotation = npc.rotation.AngleLerp(0f, 0.1f);

            context.Swarm.Declare(SwarmFormation.Scatter, npc.Center, Vector2.UnitX);

            //扑翅声一次比一次虚
            if (Timer % 16 == 0) {
                QueenBeeMotion.WingHum(npc.Center, 0.45f * (1f - t * 0.6f), -0.2f - t * 0.5f);
            }
            if (!VaultUtils.isServer && Timer % 7 == 0) {
                PRTLoader.NewParticle<PRT_HoneyDrop>(npc.Center + Main.rand.NextVector2Circular(16f, 12f),
                    Vector2.UnitY * Main.rand.NextFloat(0.5f, 1.5f),
                    QueenBeeMotion.AmberDeep, Main.rand.NextFloat(0.6f, 1f));
            }
        }

        /// <summary>幕三 顶点失速：一拍死寂</summary>
        private void UpdateStall(QueenBeeStateContext context, NPC npc) {
            npc.velocity *= 0.82f;
            npc.rotation = npc.rotation.AngleLerp(0f, 0.2f);
            context.Swarm.Declare(SwarmFormation.Scatter, npc.Center, Vector2.UnitX);
            //刻意静默：坠落前的吸气
        }

        /// <summary>幕四 坠落与蜜爆</summary>
        private void UpdateFall(QueenBeeStateContext context, NPC npc) {
            context.Swarm.Declare(SwarmFormation.Scatter, npc.Center, Vector2.UnitX);

            if (impactDone) {
                //触地后瘫伏
                npc.velocity *= 0.8f;
                npc.rotation = npc.rotation.AngleLerp(0.42f * (npc.spriteDirection >= 0 ? 1f : -1f), 0.08f);
                restPos = npc.Center;
                return;
            }

            //自由坠落+微旋
            npc.velocity.Y += 0.42f;
            if (npc.velocity.Y > 17f) {
                npc.velocity.Y = 17f;
            }
            npc.rotation += 0.024f * (npc.spriteDirection >= 0 ? 1f : -1f);
            context.UseChargePose = true;

            //坠落尾烟+蜜滴
            if (!VaultUtils.isServer && Timer % 3 == 0) {
                PRTLoader.NewParticle<PRT_HoneyMist>(npc.Center - npc.velocity * 0.6f,
                    Main.rand.NextVector2Circular(0.6f, 0.4f), QueenBeeMotion.AmberDeep * 0.45f,
                    Main.rand.NextFloat(0.7f, 1.2f));
                PRTLoader.NewParticle<PRT_HoneyDrop>(npc.Center + Main.rand.NextVector2Circular(14f, 10f),
                    -npc.velocity * 0.1f, QueenBeeMotion.HoneyGold, Main.rand.NextFloat(0.5f, 0.9f));
            }

            //触地或坠满即蜜爆
            bool grounded = Collision.SolidCollision(npc.position + new Vector2(0f, npc.height * 0.7f),
                npc.width, (int)(npc.height * 0.4f));
            if (grounded || Timer == FallEnd) {
                impactDone = true;
                npc.velocity = new Vector2(npc.velocity.X * 0.2f, -npc.velocity.Y * 0.24f);
                QueenBeeMotion.HoneyBurst(npc.Center + new Vector2(0f, npc.height * 0.3f), 2.6f, 30);
                QueenBeeMotion.Shake(npc.Center, 11f, 22);
                SoundEngine.PlaySound(SoundID.NPCDeath1 with { Volume = 1f, Pitch = -0.7f }, npc.Center);
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 1f, Pitch = -0.6f }, npc.Center);
            }
        }

        /// <summary>幕五 志哀环：幸存蜂群归来，绕遗骸缓旋</summary>
        private void UpdateMourn(QueenBeeStateContext context, NPC npc) {
            npc.velocity *= 0.85f;
            npc.rotation = npc.rotation.AngleLerp(0.42f * (npc.spriteDirection >= 0 ? 1f : -1f), 0.06f);

            Vector2 haloCenter = (restPos == Vector2.Zero ? npc.Center : restPos) + new Vector2(0f, -100f);
            context.Swarm.Declare(SwarmFormation.Halo, haloCenter, Vector2.UnitX, 0.72f);
            context.Swarm.PushRibbon(0.4f);

            //渐弱的低鸣
            if (Timer % 34 == 0) {
                float fade = 1f - (Timer - FallEnd) / (float)(MournEnd - FallEnd);
                QueenBeeMotion.WingHum(haloCenter, 0.32f * fade, -0.65f);
            }
            //遗骸下蜜洼漫延
            if (!VaultUtils.isServer && Timer % 6 == 0) {
                PRTLoader.NewParticle<PRT_HoneyDrop>(npc.Center + Main.rand.NextVector2Circular(26f, 8f),
                    Vector2.UnitY * 0.4f, QueenBeeMotion.AmberDeep, Main.rand.NextFloat(0.5f, 0.8f));
            }
        }

        /// <summary>幕六 散场：蜂群放飞离场，随后真死</summary>
        private void UpdateFinale(QueenBeeStateContext context, NPC npc) {
            npc.velocity *= 0.85f;

            if (Timer == MournEnd + 1) {
                //志哀环向外放飞(镖令先出手，标记晚些解除，否则同帧解除后无人消费镖令)
                Vector2 haloCenter = (restPos == Vector2.Zero ? npc.Center : restPos) + new Vector2(0f, -100f);
                context.Swarm.LaunchRadial(0, SwarmDirector.MaxBees - 1, haloCenter, 17f);
                QueenBeeMotion.WingHum(npc.Center, 0.5f, 0.2f);
            }

            //放飞中段解除编队标记：蜂群带着外扬速度回落原版AI自然离场
            if (Timer == MournEnd + 24 && !VaultUtils.isClient) {
                foreach (var bee in context.Swarm.Bees) {
                    if (!bee.active) {
                        continue;
                    }
                    bee.ai[3] = 0f;
                    bee.EncourageDespawn(110);
                    bee.netUpdate = true;
                }
            }
        }
    }
}
