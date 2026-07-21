using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>伽马电离短线，紫蓝闪散</summary>
    internal class PRT_GammaIonize : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "LightShot";

        private Color initialColor;
        private float initialScale;
        private float flickerPhase;
        private float deceleration;

        public override bool CanPool => true;
        public PRT_GammaIonize Configure(int lt, float flickerOffset = 0f) {
            Lifetime = lt;
            initialColor = Color;
            initialScale = Scale;
            flickerPhase = flickerOffset;
            deceleration = 0.88f;
            Rotation = Velocity.ToRotation();
            return this;
        }
        public override void Reset() {
            base.Reset();
            initialColor = default;
            initialScale = 0f;
            flickerPhase = 0f;
            deceleration = 0.88f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
        }

        public override void AI() {
            Velocity *= deceleration;
            if (deceleration > 0.82f) {
                deceleration -= 0.003f;
            }

            if (Velocity.LengthSquared() > 0.5f) {
                Rotation = Velocity.ToRotation();
            }

            float life = LifetimeCompletion;

            //二值闪
            float flicker = (float)Math.Sin((Time + flickerPhase) * 1.2f);
            flicker = flicker > 0 ? 1f : 0.3f;

            //前1/4胀，后收
            if (life < 0.25f) {
                Scale = initialScale * (life / 0.25f);
            }
            else {
                Scale = initialScale * (1f - (life - 0.25f) / 0.75f);
            }

            float fade = (float)Math.Pow(life, 1.5);
            Color = Color.Lerp(initialColor, new Color(40, 20, 100, 0), fade);

            Opacity = (1f - fade) * flicker;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D texture = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = texture.Size() * 0.5f;
            Vector2 drawPosition = Position - Main.screenPosition;

            //X拉长成射线
            Vector2 drawScale = new Vector2(Scale * 1.6f, Scale * 0.35f);

            spriteBatch.Draw(
                texture,
                drawPosition,
                null,
                Color * Opacity * 0.35f,
                Rotation,
                origin,
                drawScale * 2.2f,
                SpriteEffects.None,
                0f
            );

            spriteBatch.Draw(
                texture,
                drawPosition,
                null,
                Color * Opacity,
                Rotation,
                origin,
                drawScale,
                SpriteEffects.None,
                0f
            );

            return false;
        }
    }
}
