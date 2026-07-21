using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces
{
    /// <summary>领域展开冲击波，升层时 ai0/ai1 扫掠起止半径；按 owner 取领域中心</summary>
    internal class CyberShockwaveProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int Lifetime = 38;
        private float maxDrawRadius;
        private float startRadius;

        public override void SetDefaults() {
            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Lifetime;
        }

        public override void AI() {
            //跟主人领域中心
            CyberspacePlayer cp = Cyberspace.For(Projectile.owner);
            if (cp != null) {
                Projectile.Center = cp.DomainCenter;
                if (Projectile.localAI[0] == 0f) {
                    InitRadii(cp.Radius);
                    Projectile.localAI[0] = 1f;
                }
            }
            else if (Projectile.localAI[0] == 0f) {
                //无主人则 BaseRadius
                InitRadii(Cyberspace.BaseRadius);
                Projectile.localAI[0] = 1f;
            }
        }

        /// <summary>ai0/ai1 扫掠起止半径，0 则从中心扩散</summary>
        private void InitRadii(float fallbackRadius) {
            startRadius = MathF.Max(Projectile.ai[0], 0f);
            float endRadius = Projectile.ai[1] > 1f ? Projectile.ai[1] : fallbackRadius;
            if (endRadius < startRadius + 1f) {
                endRadius = startRadius + 1f;
            }
            maxDrawRadius = endRadius * 1.1f;
        }

        public override bool PreDraw(ref Color lightColor) {
            Effect shader = EffectLoader.CyberShockwave?.Value;
            if (shader == null) return false;
            if (VaultAsset.placeholder2 == null) return false;
            if (CWRAsset.Extra_193?.Value == null) return false;

            Texture2D canvas = VaultAsset.placeholder2.Value;
            Texture2D noise = CWRAsset.Extra_193.Value;

            float t = 1f - (float)Projectile.timeLeft / Lifetime;
            //快速起步的缓出曲线；带起始半径时从旧边界出发向新边界扫掠
            float eased = 1f - MathF.Pow(1f - t, 2.8f);
            float startFrac = maxDrawRadius > 0f ? MathHelper.Clamp(startRadius / maxDrawRadius, 0f, 1f) : 0f;
            float ringProgress = MathHelper.Lerp(startFrac, 1f, eased);
            float fadeAlpha;
            if (t < 0.55f)
                fadeAlpha = MathHelper.SmoothStep(0f, 1f, t / 0.2f);
            else
                fadeAlpha = MathHelper.SmoothStep(1f, 0f, (t - 0.55f) / 0.45f);
            fadeAlpha = MathHelper.Clamp(fadeAlpha, 0f, 1f);

            //uTime 取主人领域时间
            CyberspacePlayer cp = Cyberspace.For(Projectile.owner);
            float effectTime = cp?.EffectTime ?? Cyberspace.EffectTime;
            shader.Parameters["uTime"]?.SetValue(effectTime);
            shader.Parameters["ringProgress"]?.SetValue(ringProgress);
            shader.Parameters["ringThickness"]?.SetValue(0.065f + (1f - t) * 0.04f);
            shader.Parameters["fadeAlpha"]?.SetValue(fadeAlpha);

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float drawDiameter = maxDrawRadius * 2f;

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive,
                SamplerState.LinearWrap, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);

            Main.graphics.GraphicsDevice.Textures[1] = noise;
            Main.graphics.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
            shader.CurrentTechnique.Passes[0].Apply();

            Color ringTint = new Color(1f, 0.85f, 0.75f);
            Main.spriteBatch.Draw(canvas, drawPos, null, ringTint,
                0f, canvas.Size() * 0.5f, new Vector2(drawDiameter, drawDiameter),
                SpriteEffects.None, 0f);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);

            return false;
        }

        public override bool ShouldUpdatePosition() => false;
    }
}
