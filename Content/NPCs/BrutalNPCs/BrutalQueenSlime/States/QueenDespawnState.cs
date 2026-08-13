using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.States
{
    /// <summary>脱战撤离：光柱升天渐隐</summary>
    [InnoVault.StateMachines.VaultState((int)QueenSlimeStateIndex.Despawn, typeof(QueenSlimeStateContext))]
    internal class QueenDespawnState : QueenSlimeStateBase
    {
        public override string StateName => "Despawn";
        public override QueenSlimeStateIndex StateIndex => QueenSlimeStateIndex.Despawn;

        public QueenDespawnState() {
        }

        public override void OnEnter(QueenSlimeStateContext context) {
            base.OnEnter(context);
            NPC npc = context.Npc;
            npc.dontTakeDamage = true;
            DisableContactDamage(npc);
            npc.noGravity = true;
            npc.noTileCollide = true;
            if (!VaultUtils.isClient) {
                QueenProjHelper.ClearQueenProjectiles();
            }
        }

        public override IQueenSlimeState OnUpdate(QueenSlimeStateContext context) {
            NPC npc = context.Npc;

            Timer++;
            npc.dontTakeDamage = true;
            DisableContactDamage(npc);

            //撤离前段目标复活/回场则重新接战(服务端)
            if (!VaultUtils.isClient && Timer < 60 && context.Target.Alives()
                && npc.Distance(context.Target.Center) < 2200f) {
                npc.alpha = 0;
                npc.dontTakeDamage = false;
                return context.Phase2Unfolded ? new QueenAerialBalletState() : new QueenBallroomStepState(2);
            }

            //升天加速+渐隐
            float p = MathHelper.Clamp(Timer / 110f, 0f, 1f);
            npc.velocity = Vector2.Lerp(npc.velocity, new Vector2(0f, -14f * p - 2f), 0.08f);
            npc.alpha = (int)(255f * QueenMotion.LateSnap(p, 3));
            context.PoseCommand = context.Phase2Unfolded ? 5 : 1;
            context.WingFlapBoost = 1.2f;
            context.PrismShimmer = p;

            if (Timer == 10) {
                SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.8f, Pitch = 0.5f }, npc.Center);
            }
            if (!VaultUtils.isServer && Timer % 4 == 0) {
                PRTLoader.NewParticle<PRT_Sparkle>(npc.Center + Main.rand.NextVector2Circular(60f, 60f),
                    new Vector2(0f, -Main.rand.NextFloat(1f, 3f)), Color.White, 0.8f)?
                    .Configure(QueenMotion.PrismHue(p), 20, 0.05f, 1.3f);
            }

            if (Timer > 120 && !VaultUtils.isClient) {
                QueenMotion.ShatterAllMinions(npc);
                npc.active = false;
                npc.netUpdate = true;
            }

            return null;
        }
    }

    /// <summary>死亡演出：凝胶失稳→冠落→残翼末升→向心坍缩→绽裂成花瓣与凝胶泉</summary>
    [InnoVault.StateMachines.VaultState((int)QueenSlimeStateIndex.Death, typeof(QueenSlimeStateContext))]
    internal class QueenDeathState : QueenSlimeStateBase
    {
        public override string StateName => "Death";
        public override QueenSlimeStateIndex StateIndex => QueenSlimeStateIndex.Death;

        #region 节奏常量
        private const int DestabilizeTime = 88;   //凝胶失稳
        private const int CrownFallFrame = 90;    //冠落帧
        private const int LastRiseEnd = 196;      //残翼末升(挣扎与哀婉)
        private const int CollapseEnd = 232;      //向心坍缩
        private const int BurstFrame = CollapseEnd + 1; //233 绽裂
        private const int TotalTime = 330;
        #endregion

        public QueenDeathState() {
        }

        public override void OnEnter(QueenSlimeStateContext context) {
            base.OnEnter(context);
            NPC npc = context.Npc;
            context.DeathPerformanceFinished = false;
            npc.dontTakeDamage = true;
            DisableContactDamage(npc);
            if (npc.life < 1) {
                npc.life = 1;
            }

            if (!VaultUtils.isClient) {
                QueenProjHelper.ClearQueenProjectiles();
            }
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.9f, Pitch = -0.6f }, npc.Center);
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.8f, Pitch = -0.5f }, npc.Center);
        }

        public override IQueenSlimeState OnUpdate(QueenSlimeStateContext context) {
            NPC npc = context.Npc;

            //锁血无伤无害
            npc.dontTakeDamage = true;
            DisableContactDamage(npc);
            if (npc.life < 1) {
                npc.life = 1;
            }

            Timer++;

            //随从错帧晶化碎裂(前120帧内，服务端)
            if (!VaultUtils.isClient && Timer <= 120 && Timer % 26 == 12) {
                ShatterOneMinion(context);
            }

            //幕一 失稳：落地打摆漏胶
            if (Timer <= DestabilizeTime) {
                npc.noGravity = false;
                npc.noTileCollide = false;
                npc.velocity.X *= 0.86f;
                float p = Timer / (float)DestabilizeTime;
                context.PushSquash(0.2f * p * (float)Math.Sin(Timer * 0.62f));
                context.PrismShimmer = p * 0.7f;
                context.WingFlapBoost = 0.4f;

                if (!VaultUtils.isServer) {
                    if (Timer % 5 == 0) {
                        QueenMotion.GelSplashBurst(npc.Bottom + Main.rand.NextVector2Circular(30f, 8f), 0.5f, 2);
                    }
                    if (Timer % 14 == 0) {
                        SoundEngine.PlaySound(SoundID.NPCHit1 with {
                            Volume = 0.4f, Pitch = -0.4f + p * 0.3f, MaxInstances = 3
                        }, npc.Center);
                    }
                }
                return null;
            }

            //冠落帧
            if (Timer == CrownFallFrame) {
                if (!Main.dedServ) {
                    Gore.NewGore(npc.GetSource_FromAI(), npc.Center + new Vector2(-40f, -npc.height / 2),
                        new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), -4f), GoreID.QueenSlimeCrown);
                }
                SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.9f, Pitch = -0.2f }, npc.Center);
                context.PushSquash(-0.3f);
            }

            //幕二 残翼末升：奋力振翅只升起少许，随后缓缓沉落
            if (Timer <= LastRiseEnd) {
                float p = (Timer - DestabilizeTime) / (float)(LastRiseEnd - DestabilizeTime);
                npc.noGravity = true;
                npc.noTileCollide = false;
                //翼展随挣扎抖动衰减
                context.WingSpread = MathHelper.Clamp(1f - p * 0.6f + 0.08f * (float)Math.Sin(Timer * 0.8f), 0.2f, 1f);
                context.WingFlapBoost = 1.5f * (1f - p * 0.5f);
                //末升曲线：先升后坠
                float lift = QueenMotion.Bump(p);
                npc.velocity = Vector2.Lerp(npc.velocity, new Vector2(0f, -1.7f * lift + 1.1f * p), 0.07f);
                context.PoseCommand = 1;
                context.PrismShimmer = 0.7f;

                if (!VaultUtils.isServer && Timer % 6 == 0) {
                    PRTLoader.NewParticle<PRT_Sparkle>(npc.Center + Main.rand.NextVector2Circular(50f, 50f),
                        new Vector2(0f, -Main.rand.NextFloat(0.5f, 1.6f)), Color.White, 0.7f)?
                        .Configure(QueenMotion.PrismHue(p), 26, 0.04f, 1.4f);
                }
                return null;
            }

            //幕三 向心坍缩：体积收拢+光尘向心+死寂
            if (Timer <= CollapseEnd) {
                float p = (Timer - LastRiseEnd) / (float)(CollapseEnd - LastRiseEnd);
                npc.velocity *= 0.85f;
                npc.noGravity = true;
                QueenMotion.SetScaleAnchored(npc, MathHelper.Lerp(1f, 0.7f, QueenMotion.SnapOut(p, 3)));
                context.WingSpread = MathHelper.Clamp(0.4f - p * 0.4f, 0f, 1f);
                context.PrismShimmer = 1f;
                context.PoseCommand = 3;

                if (!VaultUtils.isServer && p < 0.8f && Timer % 2 == 0) {
                    QueenMotion.ChargeGatherFX(npc.Center, p, 150f, p);
                }
                return null;
            }

            //绽裂帧
            if (Timer == BurstFrame) {
                DoFinalBurst(context);
            }

            //余韵：隐没等待真死
            npc.alpha = (int)MathHelper.Clamp(npc.alpha + 22, 0f, 255f);
            npc.velocity *= 0.9f;

            //服务端/单人放行真死
            if (Timer >= TotalTime && !VaultUtils.isClient) {
                context.DeathPerformanceFinished = true;
                npc.dontTakeDamage = false;
                QueenMotion.SetScaleAnchored(npc, 1f);
                npc.life = 0;
                npc.HitEffect();
                npc.checkDead();
                npc.netUpdate = true;
            }

            return null;
        }

        /// <summary>随从错帧晶化(服务端，原生死亡链同步演出)</summary>
        private static void ShatterOneMinion(QueenSlimeStateContext context) {
            foreach (var n in Main.ActiveNPCs) {
                if ((n.type == NPCID.QueenSlimeMinionBlue || n.type == NPCID.QueenSlimeMinionPink || n.type == NPCID.QueenSlimeMinionPurple)
                    && (int)n.ai[2] == context.Npc.whoAmI && (int)n.ai[0] != QueenMinionRole.None) {
                    QueenMotion.ScriptKill(n);
                    return;
                }
            }
        }

        /// <summary>绽裂终拍：碎晶花瓣+凝胶泉+棱彩环，全程无害</summary>
        private void DoFinalBurst(QueenSlimeStateContext context) {
            NPC npc = context.Npc;
            npc.alpha = 255;

            if (VaultUtils.isServer) {
                return;
            }

            //晶花瓣放射
            QueenMotion.CrystalShatterBurst(npc.Center, 2.6f, 0f, playSound: false);
            QueenMotion.CrystalShatterBurst(npc.Center + new Vector2(0f, -30f), 1.8f, 0.4f, playSound: false);

            //凝胶喷泉
            for (int i = 0; i < 26; i++) {
                Vector2 vel = new Vector2(Main.rand.NextFloat(-6f, 6f), -Main.rand.NextFloat(4f, 13f));
                PRTLoader.NewParticle<PRT_QueenGelDrop>(npc.Center + Main.rand.NextVector2Circular(30f, 24f),
                    vel, QueenMotion.RoyalPink * 0.9f, Main.rand.NextFloat(0.8f, 1.5f));
            }

            //棱彩三环
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_DWave>(npc.Center, Vector2.Zero,
                    QueenMotion.PrismHue(i * 0.33f) * 0.9f, 0.4f + i * 0.18f)?
                    .Configure(new Vector2(1f, 1f), 0f, 2f + i * 0.6f, 26);
            }

            QueenMotion.Shake(npc.Center, 15f, 36, "QueenDeathBurst");
            SoundEngine.PlaySound(SoundID.NPCDeath64 with { Volume = 1.1f }, npc.Center);
            SoundEngine.PlaySound(SoundID.Shatter with { Volume = 1f, Pitch = -0.3f }, npc.Center);
            SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.8f, Pitch = -0.7f }, npc.Center);
        }
    }
}
