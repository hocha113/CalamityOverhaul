using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>空间裂隙细线</summary>
    internal class PRT_SpaceFracture : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "LightBeam";

        private Color initialColor;
        private float initialScale;
        private float angularVelocity;
        public int inOwner = -1;

        public override bool CanPool => true;
        public PRT_SpaceFracture Configure(int lt, float angularVelocity = 0f) {
            Lifetime = lt;
            initialColor = Color;
            initialScale = Scale;
            this.angularVelocity = angularVelocity;
            Rotation = Velocity.ToRotation();
            return this;
        }
        public override void Reset() {
            base.Reset();
            initialColor = default;
            initialScale = 0f;
            angularVelocity = 0f;
            inOwner = -1;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
        }

        public override void AI() {
            //前快后急减速
            float life = LifetimeCompletion;
            if (life < 0.3f) {
                Velocity *= 0.92f;
            }
            else {
                Velocity *= 0.85f;
            }

            Rotation += angularVelocity * 0.03f;

            //快现、尾锐灭
            float fadeIn = Math.Min(life * 8f, 1f);
            float fadeOut = 1f - (float)Math.Pow(Math.Max(life - 0.5f, 0f) * 2f, 2.5);
            Opacity = fadeIn * fadeOut;

            float stretchPhase = (float)Math.Sin(life * MathHelper.Pi);
            Scale = initialScale * (0.6f + stretchPhase * 0.4f);

            Color darkEnd = new Color(60, 20, 80);
            float colorShift = (float)Math.Pow(life, 1.5);
            Color = Color.Lerp(initialColor, darkEnd, colorShift);

            if (inOwner >= 0) {
                Position += Main.player[inOwner].CWR().PlayerPositionChange;
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D texture = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = texture.Size() * 0.5f;
            Vector2 drawPos = Position - Main.screenPosition;

            Vector2 scale = new Vector2(0.05f, 0.18f) * Scale;

            spriteBatch.Draw(texture, drawPos, null,
                Color * Opacity * 0.3f,
                Rotation, origin,
                scale * new Vector2(2.5f, 1.1f),
                SpriteEffects.None, 0f);

            spriteBatch.Draw(texture, drawPos, null,
                Color * Opacity,
                Rotation, origin,
                scale,
                SpriteEffects.None, 0f);

            return false;
        }
    }
}
