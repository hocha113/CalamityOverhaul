using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.Teleport
{
    /// <summary>瞬移起点解构弹幕，direction=-1 离心飞散</summary>
    internal class CyberPixelDecomposeProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //寿命略短于走廊延伸
        private const int Lifetime = 22;
        //可视半径
        private const float DisplayRadius = 220f;
        //direction=-1 离心
        private const float Direction = -1f;

        private float seed;

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
            if (Projectile.localAI[0] == 0f) {
                seed = Main.rand.NextFloat();
                Projectile.localAI[0] = 1f;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Effect shader = EffectLoader.CyberReform?.Value;
            if (shader == null) return false;
            if (VaultAsset.placeholder2 == null) return false;
            if (CWRAsset.Extra_193?.Value == null) return false;

            Texture2D canvas = VaultAsset.placeholder2.Value;
            Texture2D noise = CWRAsset.Extra_193.Value;

            float t = 1f - (float)Projectile.timeLeft / Lifetime;

            //decompose 0撕开→1飞散
            float progress = MathHelper.Clamp(t, 0f, 1f);
            //snap 0~0.15 撕开闪
            float snap = MathF.Max(0f, 1f - t / 0.15f);
            snap = MathF.Pow(snap, 1.4f) * 0.85f;
            //direction=-1，近亮远暗
            float dissipate = 0f;

            //淡入极快，尾段抖出
            float fadeAlpha;
            if (t < 0.05f) fadeAlpha = t / 0.05f;
            else if (t > 0.75f) fadeAlpha = MathHelper.SmoothStep(1f, 0f, (t - 0.75f) / 0.25f);
            else fadeAlpha = 1f;

            //uTime 取主人领域时间
            CyberspacePlayer ownerCp = Cyberspace.For(Projectile.owner);
            float effectTime = ownerCp != null && ownerCp.Active
                ? ownerCp.EffectTime
                : (float)Main.timeForVisualEffects * 0.04f;
            shader.Parameters["uTime"]?.SetValue(effectTime);
            shader.Parameters["fadeAlpha"]?.SetValue(MathHelper.Clamp(fadeAlpha, 0f, 1f));
            shader.Parameters["reformProgress"]?.SetValue(progress);
            shader.Parameters["snapPulse"]?.SetValue(MathHelper.Clamp(snap, 0f, 1f));
            shader.Parameters["dissipate"]?.SetValue(dissipate);
            shader.Parameters["seed"]?.SetValue(seed);
            shader.Parameters["direction"]?.SetValue(Direction);

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float drawDiameter = DisplayRadius * 2f;

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive,
                SamplerState.LinearWrap, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);

            Main.graphics.GraphicsDevice.Textures[1] = noise;
            Main.graphics.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
            shader.CurrentTechnique.Passes[0].Apply();

            Main.spriteBatch.Draw(canvas, drawPos, null, Color.White,
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
