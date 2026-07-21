using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>霰射枪管玻璃薄片翻滚，反光爆闪</summary>
    internal class PRT_SHPCShardGlass : BasePRT
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override int InGame_World_MaxCount => 1500;

        private float initialScale;
        private float spin;
        private float aspect;
        private Color edgeColor;
        private float glintPhase;
        private float glintSpeed;

        public override bool CanPool => true;

        public PRT_SHPCShardGlass Configure(Color edgeColor, int lifeTime) {
            this.edgeColor = edgeColor;
            Lifetime = lifeTime;
            initialScale = Scale;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            spin = Main.rand.NextFloat(0.08f, 0.24f) * (Main.rand.NextBool() ? 1f : -1f);
            aspect = Main.rand.NextFloat(0.16f, 0.3f);
            glintPhase = Main.rand.NextFloat(MathHelper.TwoPi);
            glintSpeed = Main.rand.NextFloat(0.5f, 0.9f);
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialScale = 0f;
            spin = 0f;
            aspect = 0.22f;
            edgeColor = default;
            glintPhase = 0f;
            glintSpeed = 0.7f;
        }

        public override void SetProperty() => PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;

        public override void AI() {
            Velocity = new Vector2(Velocity.X * 0.96f, Velocity.Y * 0.98f + 0.14f);
            Rotation += spin;
            float life = LifetimeCompletion;
            if (life > 0.7f) {
                Scale = initialScale * (1f - (life - 0.7f) / 0.3f);
            }
            //正对视线时尖闪
            float glint = MathF.Pow(MathF.Abs(MathF.Sin(Time * glintSpeed + glintPhase)), 6f);
            Opacity = (0.35f + 0.65f * glint) * (1f - MathF.Pow(life, 3f));
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            if (Scale < 0.08f || Opacity < 0.01f) return false;

            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 drawPos = Position - Main.screenPosition;
            Rectangle src = new(0, 0, 1, 1);
            Vector2 origin = new(0.5f, 0.5f);

            float len = 10f * Scale;
            Vector2 size = new(len, len * aspect);

            spriteBatch.Draw(pixel, drawPos, src, edgeColor * Opacity * 0.35f, Rotation,
                origin, size * 1.5f, SpriteEffects.None, 0f);
            spriteBatch.Draw(pixel, drawPos, src, Color * Opacity, Rotation,
                origin, size, SpriteEffects.None, 0f);
            Color core = Color.Lerp(Color, Color.White, 0.7f) * Opacity;
            spriteBatch.Draw(pixel, drawPos, src, core, Rotation,
                origin, new Vector2(len * 0.45f, len * aspect * 0.6f), SpriteEffects.None, 0f);

            return false;
        }
    }
}
