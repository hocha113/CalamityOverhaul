using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.States
{
    /// <summary>
    /// 死亡演出：风雪骤停(整场第一次澄澈)→挣扎起身，独眼明灭、躯体冰裂→
    /// 断裂的嘶吼→轰然前扑砸地(全场冰刺齐碎)→自足向上化作雪与暗影消散
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)DeerclopsStateIndex.Death, typeof(DeerclopsStateContext))]
    internal class DeerclopsDeathState : DeerclopsStateBase
    {
        public override string StateName => "Death";
        public override DeerclopsStateIndex StateIndex => DeerclopsStateIndex.Death;

        //演出节拍(帧)——运镜与玩家侧对齐这些常量
        internal const int StaggerEnd = 70;
        internal const int GazeEnd = 190;
        internal const int ImpactFrame = 230;
        internal const int CollapseEnd = 260;
        internal const int TotalTime = 430;

        public override void OnEnter(DeerclopsStateContext context) {
            base.OnEnter(context);
            context.DeathPerformanceFinished = false;

            NPC npc = context.Npc;
            npc.dontTakeDamage = true;
            npc.damage = 0;
            if (npc.life < 1) {
                npc.life = 1;
            }
            DeerclopsAI.ClearHostileProjectiles();

            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.DeerclopsHit with { Volume = 1.3f, Pitch = -0.6f }, npc.Center);
            }
        }

        public override IDeerclopsState OnUpdate(DeerclopsStateContext context) {
            NPC npc = context.Npc;
            Timer++;

            //全程锁血无害
            npc.dontTakeDamage = true;
            npc.damage = 0;
            if (npc.life < 1) {
                npc.life = 1;
            }
            context.HaltMovement = true;

            //幕一：踉跪，风雪骤停——整场战斗第一次澄澈
            if (Timer <= StaggerEnd) {
                context.VeilTarget = 0.03f;
                context.AnimMode = DeerAnimMode.Crouch;
                context.EyeGlow = 0.8f;
                context.EyeHeat = 0.9f;

                if (Timer == 8) {
                    DeerclopsMotion.CameraPunch(npc.Bottom, 5f, 16, "DeerDeathKneel", Vector2.UnitY);
                    DeerclopsPerformancePlayer.RequestShake(4f, 16);
                    SpawnGroundPuff(npc, 12);
                }
                //心跳般的两声闷响
                if ((Timer == 34 || Timer == 58) && !Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.DeerclopsStep with { Volume = 0.9f, Pitch = -0.8f }, npc.Center);
                }
                return null;
            }

            //幕二：最后的凝视——挣扎起身，独眼明灭，躯体冰裂
            if (Timer <= GazeEnd) {
                context.VeilTarget = 0.03f;
                context.AnimMode = DeerAnimMode.Roar;
                context.AnimTimer = (Timer - StaggerEnd) / 3;

                //独眼失稳明灭(确定性闪烁)
                float flicker = (float)Math.Sin(Timer * 0.7f) * (float)Math.Sin(Timer * 0.23f + 1.7f);
                context.EyeGlow = MathHelper.Clamp(0.55f + flicker * 0.45f, 0.05f, 1f);
                context.EyeHeat = 1f;

                //躯体冰裂三响
                if ((Timer == 96 || Timer == 128 || Timer == 158) && !Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item27 with { Volume = 1.1f, Pitch = -0.6f }, npc.Center);
                    for (int i = 0; i < 8; i++) {
                        Dust dust = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.Ice,
                            Main.rand.NextFloat(-1.5f, 1.5f), -Main.rand.NextFloat(0.5f, 2f), 80, default, Main.rand.NextFloat(1f, 1.6f));
                        dust.noGravity = Main.rand.NextBool();
                    }
                }
                //断裂的嘶吼：起调即被掐断
                if (Timer == 162 && !Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.DeerclopsScream with { Volume = 1.1f, Pitch = -0.55f }, npc.Center);
                }
                if (Timer == 176 && !Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.5f, Pitch = -0.9f }, npc.Center);
                }
                return null;
            }

            //幕三：前扑轰塌
            if (Timer <= CollapseEnd) {
                context.VeilTarget = 0.03f;
                context.AnimMode = DeerAnimMode.Crouch;
                float fallT = MathHelper.Clamp((Timer - GazeEnd) / (float)(ImpactFrame - GazeEnd), 0f, 1f);
                //poly(3)前扑——起慢坠快
                context.BodyLean = fallT * fallT * fallT * 0.62f;
                context.EyeGlow = 0.9f;

                if (Timer == ImpactFrame) {
                    DoImpact(context);
                }
                return null;
            }

            //幕四：寂静消散——自足向上化雪
            context.VeilTarget = 0f;
            context.AnimMode = DeerAnimMode.Crouch;
            context.BodyLean = 0.62f;
            context.Dissolve = MathHelper.Clamp((Timer - CollapseEnd) / 140f, 0f, 1f);
            //最后的眼芒：将熄未熄，在370帧回光一瞬后彻底黯灭
            float dieOut = 1f - MathHelper.Clamp((Timer - CollapseEnd) / 110f, 0f, 1f);
            context.EyeGlow = Timer > 360 && Timer < 378 ? 0.9f : dieOut * 0.5f;
            context.EyeHeat = dieOut;

            //身形化作上升的雪与影(本端)
            if (!Main.dedServ && Timer % 2 == 0) {
                float riseBias = context.Dissolve;
                Vector2 pos = npc.position + new Vector2(Main.rand.NextFloat(npc.width), Main.rand.NextFloat(npc.height * (1f - riseBias * 0.7f)));
                if (Main.rand.NextBool(3)) {
                    Dust shadow = Dust.NewDustPerfect(pos, DustID.Shadowflame, -Vector2.UnitY * Main.rand.NextFloat(1f, 2.5f), 140, default, Main.rand.NextFloat(0.9f, 1.4f));
                    shadow.noGravity = true;
                }
                else {
                    Dust snow = Dust.NewDustPerfect(pos, DustID.Snow, -Vector2.UnitY * Main.rand.NextFloat(0.5f, 2f), 110, default, Main.rand.NextFloat(1f, 1.8f));
                    snow.noGravity = true;
                }
            }

            //放行真死(服务端/单机)
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

        /// <summary>轰然砸地：全场唯一的大震+冰刺齐碎+雪爆</summary>
        private static void DoImpact(DeerclopsStateContext context) {
            NPC npc = context.Npc;

            //整场唯一一次大震(运镜接管时走运镜震动)
            DeerclopsMotion.CameraPunch(npc.Bottom, 17f, 40, "DeerDeathImpact", Vector2.UnitY);
            DeerclopsPerformancePlayer.RequestShake(16f, 40);

            if (!VaultUtils.isClient) {
                //全场冰刺殉碎(服务端裁决，OnKill在各端补碎冰)
                int spikeType = ModContent.ProjectileType<DeerIceSpikeProj>();
                foreach (Projectile proj in Main.ActiveProjectiles) {
                    if (proj.type == spikeType) {
                        proj.Kill();
                    }
                }
            }

            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.DeerclopsDeath with { Volume = 1.4f }, npc.Center);
            SoundEngine.PlaySound(SoundID.Item70 with { Volume = 0.9f, Pitch = -0.6f }, npc.Bottom);

            SpawnGroundPuff(npc, 30);
            for (int i = 0; i < 20; i++) {
                PRTLoader.NewParticle<PRT_ATShard>(npc.Bottom + new Vector2(Main.rand.NextFloat(-90f, 90f), -Main.rand.NextFloat(0f, 30f)),
                    new Vector2(Main.rand.NextFloat(-5f, 5f), -Main.rand.NextFloat(2f, 8f)),
                    DeerclopsMotion.IceBlue * 0.9f, Main.rand.NextFloat(0.3f, 0.6f))
                    .Configure(Main.rand.Next(20, 34), Main.rand.NextFloat(-0.3f, 0.3f));
            }
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_Smoke>(npc.Bottom + new Vector2(Main.rand.NextFloat(-70f, 70f), 0f),
                    new Vector2(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(0.5f, 1.5f)),
                    DeerclopsMotion.ColdWhite * 0.55f, Main.rand.NextFloat(1f, 1.6f))
                    .Configure(Main.rand.Next(36, 60), 0.65f, Main.rand.NextFloat(-0.05f, 0.05f));
            }
        }

        private static void SpawnGroundPuff(NPC npc, int count) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < count; i++) {
                Dust dust = Dust.NewDustPerfect(npc.Bottom + new Vector2(Main.rand.NextFloat(-80f, 80f), 0f),
                    DustID.Snow, new Vector2(Main.rand.NextFloat(-3f, 3f), -Main.rand.NextFloat(1f, 5f)), 70, default, Main.rand.NextFloat(1.2f, 2.2f));
                dust.noGravity = Main.rand.NextBool(3);
            }
        }
    }
}
