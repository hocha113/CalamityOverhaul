using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>赛博方形粒子，CyberTraceBeam</summary>
    internal class PRT_CyberSquare : BasePRT
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override int InGame_World_MaxCount => 6000;

        private float initialScale;
        private float rotationSpeed;
        private float aspectRatio;
        private Color edgeColor;
        private float flickerPhase;

        public override bool CanPool => true;
        public void Configure(Color edgeColor, int lifeTime) {
            this.edgeColor = edgeColor;
            Lifetime = lifeTime;
            initialScale = Scale;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            rotationSpeed = Main.rand.NextFloat(0.02f, 0.08f) * (Main.rand.NextBool() ? 1f : -1f);
            aspectRatio = Main.rand.NextFloat(0.5f, 1.5f);
            flickerPhase = Main.rand.NextFloat(MathHelper.TwoPi);
        }
        public override void Reset() {
            base.Reset();
            initialScale = 0f;
            rotationSpeed = 0f;
            aspectRatio = 1f;
            edgeColor = default;
            flickerPhase = 0f;
        }
        public PRT_CyberSquare() {
            aspectRatio = 1f;
        }
        public override void SetProperty() => PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;

        public override void AI() {
            Velocity *= 0.96f;
            Rotation += rotationSpeed;
            //后20%快缩
            float life = LifetimeCompletion;
            if (life > 0.8f) {
                Scale = initialScale * (1f - (life - 0.8f) / 0.2f);
            }
            float flicker = 0.7f + 0.3f * MathF.Sin(Time * 0.8f + flickerPhase);
            Opacity = flicker * (1f - MathF.Pow(life, 2.5f));
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            if (Scale < 0.1f || Opacity < 0.01f) return false;

            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 drawPos = Position - Main.screenPosition;

            float w = 6f * Scale;
            float h = 6f * Scale * aspectRatio;
            Vector2 size = new(w, h);
            Vector2 origin = new(0.5f, 0.5f);

            Color outer = edgeColor * Opacity * 0.4f;
            spriteBatch.Draw(pixel, drawPos, new Rectangle(0, 0, 1, 1), outer, Rotation,
                origin, size * 1.4f, SpriteEffects.None, 0f);

            Color inner = Color * Opacity;
            spriteBatch.Draw(pixel, drawPos, new Rectangle(0, 0, 1, 1), inner, Rotation,
                origin, size, SpriteEffects.None, 0f);

            Color core = Color.Lerp(inner, Color.White, 0.6f) * Opacity;
            spriteBatch.Draw(pixel, drawPos, new Rectangle(0, 0, 1, 1), core, Rotation,
                origin, size * 0.4f, SpriteEffects.None, 0f);

            return false;
        }
    }
}
