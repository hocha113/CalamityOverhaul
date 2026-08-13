using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>故障数据条，不稳定/相位机匣共用，跳位+闪断+RGB残影</summary>
    internal class PRT_SHPCGlitchShard : BasePRT
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override int InGame_World_MaxCount => 1200;

        private float initialScale;
        private Color accentColor;
        private bool swapped;
        private int jumpCountdown;
        private float flickerPhase;

        public override bool CanPool => true;

        /// <param name="rotationOverride">定向碎条（链线/残影）传入角度，缺省随机横竖</param>
        public void Configure(Color accentColor, int lifeTime, float? rotationOverride = null) {
            this.accentColor = accentColor;
            Lifetime = lifeTime;
            initialScale = Scale;
            Rotation = rotationOverride ?? (Main.rand.NextBool(4) ? MathHelper.PiOver2 : 0f); //多横少竖
            jumpCountdown = Main.rand.Next(3, 7);
            flickerPhase = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        public override void Reset() {
            base.Reset();
            initialScale = 0f;
            accentColor = default;
            swapped = false;
            jumpCountdown = 0;
            flickerPhase = 0f;
        }

        public override void SetProperty() => PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;

        public override void AI() {
            Velocity *= 0.90f;
            //离散跳位+换色
            if (--jumpCountdown <= 0) {
                jumpCountdown = Main.rand.Next(3, 7);
                Position += Main.rand.NextVector2Circular(6f, 4f);
                if (Main.rand.NextBool(3)) swapped = !swapped;
            }
            float life = LifetimeCompletion;
            Scale = life > 0.72f ? initialScale * (1f - (life - 0.72f) / 0.28f) : initialScale;
            //偶发一帧近熄
            float blink = Main.rand.NextBool(11) ? 0.15f : 1f;
            float flicker = 0.78f + 0.22f * MathF.Sin(Time * 1.1f + flickerPhase);
            Opacity = blink * flicker * (1f - MathF.Pow(life, 3f));
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            if (Scale < 0.06f || Opacity < 0.01f) return false;

            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 drawPos = Position - Main.screenPosition;
            Color main = swapped ? accentColor : Color;
            Color edge = swapped ? Color : accentColor;

            Vector2 size = new(11f * Scale, 2.6f * Scale);
            spriteBatch.Draw(pixel, drawPos, new Rectangle(0, 0, 1, 1), edge * (Opacity * 0.45f), Rotation,
                new Vector2(0.5f, 0.5f), size * 1.5f, SpriteEffects.None, 0f);
            spriteBatch.Draw(pixel, drawPos, new Rectangle(0, 0, 1, 1), main * Opacity, Rotation,
                new Vector2(0.5f, 0.5f), size, SpriteEffects.None, 0f);
            //RGB分离残影
            Vector2 chromOff = new(3f * Scale, 0f);
            spriteBatch.Draw(pixel, drawPos - chromOff, new Rectangle(0, 0, 1, 1),
                (main with { G = 30, B = 30 }) * (Opacity * 0.30f), Rotation,
                new Vector2(0.5f, 0.5f), size * 0.9f, SpriteEffects.None, 0f);
            spriteBatch.Draw(pixel, drawPos + chromOff, new Rectangle(0, 0, 1, 1),
                (main with { R = 30, G = 60 }) * (Opacity * 0.30f), Rotation,
                new Vector2(0.5f, 0.5f), size * 0.9f, SpriteEffects.None, 0f);

            return false;
        }
    }
}
