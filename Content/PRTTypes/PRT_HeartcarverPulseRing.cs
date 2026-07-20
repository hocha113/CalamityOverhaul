using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 刻心者血色脉冲环：干净的扩散环（无星光装饰），主环 + 滞后回声环双层。<br/>
    /// 用于心脏搏动脉冲、剜心击命中确认、冲刺起步冲击圈
    /// </summary>
    internal class PRT_HeartcarverPulseRing : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "DiffusionCircle";
        public override bool CanPool => true;

        private float startScale;
        private float endScale;
        private Color initialColor;

        public PRT_HeartcarverPulseRing Configure(float originScale, float finalScale, int lifetime) {
            startScale = originScale;
            endScale = finalScale;
            Scale = originScale;
            Lifetime = lifetime;
            initialColor = Color;
            return this;
        }

        public override void Reset() {
            base.Reset();
            startScale = 0f;
            endScale = 0f;
            initialColor = default;
        }

        public override void SetProperty() => PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;

        public override void AI() {
            float t = LifetimeCompletion;
            //快张缓收：搏动波前沿冲出后减速消散
            Scale = MathHelper.Lerp(startScale, endScale, 1f - MathF.Pow(1f - t, 2.6f));
            Opacity = 1f - t * t;
            Color = initialColor * Opacity;
            Velocity *= 0.92f;
            Lighting.AddLight(Position, Color.R / 255f * 0.6f, Color.G / 255f * 0.6f, Color.B / 255f * 0.6f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;
            //主环
            spriteBatch.Draw(tex, pos, null, Color, Rotation, origin, Scale, SpriteEffects.None, 0f);
            //滞后回声环：略小略暗，制造波的厚度
            spriteBatch.Draw(tex, pos, null, Color * 0.45f, Rotation, origin, Scale * 0.82f, SpriteEffects.None, 0f);
            return false;
        }
    }
}
