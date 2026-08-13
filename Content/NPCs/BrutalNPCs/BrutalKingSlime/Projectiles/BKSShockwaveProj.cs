using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Rendering;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Projectiles
{
    /// <summary>落地凝胶冲击环，无伤纯演出；ai[0]档位0小1中2大；服务端生成</summary>
    internal class BKSShockwaveProj : ModProjectile, IWarpDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int MaxLife = 34;

        private float SizeClass => Projectile.ai[0];

        private float BaseSize => SizeClass switch {
            2f => 760f,
            1f => 470f,
            _ => 300f
        };

        private float Progress => 1f - Projectile.timeLeft / (float)MaxLife;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2000;

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
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (!VaultUtils.isServer) {
                    float power = 1f + SizeClass * 0.6f;
                    KingSlimeGelFX.BubbleFizz(Projectile.Center, 60f * power, (int)(6 * power));
                }
            }

            Lighting.AddLight(Projectile.Center,
                KingSlimeGelFX.GelMid.ToVector3() * (1.2f * (1f - Progress) * (1f + SizeClass * 0.5f)));
        }

        public override bool PreDraw(ref Color lightColor) {
            Effect shader = EffectLoader.KingSlimeShockwave?.Value;
            Texture2D canvas = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (shader == null || canvas == null || noise == null) {
                return false;
            }

            float t = Progress;
            float size = BaseSize * (0.4f + t * 1.3f);

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;

            shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly + Projectile.whoAmI * 0.31f);
            shader.Parameters["ringProgress"]?.SetValue(t);
            shader.Parameters["fadeAlpha"]?.SetValue((1f - t) * (0.8f + SizeClass * 0.15f));
            shader.Parameters["pulseIntensity"]?.SetValue(0.6f + SizeClass * 0.3f);
            shader.Parameters["coreColor"]?.SetValue(KingSlimeGelFX.CrownGold.ToVector3());
            shader.Parameters["midColor"]?.SetValue(KingSlimeGelFX.GelMid.ToVector3());
            shader.Parameters["edgeColor"]?.SetValue(KingSlimeGelFX.GelDeep.ToVector3());
            shader.CurrentTechnique.Passes[0].Apply();

            sb.Draw(canvas, Projectile.Center - Main.screenPosition, null, Color.White, 0f,
                canvas.Size() * 0.5f, new Vector2(size / canvas.Width, size / canvas.Height), SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }

        public bool CanDrawCustom() => true;

        public bool DontUseBlueshiftEffect() => true;

        /// <summary>扭曲层之上补绘可见扩散环</summary>
        public void DrawCustom(SpriteBatch spriteBatch) {
            Texture2D ring = CWRAsset.DiffusionCircle.Value;
            float t = Progress;
            float scale = BaseSize / ring.Width * (0.2f + t * 1.05f);
            float alpha = (1f - t) * (1f - t) * 0.65f;
            Color color = Color.Lerp(KingSlimeGelFX.GelFoam, KingSlimeGelFX.GelMid, t) with { A = 0 };
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            spriteBatch.Draw(ring, drawPos, null, color * alpha, t * 1.1f, ring.Size() / 2f, scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(ring, drawPos, null, color * (alpha * 0.5f), -t * 0.7f, ring.Size() / 2f, scale * 0.74f, SpriteEffects.None, 0f);
        }

        /// <summary>屏幕扭曲环</summary>
        public void Warp() {
            float t = Progress;
            float size = BaseSize * (0.32f + t * 1.35f);
            float intensity = (1f - t) * 0.5f * Math.Min(1f + SizeClass * 0.3f, 1.7f);
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
