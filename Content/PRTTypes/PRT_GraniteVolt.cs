using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>花岗微电弧，2~6帧，有初速顺速劈否则随机向</summary>
    internal class PRT_GraniteVolt : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "ThunderTrail";
        public override bool CanPool => true;

        private float aspect;
        private float flicker;

        /// <param name="lifetime">帧，夹到 2~6</param>
        /// <param name="aspect">宽长比，默认 0.42</param>
        public PRT_GraniteVolt Configure(int lifetime, float aspect = 0.42f) {
            Lifetime = Math.Clamp(lifetime, 2, 6);
            this.aspect = aspect;
            return this;
        }

        public override void Reset() {
            base.Reset();
            aspect = 0f;
            flicker = 1f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            Rotation = Velocity != Vector2.Zero ? Velocity.ToRotation() : Main.rand.NextFloat(MathHelper.TwoPi);
            ai[0] = Main.rand.NextBool() ? 1f : 0f; //随机竖翻
            flicker = 1f;
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(3, 6);
            }
            if (aspect <= 0f) {
                aspect = 0.42f;
            }
        }

        public override void AI() {
            Velocity *= 0.75f;
            flicker = Main.rand.NextFloat(0.55f, 1f);
            Scale *= 0.96f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = TexValue;
            Vector2 pos = Position - Main.screenPosition;
            Vector2 origin = tex.Size() / 2f;
            float fade = (1f - LifetimeCompletion * 0.6f) * flicker;
            SpriteEffects fx = ai[0] == 1f ? SpriteEffects.FlipVertically : SpriteEffects.None;
            Color edge = Color; edge.A = 0;
            Vector2 scale = new Vector2(Scale, Scale * aspect);

            spriteBatch.Draw(tex, pos, null, edge * fade, Rotation, origin, scale, fx, 0f);
            spriteBatch.Draw(tex, pos, null, Color.White * 0.85f * fade, Rotation, origin
                , scale * new Vector2(0.86f, 0.6f), fx, 0f);
            return false;
        }
    }
}
