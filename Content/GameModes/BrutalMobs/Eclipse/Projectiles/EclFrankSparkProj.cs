using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Eclipse.Projectiles
{
    /// <summary>
    /// 科学怪人签名·突进落点电火花（M6 差异分支）：冲锋收势点滞留的微区域判定。
    /// 伤害窗=亮窗：前 <see cref="ActiveFrames"/> 帧可判定且全亮，其后无害淡出（判窗与绘制强度同一判据）。
    /// 残留物语义：生成即定点、无追踪、不依赖施主存活（预告由冲锋警示带承担，火花区在带内）。
    /// 占位贴图仅作句柄：遮挡体由手绘提供（Extra_98 真透暗壳打底 + A=0 电蓝亮芯，M5 合规）
    /// </summary>
    internal class EclFrankSparkProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>判定亮窗帧数（签名口径：8 帧滞留判定）</summary>
        internal const int ActiveFrames = 8;
        /// <summary>无害淡出帧数</summary>
        private const int FadeFrames = 6;

        private static readonly Color SparkDark = new Color(18, 24, 44);
        private static readonly Color SparkCore = new Color(150, 220, 255, 0);

        /// <summary>是否处于判定亮窗（伤害与满强度视觉共用此判据）</summary>
        private bool Live => Projectile.timeLeft > FadeFrames;

        public override void SetDefaults() {
            Projectile.width = 54;
            Projectile.height = 40;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = ActiveFrames + FadeFrames;
            Projectile.netImportant = true;
        }

        /// <summary>伤害窗=亮窗：淡出段绝不判定</summary>
        public override bool? CanDamage() {
            if (Live) {
                return null;
            }
            return false;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.DD2_LightningBugZap with {
                        Volume = 0.5f, Pitch = -0.1f, MaxInstances = 5
                    }, Projectile.Center);
                }
            }
            if (!VaultUtils.isServer) {
                //电弧尘：亮窗 3 粒/帧、淡出 1 粒/帧
                int budget = Live ? 3 : 1;
                for (int i = 0; i < budget; i++) {
                    Dust arc = Dust.NewDustPerfect(
                        Projectile.Center + Main.rand.NextVector2Circular(Projectile.width * 0.45f, Projectile.height * 0.4f),
                        DustID.Electric, Main.rand.NextVector2Circular(1.6f, 1.2f), 60, default, Main.rand.NextFloat(0.6f, 1f));
                    arc.noGravity = true;
                }
                Lighting.AddLight(Projectile.Center, 0.16f, 0.24f, 0.34f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D shell = CWRAsset.Extra_98.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            //亮窗全强，淡出段线性退光（与 CanDamage 同一判据，所见即所判）
            float strength = Live ? 1f : Projectile.timeLeft / (float)FadeFrames;
            //确定性电闪抖动（不吃 Main.rand，本端自洽）
            float flicker = 0.7f + 0.3f * MathF.Sin((Projectile.timeLeft * 2.4f + Projectile.identity) * 2.1f);

            Vector2 shellScale = new Vector2(Projectile.width * 1.18f / shell.Width, Projectile.height * 1.18f / shell.Height);
            //真透暗壳（遮挡像素层）→ 电蓝亮芯 → 加色辉光
            Main.EntitySpriteDraw(shell, drawPos, null, SparkDark * (0.85f * strength), 0f,
                shell.Size() / 2f, shellScale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(shell, drawPos, null, SparkCore * (0.8f * strength * flicker), 0f,
                shell.Size() / 2f, shellScale * 0.72f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, drawPos, null, SparkCore * (0.5f * strength * flicker), 0f,
                glow.Size() / 2f, new Vector2(Projectile.width * 1.7f / glow.Width, Projectile.height * 1.5f / glow.Height),
                SpriteEffects.None, 0);
            return false;
        }
    }
}
