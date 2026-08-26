using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 沉波狱吏淌水滴：出水尸身上滚落的污水珠，比 SewageGlob 更小更清亮（是水不是泥）。
    /// 重力下坠、速度纵向拉伸成水线，触固体摊成 4 帧微渍熄灭；不追踪不汇聚。
    /// </summary>
    internal class PRT_TurnkeyDrip : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 300;

        private Color initialColor;
        private bool splatting;
        private int splatTicks;

        public PRT_TurnkeyDrip Configure(int lifetime) {
            Lifetime = lifetime;
            initialColor = Color;
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            splatting = false;
            splatTicks = 0;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            if (Lifetime <= 0) {
                Lifetime = 30;
            }
            if (initialColor == default) {
                initialColor = Color;
            }
        }

        public override void AI() {
            if (splatting) {
                Velocity = Vector2.Zero;
                splatTicks++;
                Color = Color.Lerp(initialColor, Color.Transparent, splatTicks / 4f);
                if (splatTicks >= 4) {
                    active = false;
                }
                return;
            }

            Velocity.X *= 0.97f;
            Velocity.Y = Math.Min(Velocity.Y + 0.34f, 11f);
            if (Velocity.Y > 1f && Collision.SolidCollision(Position - new Vector2(1f, 1f), 2, 2)) {
                splatting = true;
                return;
            }
            //入水即被水面收走（水滴回水没有存在感，别留悬浮球）
            if (Collision.WetCollision(Position, 2, 2)) {
                active = false;
                return;
            }

            float t = LifetimeCompletion;
            if (t > 0.75f) {
                Color = Color.Lerp(initialColor, Color.Transparent, (t - 0.75f) / 0.25f);
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;

            if (splatting) {
                float k = splatTicks / 4f;
                spriteBatch.Draw(tex, pos, null, Color, 0f, origin,
                    new Vector2(0.22f * (1f + k), 0.06f) * Scale, SpriteEffects.None, 0f);
                return false;
            }

            //水线：坠速越快越细长；顶部一粒更亮的小高光（水珠的受光点）
            float stretch = MathHelper.Clamp(Velocity.Length() * 0.09f, 0f, 1.6f);
            Vector2 body = new Vector2(0.13f * (1f - stretch * 0.25f), 0.15f * (1f + stretch * 2.2f)) * Scale;
            spriteBatch.Draw(tex, pos, null, Color, Velocity.ToRotation() + MathHelper.PiOver2,
                origin, body, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos - new Vector2(0f, 2f) * Scale, null, Color * 0.65f, 0f,
                origin, body * 0.4f, SpriteEffects.None, 0f);
            return false;
        }
    }
}
