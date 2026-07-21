using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Projectiles
{
    /// <summary>无伤冲击波，WarpEffectRender；ai[0]档0音爆1死光2超新星；ai[1]主题0激光1魔焰2混</summary>
    internal class TwinsSupernovaBlast : ModProjectile, IWarpDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int MaxLife = 38;

        private float SizeClass => Projectile.ai[0];
        private float ThemeMode => Projectile.ai[1];

        private float BaseSize => SizeClass switch {
            2f => 960f,
            1f => 560f,
            _ => 330f
        };

        private float Progress => 1f - Projectile.timeLeft / (float)MaxLife;

        private Color PrimaryColor => ThemeMode switch {
            1f => TwinsMotion.SpazColor,
            2f => TwinsMotion.SpazColor,
            _ => TwinsMotion.RetinColor
        };

        private Color SecondaryColor => ThemeMode switch {
            1f => new Color(255, 220, 120),
            2f => TwinsMotion.RetinColor,
            _ => new Color(150, 110, 255)
        };

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2400;

        public override void SetDefaults() {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = MaxLife;
            Projectile.alpha = 255;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            //首帧客户端爆发粒子
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (!VaultUtils.isServer) {
                    float power = 1f + SizeClass * 0.65f;
                    PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero,
                        PrimaryColor, 0.1f)?.Configure(0.1f, 1.05f * power, 26);
                    int sparkCount = (int)(8 * power);
                    int eyeMode = ThemeMode == 1f ? 1 : 0;
                    for (int i = 0; i < sparkCount; i++) {
                        PRTLoader.NewParticle<PRT_TwinsSpark>(Projectile.Center,
                            Main.rand.NextVector2Unit() * Main.rand.NextFloat(4f, 11f) * power,
                            Color.White, Main.rand.NextFloat(1f, 1.8f))?
                            .Configure(Main.rand.Next(16, 26), ThemeMode == 2f ? Main.rand.Next(2) : eyeMode);
                    }
                }
            }

            Lighting.AddLight(Projectile.Center,
                PrimaryColor.ToVector3() * (1.5f * (1f - Progress) * (1f + SizeClass * 0.5f)));
        }

        public override bool PreDraw(ref Color lightColor) => false;

        public bool CanDrawCustom() => true;

        public bool DontUseBlueshiftEffect() => true;

        /// <summary>扭曲层之上补绘的双色反向旋转冲击环</summary>
        public void DrawCustom(SpriteBatch spriteBatch) {
            Texture2D ring = CWRAsset.DiffusionCircle.Value;
            float t = Progress;
            float scale = BaseSize / ring.Width * (0.22f + t * 1.2f);
            float alpha = (1f - t) * (1f - t) * 0.85f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            Color outerColor = Color.Lerp(Color.White, PrimaryColor, MathHelper.Clamp(t * 1.6f, 0f, 1f)) with { A = 0 };
            Color innerColor = SecondaryColor with { A = 0 };

            //主环顺时针
            spriteBatch.Draw(ring, drawPos, null, outerColor * alpha, t * 1.5f,
                ring.Size() / 2f, scale, SpriteEffects.None, 0f);
            //副环逆时针，双色交叠
            spriteBatch.Draw(ring, drawPos, null, innerColor * (alpha * 0.6f), -t * 1.1f,
                ring.Size() / 2f, scale * 0.74f, SpriteEffects.None, 0f);
        }

        /// <summary>屏幕扭曲，扩张的冲击波折射环</summary>
        public void Warp() {
            float t = Progress;
            float size = BaseSize * (0.3f + t * 1.45f);
            float intensity = (1f - t) * 0.5f * Math.Min(1f + SizeClass * 0.4f, 1.9f);
            NeutronWarpHelper.DrawWarp(
                Projectile.Center,
                screenWidth: size,
                screenHeight: size,
                intensity: intensity,
                progress: t,
                rotation: 0f,
                technique: "ShockwaveRing"
            );
        }
    }
}
