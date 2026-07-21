using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>延伸枪托碎光，青/品色散，呼应 ExtenderCleave.fx</summary>
    internal class PRT_SHPCExtenderShred : BasePRT
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override int InGame_World_MaxCount => 2000;
        public override bool CanPool => true;

        private static readonly Color DispCyan = new(70, 240, 255);
        private static readonly Color DispMagenta = new(255, 90, 235);

        private Color edgeColor;
        private float initialScale;
        private Vector2 dispAxis;

        public PRT_SHPCExtenderShred Configure(Color edgeColor, int lifetime) {
            this.edgeColor = edgeColor;
            Lifetime = lifetime;
            initialScale = Scale;
            //色散轴=初速垂线
            dispAxis = Velocity.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
            return this;
        }

        public override void Reset() {
            base.Reset();
            edgeColor = default;
            initialScale = 0f;
            dispAxis = default;
        }

        public override void SetProperty() => PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;

        public override void AI() {
            Velocity *= 0.90f;
            if (Velocity.LengthSquared() > 0.01f) {
                Rotation = Velocity.ToRotation();
            }
            float life = LifetimeCompletion;
            Opacity = 1f - MathF.Pow(life, 1.8f);
            Scale = initialScale * (1f - life * 0.35f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            if (Opacity < 0.02f || Scale < 0.05f) return false;

            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 drawPos = Position - Main.screenPosition;
            Rectangle src = new(0, 0, 1, 1);
            Vector2 origin = new(0.5f, 0.5f);

            float len = MathHelper.Clamp(Velocity.Length() * 2.2f, 6f, 26f) * Scale;
            float thick = 2f * Scale;
            //色散随衰减撕开
            Vector2 split = dispAxis * (LifetimeCompletion * 3.2f);

            spriteBatch.Draw(pixel, drawPos + split, src, DispCyan * Opacity * 0.5f, Rotation,
                origin, new Vector2(len, thick), SpriteEffects.None, 0f);
            spriteBatch.Draw(pixel, drawPos - split, src, DispMagenta * Opacity * 0.5f, Rotation,
                origin, new Vector2(len, thick), SpriteEffects.None, 0f);
            spriteBatch.Draw(pixel, drawPos, src, edgeColor * Opacity * 0.55f, Rotation,
                origin, new Vector2(len * 1.15f, thick * 2.0f), SpriteEffects.None, 0f);
            Color core = Color.Lerp(Color, Color.White, 0.55f) * Opacity;
            spriteBatch.Draw(pixel, drawPos, src, core, Rotation,
                origin, new Vector2(len * 0.6f, thick * 0.6f), SpriteEffects.None, 0f);

            return false;
        }
    }
}
