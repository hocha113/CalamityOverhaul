using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 刻心者血珠，非加色，Extra_98 真 alpha（黑底贴图会糊黑底）
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

            float t = LifetimeCompletion;
            Scale *= 0.985f;
            Color = Color.Lerp(initialColor, Color.Transparent, MathF.Pow(t, 2.4f));
            Rotation = Velocity.ToRotation() + MathHelper.PiOver2;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;
            //快成线、慢成珠
            float stretch = MathHelper.Clamp(Velocity.Length() * 0.045f, 0f, 0.85f);
            Vector2 scale = new Vector2(0.34f * (1f - stretch * 0.35f), 0.62f * (1f + stretch * 1.7f)) * Scale;

            spriteBatch.Draw(tex, pos, null, Color, Rotation, origin, scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, Color, Rotation, origin, scale * new Vector2(0.45f, 1f), SpriteEffects.None, 0f);
            return false;
        }
    }
}
