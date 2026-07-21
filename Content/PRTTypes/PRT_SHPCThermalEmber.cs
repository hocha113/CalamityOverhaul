using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>热成像余烬，热浮+对流摇摆</summary>
    internal class PRT_SHPCThermalEmber : BasePRT
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override int InGame_World_MaxCount => 2000;

        private Color hotColor;
        private Color coldColor;
        private float initialScale;
        private float swayPhase;
        private float swaySpeed;

        public override bool CanPool => true;

        public void Configure(Color coldColor, int lifeTime) {
            this.coldColor = coldColor;
            hotColor = Color;
            Lifetime = lifeTime;
            initialScale = Scale;
            swayPhase = Main.rand.NextFloat(MathHelper.TwoPi);
            swaySpeed = Main.rand.NextFloat(0.12f, 0.24f);
        }

        public override void Reset() {
            base.Reset();
            hotColor = default;
            coldColor = default;
            initialScale = 0f;
            swayPhase = 0f;
            swaySpeed = 0f;
        }

        public override void SetProperty() => PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;

        public override void AI() {
            //横阻、竖升封顶
            Velocity.X *= 0.94f;
            Velocity.Y = MathF.Max(Velocity.Y - 0.045f, -2.6f);
            Position.X += MathF.Sin(Time * swaySpeed + swayPhase) * 0.35f;

            float life = LifetimeCompletion;
            Color = Color.Lerp(hotColor, coldColor, MathF.Pow(life, 1.4f));
            Scale = initialScale * (1f - MathF.Pow(life, 2.2f));
            Opacity = 1f - MathF.Pow(life, 2.6f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            if (Scale < 0.05f || Opacity < 0.01f) return false;

            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 drawPos = Position - Main.screenPosition;
            Vector2 origin = new(0.5f, 0.5f);
            float w = 3.4f * Scale;

            spriteBatch.Draw(pixel, drawPos, new Rectangle(0, 0, 1, 1), Color * (Opacity * 0.35f),
                0f, origin, new Vector2(w * 2.6f), SpriteEffects.None, 0f);
            Color core = Color.Lerp(Color, Color.White, 0.35f) * Opacity;
            spriteBatch.Draw(pixel, drawPos, new Rectangle(0, 0, 1, 1), core,
                0f, origin, new Vector2(w), SpriteEffects.None, 0f);
            return false;
        }
    }
}
