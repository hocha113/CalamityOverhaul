using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 鬼雨贴地潮雾：慢、横移、低对比，非加色（SmokeSheet01 白RGB+Alpha）。
    /// </summary>
    internal class PRT_GhostRainMist : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "SmokeSheet01";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 40;

        private Color initialColor;
        private int frameIndex;
        private float drift;

        public PRT_GhostRainMist Configure(int lifetime) {
            Lifetime = lifetime;
            initialColor = Color;
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            frameIndex = 0;
            drift = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            frameIndex = Main.rand.Next(4);
            drift = Main.rand.NextFloat(-0.006f, 0.006f);
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            if (Lifetime <= 0) {
                Lifetime = 120;
            }
            if (initialColor == default) {
                initialColor = Color;
            }
        }

        public override void AI() {
            Velocity.X *= 0.995f;
            Velocity.Y *= 0.97f;
            Rotation += drift;
            Scale += 0.0022f;

            //入出场都软：正弦包络压透明度
            float t = LifetimeCompletion;
            float envelope = MathF.Sin(MathHelper.Pi * t);
            Color = initialColor * (0.34f * envelope);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            //2×2 序列帧，帧边长按贴图实际尺寸取
            int frameSize = tex.Width / 2;
            Rectangle frame = new(frameIndex % 2 * frameSize, frameIndex / 2 * frameSize,
                frameSize, frameSize);
            spriteBatch.Draw(tex, Position - Main.screenPosition, frame, Color, Rotation,
                frame.Size() * 0.5f, Scale, SpriteEffects.None, 0f);
            return false;
        }
    }
}
