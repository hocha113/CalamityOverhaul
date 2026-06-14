using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Rendering;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Boss.Destroyer
{
    /// <summary>音爆冲击环：无伤害纯演出，经<see cref="Renders.WarpEffectRender"/>扩张屏幕扭曲环叠加扩散圆环与火花，服务端生成保证多人可见；ai[0]:尺寸档位0=冲刺音爆1=俯冲音爆2=终结冲击</summary>
    internal class DestroyerShockwave : ModProjectile, IWarpDrawable
    {
        public override string Texture => CWRConstant.Placeholder;

        private const int MaxLife = 36;

        private float SizeClass => Projectile.ai[0];

        private float BaseSize => SizeClass switch {
            2f => 880f,
            1f => 520f,
            _ => 340f
        };

        private float Progress => 1f - Projectile.timeLeft / (float)MaxLife;

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
                    float power = 1f + SizeClass * 0.7f;
                    PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero,
                        DestroyerMotionFX.HotRed, 0.1f).Configure(0.1f, 1.1f * power, 26);
                    int sparkCount = (int)(10 * power);
                    for (int i = 0; i < sparkCount; i++) {
                        Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(4f, 12f) * power;
                        PRTLoader.NewParticle<PRT_Spark>(Projectile.Center, vel,
                            Color.Lerp(DestroyerMotionFX.HotOrange, DestroyerMotionFX.WhiteHot, Main.rand.NextFloat()),
                            Main.rand.NextFloat(0.8f, 1.4f)).Configure(true, Main.rand.Next(14, 26));
                    }
                }
            }

            Lighting.AddLight(Projectile.Center,
                DestroyerMotionFX.HotOrange.ToVector3() * (1.6f * (1f - Progress) * (1f + SizeClass * 0.5f)));
        }

        public override bool PreDraw(ref Color lightColor) => false;

        public bool CanDrawCustom() => true;

        public bool DontUseBlueshiftEffect() => true;

        /// <summary>扭曲层之上补绘的可见冲击环</summary>
        public void DrawCustom(SpriteBatch spriteBatch) {
            Texture2D ring = CWRAsset.DiffusionCircle.Value;
            float t = Progress;
            float scale = BaseSize / ring.Width * (0.25f + t * 1.15f);
            float alpha = (1f - t) * (1f - t) * 0.8f;
            Color color = Color.Lerp(DestroyerMotionFX.WhiteHot, DestroyerMotionFX.HotRed, t) with { A = 0 };
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            spriteBatch.Draw(ring, drawPos, null, color * alpha, t * 1.4f, ring.Size() / 2f, scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(ring, drawPos, null, color * (alpha * 0.55f), -t * 0.9f, ring.Size() / 2f, scale * 0.78f, SpriteEffects.None, 0f);
        }

        /// <summary>屏幕扭曲：扩张的冲击波环</summary>
        public void Warp() {
            float t = Progress;
            float size = BaseSize * (0.3f + t * 1.4f);
            float intensity = (1f - t) * 0.55f * Math.Min(1f + SizeClass * 0.35f, 1.8f);
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
