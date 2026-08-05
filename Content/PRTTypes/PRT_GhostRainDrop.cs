using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 鬼雨主雨滴：快成丝、慢成珠，触地转短暂扁溅后熄。
    /// Extra_98 真 alpha，非加色，无光晕。
    /// </summary>
    internal class PRT_GhostRainDrop : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 160;

        private Color initialColor;
        private float windX;
        private bool splashing;
        private int splashTicks;

        public PRT_GhostRainDrop Configure(int lifetime, float wind) {
            Lifetime = lifetime;
            windX = wind;
            initialColor = Color;
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            windX = 0f;
            splashing = false;
            splashTicks = 0;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            if (Lifetime <= 0) {
                Lifetime = 100;
            }
            if (initialColor == default) {
                initialColor = Color;
            }
        }

        public override void AI() {
            if (splashing) {
                Velocity = Vector2.Zero;
                splashTicks++;
                Color = Color.Lerp(initialColor, Color.Transparent, splashTicks / 8f);
                if (splashTicks >= 8) {
                    active = false;
                }
                return;
            }

            Velocity.X = windX;
            Velocity.Y = Math.Min(Velocity.Y + 0.5f, 16.5f);
            Rotation = Velocity.ToRotation() + MathHelper.PiOver2;

            //触地转扁溅
            if (Velocity.Y > 2f && Collision.SolidCollision(Position - new Vector2(1f, 1f), 2, 2)) {
                splashing = true;
                return;
            }

            float t = LifetimeCompletion;
            if (t > 0.85f) {
                Color = Color.Lerp(initialColor, Color.Transparent, (t - 0.85f) / 0.15f);
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;

            if (splashing) {
                //扁溅：横向摊开的一小片水光
                float k = splashTicks / 8f;
                Vector2 scale = new Vector2(0.36f * (1f + k * 1.4f), 0.09f) * Scale;
                spriteBatch.Draw(tex, pos, null, Color, 0f, origin, scale, SpriteEffects.None, 0f);
                return false;
            }

            //快成丝、慢成珠
            float stretch = MathHelper.Clamp(Velocity.Length() * 0.055f, 0f, 1f);
            Vector2 body = new Vector2(0.13f * (1f - stretch * 0.35f),
                0.42f * (1f + stretch * 2.4f)) * Scale;
            spriteBatch.Draw(tex, pos, null, Color, Rotation, origin, body, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, Color * 0.6f, Rotation, origin,
                body * new Vector2(0.45f, 1.06f), SpriteEffects.None, 0f);
            return false;
        }
    }
}
