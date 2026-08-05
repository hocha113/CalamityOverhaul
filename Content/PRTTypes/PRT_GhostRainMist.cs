using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 鬼雨贴地潮雾：慢、横移、低对比；Masking/Fog 真 alpha，AlphaBlend 直绘。
    /// </summary>
    internal class PRT_GhostRainMist : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Fog";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 120;

        private Color initialColor;
        private float drift;

        public PRT_GhostRainMist Configure(int lifetime) {
            Lifetime = lifetime;
            initialColor = Color;
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            drift = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
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
            Vector2 origin = tex.Size() * 0.5f;
            spriteBatch.Draw(tex, Position - Main.screenPosition, null, Color, Rotation,
                origin, Scale, SpriteEffects.None, 0f);
            return false;
        }
    }
}
