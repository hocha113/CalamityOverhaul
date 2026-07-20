using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 刻心者液态血珠：受重力、随速度拉伸的非加色血滴，读作液体而非能量。<br/>
    /// 用于剜心飞溅、心脏滴血、血刃收束等"血是液体"的场合。<br/>
    /// 贴图必须用带真 alpha 的 Extra_98（黑底遮罩类贴图在 AlphaBlend 直绘会糊出黑色矩形底）
    /// </summary>
    internal class PRT_HeartcarverDroplet : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;

        private Color initialColor;
        private float gravity;
        private float drag;

        public PRT_HeartcarverDroplet Configure(int lifetime, float gravityPerFrame = 0.32f, float dragMul = 0.985f) {
            Lifetime = lifetime;
            initialColor = Color;
            gravity = gravityPerFrame;
            drag = dragMul;
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            gravity = 0f;
            drag = 1f;
        }

        public override void AI() {
            Velocity.X *= drag;
            Velocity.Y += gravity;
            if (Velocity.Y > 14f) {
                Velocity.Y = 14f;
            }

            //血珠坠落中逐渐凝缩变暗
            float t = LifetimeCompletion;
            Scale *= 0.985f;
            Color = Color.Lerp(initialColor, Color.Transparent, MathF.Pow(t, 2.4f));
            Rotation = Velocity.ToRotation() + MathHelper.PiOver2;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;
            //随速度纵向拉伸：快则成线、慢则成珠
            float stretch = MathHelper.Clamp(Velocity.Length() * 0.045f, 0f, 0.85f);
            Vector2 scale = new Vector2(0.34f * (1f - stretch * 0.35f), 0.62f * (1f + stretch * 1.7f)) * Scale;

            //双层同色窄叠：中心更实，读作液滴而非光斑
            spriteBatch.Draw(tex, pos, null, Color, Rotation, origin, scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, Color, Rotation, origin, scale * new Vector2(0.45f, 1f), SpriteEffects.None, 0f);
            return false;
        }
    }
}
