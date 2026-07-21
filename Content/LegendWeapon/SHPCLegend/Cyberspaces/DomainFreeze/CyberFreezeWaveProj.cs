using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.DomainFreeze
{
    /// <summary>冻结能量波弹幕，领域中心向外扩散</summary>
    internal class CyberFreezeWaveProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int Lifetime = 55;
        private float maxDrawRadius;

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
            //贴主人领域中心
            CyberspacePlayer cp = Cyberspace.For(Projectile.owner);
            if (cp != null) {
                Projectile.Center = cp.DomainCenter;
            }
            if (Projectile.localAI[0] == 0f) {
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(CWRSound.FaultOccurred, Projectile.Center);
                    SoundEngine.PlaySound(CWRSound.Faultrelease, Projectile.Center);
                }
                maxDrawRadius = (cp?.Radius ?? Cyberspace.BaseRadius) * 1.15f;
                Projectile.localAI[0] = 1f;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Effect shader = CyberDomainFreezeAssets.CyberFreezeWave;
            if (shader == null) return false;
            if (VaultAsset.placeholder2 == null) return false;
            if (CWRAsset.Extra_193?.Value == null) return false;

            Texture2D canvas = VaultAsset.placeholder2.Value;
            Texture2D noise = CWRAsset.Extra_193.Value;

            float t = 1f - (float)Projectile.timeLeft / Lifetime;
            //缓出展开
            float ringProgress = 1f - MathF.Pow(1f - t, 3.2f);

            float fadeAlpha;
            if (t < 0.15f)
                fadeAlpha = MathHelper.SmoothStep(0f, 1f, t / 0.15f);
            else if (t < 0.6f)
                fadeAlpha = 1f;
            else
                fadeAlpha = MathHelper.SmoothStep(1f, 0f, (t - 0.6f) / 0.4f);
            fadeAlpha = MathHelper.Clamp(fadeAlpha, 0f, 1f);

            //环厚随展开变薄
            float thickness = 0.08f + (1f - t) * 0.05f;

            //uTime 取主人领域时间
            CyberspacePlayer cpForShader = Cyberspace.For(Projectile.owner);
            float effectTime = cpForShader?.EffectTime ?? Cyberspace.EffectTime;
            shader.Parameters["uTime"]?.SetValue(effectTime);
            shader.Parameters["ringProgress"]?.SetValue(ringProgress);
            shader.Parameters["ringThickness"]?.SetValue(thickness);
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

            //暗红晶
            Color waveTint = new Color(1f, 0.3f, 0.35f);
            Main.spriteBatch.Draw(canvas, drawPos, null, waveTint,
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
