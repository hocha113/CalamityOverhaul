using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Rendering;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.States
{
    /// <summary>诅咒崩解死亡演出：坠地熄火→亡者悲鸣→万手告别→诅咒剥离→崩解新星</summary>
    [InnoVault.StateMachines.VaultState((int)SkeletronStateIndex.Death, typeof(SkeletronStateContext))]
    internal class SkeletronDeathState : SkeletronStateBase
    {
        public override string StateName => "Death";
        public override SkeletronStateIndex StateIndex => SkeletronStateIndex.Death;

        #region 时间线常量
        internal const int FallEnd = 70;        //坠地熄火
        internal const int LamentEnd = 150;     //亡者悲鸣（钟声渐急）
        internal const int CradleEnd = 255;     //万手自黑暗中伸来
        internal const int StripEnd = 330;      //诅咒被缓缓拔出
        internal const int SilenceEnd = 352;    //收束死寂
        internal const int NovaFrame = 352;     //崩解新星（全场唯一冲击帧）
        internal const int DeathEnd = 470;
        #endregion

        private bool landed;
        private bool novaDone;
        private float landY;

        public override void OnEnter(SkeletronStateContext context) {
            base.OnEnter(context);
            NPC npc = context.Npc;

            landed = false;
            novaDone = false;
            landY = -1f;

            npc.ai[SkeletronAiSlots.HeadPhase] = SkeletronPhase.DeathShow;
            npc.velocity *= 0.4f;
            context.DeathTimer = 0;
            SkeletronHeadAI.ActivePerformanceHead = npc.whoAmI;

            //公平阀：清空敌对弹幕
            SkeletronFacts.ClearHostileProjectiles();

            //清 debuff
            for (int i = 0; i < npc.buffType.Length; i++) {
                npc.buffTime[i] = 0;
            }

            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCDeath2 with { Volume = 0.9f, Pitch = -0.8f }, npc.Center);
            }
        }

        public override ISkeletronState OnUpdate(SkeletronStateContext context) {
            NPC npc = context.Npc;

            npc.dontTakeDamage = true;
            npc.damage = 0;
            if (npc.life < 1) {
                npc.life = 1;
            }
            context.DeathTimer = Timer;

            //黑暗领域随演出压近
            float domain = MathHelper.Clamp((Timer - FallEnd) / 90f, 0f, 0.7f);
            if (Timer > NovaFrame) {
                domain = MathHelper.Lerp(0.7f, 0f, (Timer - NovaFrame) / (float)(DeathEnd - NovaFrame));
            }
            SkeletronScreenEffects.RequestDomain(domain);

            if (Timer < FallEnd) {
                UpdateFall(context, npc);
            }
            else if (Timer < LamentEnd) {
                UpdateLament(context, npc);
            }
            else if (Timer < CradleEnd) {
                UpdateCradle(context, npc);
            }
            else if (Timer < StripEnd) {
                UpdateStrip(context, npc);
            }
            else {
                UpdateNovaAndSettle(context, npc);
            }

            Timer++;

            //落幕
            if (Timer >= DeathEnd) {
                context.DeathPerformanceFinished = true;
                if (SkeletronHeadAI.ActivePerformanceHead == npc.whoAmI) {
                    SkeletronHeadAI.ActivePerformanceHead = -1;
                }
                if (!VaultUtils.isClient) {
                    npc.dontTakeDamage = false;
                    npc.life = 0;
                    npc.HitEffect();
                    npc.checkDead();
                    npc.netUpdate = true;
                }
            }
            return null;
        }

        public override void OnExit(SkeletronStateContext context) {
            base.OnExit(context);
            if (SkeletronHeadAI.ActivePerformanceHead == context.Npc.whoAmI) {
                SkeletronHeadAI.ActivePerformanceHead = -1;
            }
        }

        #region 各阶段

        /// <summary>坠地熄火：眼火余烬熄灭，颅骨自由坠落磕在地上</summary>
        private void UpdateFall(SkeletronStateContext context, NPC npc) {
            context.EyeFlame = MathHelper.Clamp(1f - Timer / 26f, 0f, 1f);
            context.CrownFlame = 0f;

            if (landY < 0f) {
                landY = SkeletronFacts.FindGroundY(npc.Center, 90);
                if (landY < 0f) {
                    landY = npc.Center.Y + 700f;
                }
            }

            if (!landed) {
                npc.velocity.X *= 0.97f;
                npc.velocity.Y += 0.34f;
                if (npc.velocity.Y > 13f) {
                    npc.velocity.Y = 13f;
                }
                //歪斜倾覆
                npc.rotation += 0.011f;

                if (npc.Center.Y >= landY - 44f) {
                    landed = true;
                    npc.Center = new Vector2(npc.Center.X, landY - 44f);
                    npc.velocity = Vector2.Zero;
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 1f, Pitch = -0.6f }, npc.Center);
                        SoundEngine.PlaySound(SoundID.Item35 with { Volume = 1f, Pitch = -0.9f }, npc.Center);
                        SkeletronScreenEffects.PushShake(npc.Center, 7f);
                        for (int i = 0; i < 14; i++) {
                            Dust dust = Dust.NewDustDirect(npc.BottomLeft - new Vector2(0f, 12f), npc.width, 12, DustID.Bone,
                                Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-2.5f, 0f), 140, default, 1.5f);
                            dust.noGravity = false;
                        }
                    }
                }
            }
            else {
                npc.velocity = Vector2.Zero;
                npc.rotation = npc.rotation.AngleLerp(0.22f, 0.06f);
            }
        }

        /// <summary>亡者悲鸣：死寂里裂缝渗出幽魂，钟声渐急</summary>
        private void UpdateLament(SkeletronStateContext context, NPC npc) {
            npc.velocity = Vector2.Zero;
            context.EyeFlame = 0f;

            if (!VaultUtils.isServer) {
                //钟声渐急
                if (Timer == 96 || Timer == 128 || Timer == 146) {
                    float pitch = -0.85f + (Timer - 96) * 0.004f;
                    SoundEngine.PlaySound(SoundID.Item35 with { Volume = 0.95f, Pitch = pitch }, npc.Center);
                }
                //裂缝渗魂
                if (Timer % 7 == 0) {
                    Vector2 pos = npc.Center + Main.rand.NextVector2Circular(npc.width * 0.4f, npc.height * 0.4f);
                    PRTLoader.NewParticle<PRT_SkeleGhostFlame>(pos,
                        new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(-2.4f, -1f)),
                        SkeletronRenderHelper.GhostDeep, Main.rand.NextFloat(1f, 1.8f))?.Configure(Main.rand.Next(30, 50));
                }
            }
        }

        /// <summary>万手告别：八只幽灵手自黑暗中伸来环抱颅骨</summary>
        private void UpdateCradle(SkeletronStateContext context, NPC npc) {
            npc.velocity = Vector2.Zero;

            if (Timer == LamentEnd && !VaultUtils.isClient) {
                int cradleLife = DeathEnd - LamentEnd + 20;
                for (int i = 0; i < 8; i++) {
                    float angle = MathHelper.TwoPi * i / 8f + 0.2f;
                    Vector2 pos = npc.Center + angle.ToRotationVector2() * SkeletronGhostArmProj.CradleRadius;
                    int proj = Projectile.NewProjectile(npc.GetSource_FromAI(), pos, Vector2.Zero,
                        ModContent.ProjectileType<SkeletronGhostArmProj>(), 0, 0f, Main.myPlayer,
                        (float)SkeletronGhostArmProj.ArmMode.DeathCradle, angle, i * 6f);
                    if (proj >= 0 && proj < Main.maxProjectiles) {
                        Main.projectile[proj].timeLeft = cradleLife;
                        Main.projectile[proj].netUpdate = true;
                    }
                }
                npc.netUpdate = true;
            }
            if (Timer == LamentEnd + 4 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.85f, Pitch = -0.75f }, npc.Center);
            }

            //被环抱的颅骨微微离地
            float t = MathHelper.Clamp((Timer - (LamentEnd + 60)) / 45f, 0f, 1f);
            if (t > 0f) {
                npc.Center = new Vector2(npc.Center.X, landY - 44f - t * 46f);
                npc.rotation = npc.rotation.AngleLerp(0f, 0.05f);
                //颤抖
                npc.position += Main.rand.NextVector2Circular(1.1f, 1.1f) * t;
            }
        }

        /// <summary>诅咒剥离：青焰法体自颅顶被缓缓拔出，骨壳剥落</summary>
        private void UpdateStrip(SkeletronStateContext context, NPC npc) {
            float t = (Timer - CradleEnd) / (float)(StripEnd - CradleEnd);
            npc.velocity = Vector2.Zero;
            npc.position += Main.rand.NextVector2Circular(1.6f, 1.6f) * (0.4f + t);

            //剥离的诅咒：头顶向心涡流增强（绘制层消费）
            context.SpinVortex = t;
            context.VortexConverge = 1f;
            context.EyeFlame = t * 0.7f;

            if (!VaultUtils.isServer) {
                //幽焰柱升腾
                if (Timer % 3 == 0) {
                    PRTLoader.NewParticle<PRT_SkeleGhostFlame>(
                        npc.Center + new Vector2(Main.rand.NextFloat(-26f, 26f), -30f),
                        new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), -(2.2f + t * 4.4f)),
                        SkeletronRenderHelper.GhostCyan, Main.rand.NextFloat(1.5f, 2.6f))?.Configure(Main.rand.Next(28, 44));
                }
                //骨壳剥落
                if (Timer % 5 == 0) {
                    PRTLoader.NewParticle<PRT_SkeleBoneChip>(
                        npc.Center + Main.rand.NextVector2Circular(npc.width * 0.42f, npc.height * 0.42f),
                        new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-1f, 0.6f)),
                        Color.White, Main.rand.NextFloat(0.6f, 1.1f))?.Configure(Main.rand.Next(40, 70));
                }
                if (Timer == CradleEnd + 8) {
                    SoundEngine.PlaySound(SoundID.ForceRoarPitched with { Volume = 0.8f, Pitch = -0.9f }, npc.Center);
                }
            }
        }

        /// <summary>收束死寂→崩解新星→尘埃落定</summary>
        private void UpdateNovaAndSettle(SkeletronStateContext context, NPC npc) {
            npc.velocity = Vector2.Zero;

            //死寂：一切收拢（新星前的吸气）
            if (Timer < NovaFrame) {
                float t = (Timer - StripEnd) / (float)(SilenceEnd - StripEnd);
                context.SpinVortex = 1f - t * 0.6f;
                context.VortexConverge = 1f;
                context.EyeFlame = 0f;
                return;
            }

            //崩解新星（全场唯一冲击帧）
            if (!novaDone) {
                novaDone = true;
                context.SpinVortex = 0f;
                context.VortexConverge = 0f;
                if (!VaultUtils.isServer) {
                    SkeletronScreenEffects.PushBoneFlash(1f, 30);
                    SkeletronScreenEffects.PushShockRing(npc.Center, 1.2f, 980f, 34);
                    SkeletronScreenEffects.PushShake(npc.Center, 14f);
                    SoundEngine.PlaySound(SoundID.NPCDeath10 with { Volume = 1.1f, Pitch = -0.55f }, npc.Center);
                    SoundEngine.PlaySound(SoundID.Item35 with { Volume = 1.1f, Pitch = -0.4f }, npc.Center);

                    for (int i = 0; i < 34; i++) {
                        PRTLoader.NewParticle<PRT_SkeleBoneChip>(npc.Center + Main.rand.NextVector2Circular(30f, 30f),
                            Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(4f, 12f),
                            Color.White, Main.rand.NextFloat(0.7f, 1.3f))?.Configure(Main.rand.Next(50, 90));
                    }
                    for (int i = 0; i < 40; i++) {
                        PRTLoader.NewParticle<PRT_SkeleGhostFlame>(npc.Center + Main.rand.NextVector2Circular(40f, 40f),
                            Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(5f, 11f),
                            SkeletronRenderHelper.GhostCyan, Main.rand.NextFloat(1.8f, 3f))?.Configure(Main.rand.Next(30, 52));
                    }
                }
                if (!VaultUtils.isClient) {
                    SkeletronHeadAI.Announce(SkeletronHeadAI.Death_Text, SkeletronRenderHelper.GhostDeep);
                }
            }

            //骨壳崩解淡出
            npc.alpha = Math.Min(npc.alpha + 5, 220);
            context.EyeFlame = 0f;

            //残烟
            if (!VaultUtils.isServer && Timer % 8 == 0 && Timer < DeathEnd - 30) {
                Vector2 pos = npc.Center + Main.rand.NextVector2Circular(npc.width * 0.5f, npc.height * 0.5f);
                PRTLoader.NewParticle<PRT_SkeleGhostFlame>(pos,
                    new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(-1.6f, -0.6f)),
                    SkeletronRenderHelper.GhostDeep, Main.rand.NextFloat(0.9f, 1.5f))?.Configure(Main.rand.Next(30, 50));
            }
        }

        #endregion
    }
}
