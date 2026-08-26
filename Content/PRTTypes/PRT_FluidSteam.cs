using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 液体系蒸汽/烟团:Fog 单帧真 alpha,AlphaBlend 直接染色(白=蒸汽,暗色=浓烟)。
    /// 上升+缓胀+旋转消散;形变靠随机旋转与镜像(Fog 是不对称烟羽)
    /// </summary>
    internal class PRT_FluidSteam : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Fog";
        public override bool CanPool => true;

        private Color initialColor;
        private float spin;
        private float rise;
        private SpriteEffects flip;

        public PRT_FluidSteam Configure(int lifetime, float risePerFrame = 0.05f, float spinSpeed = 0.012f) {
            Lifetime = lifetime;
            initialColor = Color;
            rise = risePerFrame;
            spin = spinSpeed * (Main.rand.NextBool() ? 1f : -1f);
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            flip = Main.rand.NextBool() ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            spin = 0f;
            rise = 0f;
            flip = SpriteEffects.None;
        }

        public override void AI() {
            Velocity *= 0.96f;
            Velocity.Y -= rise;
            Rotation += spin;
            Scale += 0.006f;

            float t = LifetimeCompletion;
            //先浮现后消散
            float a = MathF.Sin(MathHelper.Clamp(t, 0f, 1f) * MathHelper.Pi);
            Color = initialColor * (a * a);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            spriteBatch.Draw(tex, Position - Main.screenPosition, null, Color,
                Rotation, tex.Size() * 0.5f, Scale, flip, 0f);
            return false;
        }
    }
}
