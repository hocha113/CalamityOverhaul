using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.Rendering;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.Projectiles
{
    /// <summary>
    /// 蜂巢炮台：定点持续威胁；生长→周期预警脉冲→三连毒刺齐射→到期萎缩<br/>
    /// ai[0]=死亡模式加成(0/1)
    /// </summary>
    internal class WaxHiveTurret : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>总存活帧(12s)</summary>
        internal const int LifeTime = 720;
        /// <summary>成长帧</summary>
        private const int GrowTime = 30;
        /// <summary>齐射周期</summary>
        private const int VolleyInterval = 90;
        /// <summary>齐射前预警帧</summary>
        private const int WarnTime = 36;
        /// <summary>萎缩帧</summary>
        private const int DecayTime = 24;
        /// <summary>公平阀：齐射相邻毒刺角步进，3~4连扇内恒有可穿行角缝；齐射前36帧升调滴答预警</summary>
        private const float VolleySpreadStep = 0.09f;

        private bool DeathBoost => Projectile.ai[0] == 1f;
        private float LivedFrames => LifeTime - Projectile.timeLeft;

        public override void SetDefaults() {
            Projectile.width = 44;
            Projectile.height = 44;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeTime;
            Projectile.aiStyle = -1;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            float lived = LivedFrames;

            //生长期：蜡屑迸出
            if (lived < GrowTime) {
                if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                    PRTLoader.NewParticle<PRT_WaxChip>(
                        Projectile.Center + Main.rand.NextVector2Circular(16f, 16f),
                        Main.rand.NextVector2Circular(2f, 1f) - Vector2.UnitY * Main.rand.NextFloat(1f, 3f),
                        QueenBeeMotion.WaxPale, Main.rand.NextFloat(0.7f, 1.1f));
                }
                if (lived == 1f) {
                    SoundEngine.PlaySound(SoundID.NPCHit18 with { Volume = 0.7f, Pitch = -0.4f }, Projectile.Center);
                }
                return;
            }

            //萎缩期不再齐射
            if (Projectile.timeLeft <= DecayTime) {
                if (!VaultUtils.isServer && Main.rand.NextBool(4)) {
                    PRTLoader.NewParticle<PRT_HoneyDrop>(
                        Projectile.Center + Main.rand.NextVector2Circular(12f, 12f),
                        Vector2.UnitY * Main.rand.NextFloat(0.5f, 1.5f),
                        QueenBeeMotion.AmberDeep, Main.rand.NextFloat(0.5f, 0.8f));
                }
                return;
            }

            //齐射节拍
            int cycleT = (int)lived % VolleyInterval;
            int fireFrame = VolleyInterval - 1;
            int warnStart = fireFrame - WarnTime;

            //预警升调滴答
            if (cycleT >= warnStart && cycleT < fireFrame && (cycleT - warnStart) % 12 == 0) {
                float warnP = (cycleT - warnStart) / (float)WarnTime;
                SoundEngine.PlaySound(SoundID.Item17 with {
                    Volume = 0.32f + warnP * 0.2f,
                    Pitch = -0.5f + warnP * 0.7f,
                    MaxInstances = 4
                }, Projectile.Center);
            }

            //齐射帧
            if (cycleT == fireFrame) {
                FireVolley();
            }

            Lighting.AddLight(Projectile.Center, QueenBeeMotion.HoneyGold.ToVector3() * 0.4f);
        }

        /// <summary>三连毒刺，锁最近玩家(服务端权威)</summary>
        private void FireVolley() {
            Player target = FindNearestPlayer(1500f);
            if (target == null) {
                return;
            }

            SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.85f, Pitch = 0.15f }, Projectile.Center);
            if (!VaultUtils.isServer) {
                QueenBeeMotion.AmberBoom(Projectile.Center,
                    (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitY), 0.6f);
            }

            if (VaultUtils.isClient) {
                return;
            }

            int count = DeathBoost ? 4 : 3;
            float speed = DeathBoost ? 8.5f : 7f;
            Vector2 baseDir = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitY);
            for (int i = 0; i < count; i++) {
                float offset = (i - (count - 1) * 0.5f) * VolleySpreadStep;
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center,
                    baseDir.RotatedBy(offset) * speed, ModContent.ProjectileType<BrutalBeeStinger>(),
                    BrutalBeeStinger.BaseDamage, 0f, Main.myPlayer, 2f);
            }
        }

        private Player FindNearestPlayer(float maxDist) {
            Player best = null;
            float bestDist = maxDist;
            foreach (var p in Main.ActivePlayers) {
                if (p.dead) {
                    continue;
                }
                float d = p.Distance(Projectile.Center);
                if (d < bestDist) {
                    bestDist = d;
                    best = p;
                }
            }
            return best;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCDeath1 with { Volume = 0.6f, Pitch = -0.2f }, Projectile.Center);
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_WaxChip>(Projectile.Center,
                    Main.rand.NextVector2Circular(3f, 2f) - Vector2.UnitY * Main.rand.NextFloat(1f, 3f),
                    QueenBeeMotion.WaxPale, Main.rand.NextFloat(0.8f, 1.3f));
            }
            QueenBeeMotion.HoneyBurst(Projectile.Center, 0.9f, 6, false);
        }

        public override bool PreDraw(ref Color lightColor) {
            //蜂窝炮台体：Beenade原版贴图放大+蜡壳层次+预警辉光
            Main.instance.LoadProjectile(ProjectileID.Beenade);
            Texture2D tex = TextureAssets.Projectile[ProjectileID.Beenade].Value;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;

            float lived = LivedFrames;
            //生长弹性过冲
            float grow = MathHelper.Clamp(lived / GrowTime, 0f, 1f);
            float growScale = grow < 1f
                ? MathHelper.Lerp(0.1f, 1.12f, 1f - (1f - grow) * (1f - grow)) - 0.12f * grow * grow
                : 1f;
            //萎缩塌陷
            if (Projectile.timeLeft <= DecayTime) {
                growScale *= MathHelper.Lerp(0.2f, 1f, Projectile.timeLeft / (float)DecayTime);
            }

            //预警充能：齐射前渐亮+微胀
            float warnGlow = 0f;
            if (lived >= GrowTime && Projectile.timeLeft > DecayTime) {
                int cycleT = (int)lived % VolleyInterval;
                int warnStart = VolleyInterval - 1 - WarnTime;
                if (cycleT >= warnStart) {
                    warnGlow = (cycleT - warnStart) / (float)WarnTime;
                }
            }

            float breathe = 1f + 0.045f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3.2f + Projectile.whoAmI);
            float scale = 2.1f * growScale * breathe * (1f + warnGlow * 0.1f);

            //琥珀底辉
            Texture2D glowTex = CWRUtils.GetT2DAsset(CWRConstant.Masking + "SoftGlow")?.Value;
            if (glowTex != null) {
                Main.EntitySpriteDraw(glowTex, pos, null,
                    new Color(255, 190, 80, 0) * (0.22f + warnGlow * 0.4f), 0f,
                    glowTex.Size() * 0.5f, scale * 0.9f, SpriteEffects.None, 0);
            }

            //本体双层：暗壳+主体
            Main.EntitySpriteDraw(tex, pos + new Vector2(0f, 2f), null,
                QueenBeeMotion.AmberDeep * 0.5f, Projectile.rotation, origin, scale * 1.05f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, pos, null, lightColor, Projectile.rotation, origin, scale, SpriteEffects.None, 0);
            //预警白热(短促)
            if (warnGlow > 0.55f) {
                float flash = (warnGlow - 0.55f) / 0.45f;
                Main.EntitySpriteDraw(tex, pos, null, new Color(255, 235, 170, 0) * (flash * 0.5f),
                    Projectile.rotation, origin, scale * 1.03f, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
