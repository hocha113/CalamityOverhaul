using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Rendering;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.States
{
    /// <summary>脱战：王冠先坠，身体黯淡融成一滩渗地而去</summary>
    [InnoVault.StateMachines.VaultState((int)KingSlimeStateIndex.Despawn, typeof(KingSlimeStateContext))]
    internal class KingSlimeDespawnState : KingSlimeStateBase
    {
        public override string StateName => "Despawn";
        public override KingSlimeStateIndex StateIndex => KingSlimeStateIndex.Despawn;

        private const int MeltTime = 150;

        public override void OnEnter(KingSlimeStateContext context) {
            base.OnEnter(context);
            context.Npc.dontTakeDamage = true;

            //悬浮王冠改作坠地演出
            if (!VaultUtils.isClient) {
                Projectile crown = context.FindCrown();
                if (crown != null) {
                    crown.ai[1] = BKSCrownProj.ModeDeathDrop;
                    crown.damage = 0;
                    crown.netUpdate = true;
                }
            }
        }

        public override IKingSlimeState OnUpdate(KingSlimeStateContext context) {
            NPC npc = context.Npc;
            Timer++;

            context.ContactDamageScale = 0f;
            npc.velocity.X *= 0.85f;
            npc.dontTakeDamage = true;

            float t = MathHelper.Clamp(Timer / (float)MeltTime, 0f, 1f);
            context.VisualSquash = MathHelper.Lerp(context.VisualSquash, 1f - 0.75f * t, 0.16f);
            context.BodyOpacity = 1f - t * 0.9f;
            context.AuraMode = 0;
            context.AuraProgress = 0f;

            if (!VaultUtils.isServer) {
                if ((int)Timer % 6 == 0) {
                    KingSlimeGelFX.BubbleFizz(npc.Bottom - new Vector2(0f, 8f), npc.width * 0.4f * (1f - t * 0.5f), 1);
                }
                if ((int)Timer == 20) {
                    SoundEngine.PlaySound(SoundID.Drown with { Pitch = -0.6f, Volume = 0.8f }, npc.Center);
                }
            }

            if (Timer > MeltTime + 20) {
                if (!VaultUtils.isClient) {
                    npc.active = false;
                    npc.netUpdate = true;
                }
            }

            return null;
        }
    }

    /// <summary>
    /// 死亡演出：剧痛崩漏→三次成形挣扎(一次比一次无力)→体内忍者破体逃逸→终融成滩放行真死
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)KingSlimeStateIndex.Death, typeof(KingSlimeStateContext))]
    internal class KingSlimeDeathState : KingSlimeStateBase
    {
        public override string StateName => "Death";
        public override KingSlimeStateIndex StateIndex => KingSlimeStateIndex.Death;

        #region 节拍(运镜对齐)
        internal const int ActAgonyEnd = 70;
        internal const int ActStruggleEnd = 180;
        internal const int NinjaEscapeFrame = 212;
        internal const int ActNinjaEnd = 234;
        internal const int ActMeltEnd = 326;
        #endregion

        //挣扎脉冲帧与峰值(相对幕二起点)
        private static readonly int[] PulseFrames = [14, 50, 86];
        private static readonly float[] PulsePeaks = [1.42f, 1.24f, 1.08f];

        private bool crownDropped;
        private bool poolSpawned;
        private bool ninjaFled;
        private bool textShown;

        public override void OnEnter(KingSlimeStateContext context) {
            base.OnEnter(context);
            NPC npc = context.Npc;
            context.DeathPerformanceFinished = false;
            npc.dontTakeDamage = true;
            npc.damage = 0;
            if (npc.life < 1) {
                npc.life = 1;
            }
            crownDropped = false;
            poolSpawned = false;
            ninjaFled = false;
            textShown = false;
            KingSlimeAI.ActivePerformanceIndex = npc.whoAmI;

            SoundEngine.PlaySound(SoundID.NPCDeath1 with { Pitch = -0.7f, Volume = 1.1f }, npc.Center);
            SoundEngine.PlaySound(SoundID.QueenSlime with { Pitch = -0.75f, Volume = 0.9f }, npc.Center);
        }

        public override IKingSlimeState OnUpdate(KingSlimeStateContext context) {
            NPC npc = context.Npc;
            Timer++;

            //锁血无伤无害
            npc.dontTakeDamage = true;
            context.ContactDamageScale = 0f;
            if (npc.life < 1) {
                npc.life = 1;
            }
            npc.velocity.X *= 0.86f;

            if (Timer <= ActAgonyEnd) {
                UpdateAgony(context);
            }
            else if (Timer <= ActStruggleEnd) {
                UpdateStruggle(context);
            }
            else if (Timer <= ActNinjaEnd) {
                UpdateNinjaEscape(context);
            }
            else if (Timer <= ActMeltEnd) {
                UpdateFinalMelt(context);
            }
            else {
                FinishDeath(context);
            }

            return null;
        }

        /// <summary>幕一：剧痛，王冠熄光坠地，体表崩漏渐密</summary>
        private void UpdateAgony(KingSlimeStateContext context) {
            NPC npc = context.Npc;

            //王冠坠地(悬浮冠改演出坠落；P1无悬浮冠则弹出一顶)
            if (!crownDropped && Timer >= 6) {
                crownDropped = true;
                if (!VaultUtils.isClient) {
                    Projectile crown = context.FindCrown();
                    if (crown != null) {
                        crown.ai[1] = BKSCrownProj.ModeDeathDrop;
                        crown.damage = 0;
                        crown.velocity = new Vector2(Main.rand.NextFloat(-2f, 2f), -3f);
                        crown.netUpdate = true;
                    }
                    else {
                        int idx = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Top + new Vector2(0f, -6f),
                            new Vector2(Main.rand.NextFloat(-2f, 2f), -3.5f),
                            ModContent.ProjectileType<BKSCrownProj>(), 0, 0f, Main.myPlayer,
                            npc.whoAmI, BKSCrownProj.ModeDeathDrop);
                        if (idx >= 0 && idx < Main.maxProjectiles) {
                            Main.projectile[idx].damage = 0;
                        }
                    }
                }
                KingSlimeGelFX.CrownChime(npc.Top, -0.4f, 0.9f);
            }

            //剧痛颤抖+崩漏
            float t = Timer / (float)ActAgonyEnd;
            if ((int)Timer % 12 == 0) {
                context.ImpactSquash(0.1f + t * 0.08f);
            }
            if (!VaultUtils.isServer) {
                if ((int)Timer % 5 == 0) {
                    Vector2 leak = npc.Center + Main.rand.NextVector2Circular(npc.width * 0.42f, npc.height * 0.42f);
                    KingSlimeGelFX.GelSplatter(leak, Main.rand.NextVector2Unit(), 2, 3.5f + t * 3f, 0.8f);
                }
                if ((int)Timer % 8 == 0) {
                    KingSlimeGelFX.BubbleFizz(npc.Center, npc.width * 0.5f, 2);
                }
            }
        }

        /// <summary>幕二：三次成形挣扎，一次比一次无力，体积渐失</summary>
        private void UpdateStruggle(KingSlimeStateContext context) {
            NPC npc = context.Npc;
            int t = (int)Timer - ActAgonyEnd;

            //挣扎脉冲：起塔尝试，峰值一次比一次弱
            for (int i = 0; i < 3; i++) {
                if (t == PulseFrames[i]) {
                    context.SquashVelocity += (PulsePeaks[i] - 1f) * 0.9f;
                    KingSlimeGelFX.SquishSound(npc.Center, -0.3f - i * 0.12f, 0.9f - i * 0.15f);
                    KingSlimeGelFX.CameraPunch(npc.Center, 2.6f - i * 0.5f, 10, "BKSDeathStruggle");
                }
                //塌落拍
                if (t == PulseFrames[i] + 16) {
                    context.SquashVelocity -= 0.3f - i * 0.06f;
                    if (!VaultUtils.isServer) {
                        KingSlimeGelFX.LandingBurst(npc.Bottom, 9f - i * 2f, 1f);
                    }
                }
            }

            //体积流失
            context.ScaleMul = MathHelper.Lerp(context.ScaleMul, 0.82f, 0.012f);
            context.BodyOpacity = MathHelper.Lerp(context.BodyOpacity, 0.85f, 0.02f);

            //脚下凝胶池渐积(纯演出，零伤害)
            if (!poolSpawned && t == 30 && !VaultUtils.isClient) {
                poolSpawned = true;
                Vector2 ground = KingSlimeGelFX.FindGroundBelow(npc.Bottom + new Vector2(0f, -8f));
                Projectile.NewProjectile(npc.GetSource_FromAI(), ground, Vector2.Zero,
                    ModContent.ProjectileType<BKSGelPoolProj>(), 0, 0f, Main.myPlayer,
                    300f, 400f);
            }

            if (!VaultUtils.isServer && (int)Timer % 6 == 0) {
                KingSlimeGelFX.BubbleFizz(npc.Bottom - new Vector2(0f, 10f), npc.width * 0.5f, 2);
            }
        }

        /// <summary>幕三：忍者破体逃逸(彩蛋致敬：吞下的忍者获救)</summary>
        private void UpdateNinjaEscape(KingSlimeStateContext context) {
            NPC npc = context.Npc;

            //忍者剪影挣扎发亮
            float t = MathHelper.Clamp(((int)Timer - ActStruggleEnd) / (float)(NinjaEscapeFrame - ActStruggleEnd), 0f, 1f);
            if (!ninjaFled) {
                context.NinjaGlow = t;
                if ((int)Timer % 9 == 0) {
                    context.ImpactSquash(0.08f);
                }
            }

            if ((int)Timer == NinjaEscapeFrame) {
                ninjaFled = true;
                context.NinjaGone = true;
                context.NinjaGlow = 0f;
                //破体爆胶
                context.SquashVelocity -= 0.35f;
                SoundEngine.PlaySound(SoundID.NPCDeath1 with { Pitch = -0.2f, Volume = 1f }, npc.Center);
                SoundEngine.PlaySound(SoundID.Item7 with { Pitch = 0.4f, Volume = 0.9f }, npc.Center);
                if (!VaultUtils.isServer) {
                    KingSlimeGelFX.GelSplatter(npc.Center, -Vector2.UnitY, 16, 9f, 1.2f);
                }
                //逃逸忍者(纯演出)
                if (!VaultUtils.isClient) {
                    int dir = npc.Center.X < Main.maxTilesX * 8f ? -1 : 1;
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center,
                        new Vector2(dir * 9.5f, -5f),
                        ModContent.ProjectileType<BKSNinjaProj>(), 0, 0f, Main.myPlayer,
                        npc.whoAmI, 3f);
                }
            }

            if (ninjaFled && !textShown && (int)Timer == NinjaEscapeFrame + 12) {
                textShown = true;
                if (!VaultUtils.isServer) {
                    VaultUtils.Text(KingSlimeAI.NinjaFreed_Text.Value, KingSlimeGelFX.GelFoam);
                }
            }
        }

        /// <summary>幕四：失去核心，彻底融成一滩</summary>
        private void UpdateFinalMelt(KingSlimeStateContext context) {
            NPC npc = context.Npc;
            float t = MathHelper.Clamp(((int)Timer - ActNinjaEnd) / (float)(ActMeltEnd - ActNinjaEnd), 0f, 1f);

            context.NinjaGone = true;
            context.VisualSquash = MathHelper.Lerp(context.VisualSquash, 1f - 0.78f * t, 0.1f);
            context.BodyOpacity = MathHelper.Lerp(0.85f, 0.15f, t);
            context.ScaleMul = MathHelper.Lerp(context.ScaleMul, 0.6f, 0.02f);

            if (!VaultUtils.isServer) {
                //末段稀落泡音
                if ((int)Timer % 14 == 0 && Main.rand.NextBool(2)) {
                    KingSlimeGelFX.BubbleFizz(npc.Bottom - new Vector2(0f, 6f), npc.width * 0.4f, 1);
                    SoundEngine.PlaySound(SoundID.Drown with {
                        Pitch = 0.2f + t * 0.3f, Volume = 0.5f * (1f - t * 0.5f), MaxInstances = 2
                    }, npc.Center);
                }
            }
        }

        /// <summary>放行真死：掉落走原版</summary>
        private void FinishDeath(KingSlimeStateContext context) {
            NPC npc = context.Npc;
            KingSlimeAI.ActivePerformanceIndex = -1;
            if (VaultUtils.isClient) {
                return;
            }
            context.DeathPerformanceFinished = true;
            npc.dontTakeDamage = false;
            npc.life = 0;
            npc.HitEffect();
            npc.checkDead();
            npc.netUpdate = true;
        }

        public override void OnExit(KingSlimeStateContext context) {
            base.OnExit(context);
            KingSlimeAI.ActivePerformanceIndex = -1;
        }
    }
}
