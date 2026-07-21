using CalamityOverhaul.Content.Items.Ranged;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 血色弹道光痕，起点→终点三层驻留（深红外晕/血色中层/暖橙热芯先熄）
    /// Extra_98 拉伸，兼枪口爆闪/破空痕/接棺闪痕
    /// </summary>
    internal class PRT_PallbearerTracer : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "SoftGlow";
        public override bool CanPool => true;

        private Vector2 start;
        private Vector2 end;
        private float width;

        public PRT_PallbearerTracer Configure(Vector2 startPos, Vector2 endPos, float beamWidth, int lifetime) {
            start = startPos;
            end = endPos;
            width = beamWidth;
            Lifetime = lifetime;
            Position = (startPos + endPos) * 0.5f;
            return this;
        }

        public override void Reset() {
            base.Reset();
            start = end = Vector2.Zero;
            width = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            Velocity = Vector2.Zero;
        }

        public override void AI() {
            //瞬亮缓出
            Opacity = MathF.Pow(1f - LifetimeCompletion, 1.6f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D streak = PRT_PallbearerEmber.StreakTex?.Value;
            if (streak == null || start == end) {
                return false;
            }
            float lc = LifetimeCompletion;
            Vector2 delta = end - start;
            Vector2 mid = Position - Main.screenPosition;
            float rot = delta.ToRotation() + MathHelper.PiOver2;
            Vector2 texOrigin = streak.Size() * 0.5f;
            float lenScale = delta.Length() / streak.Height;
            //熄灭收窄、长度不变
            float xScale = width * (1f - lc * 0.45f) / streak.Width;

            Color deep = PallbearerVFX.BloodDeep with { A = 0 };
            Color blood = PallbearerVFX.Blood with { A = 0 };
            Color hot = PallbearerVFX.Ember with { A = 0 };
            //热芯先熄
            float coreOpacity = MathF.Pow(1f - lc, 3.2f);

            spriteBatch.Draw(streak, mid, null, deep * (0.55f * Opacity), rot, texOrigin
                , new Vector2(xScale * 2.1f, lenScale), SpriteEffects.None, 0f);
            spriteBatch.Draw(streak, mid, null, blood * (0.85f * Opacity), rot, texOrigin
                , new Vector2(xScale, lenScale), SpriteEffects.None, 0f);
            spriteBatch.Draw(streak, mid, null, hot * (0.95f * coreOpacity), rot, texOrigin
                , new Vector2(xScale * 0.34f, lenScale), SpriteEffects.None, 0f);
            return false;
        }
    }
}
