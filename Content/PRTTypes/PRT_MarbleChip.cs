using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>白金石屑，配 <see cref="PRT_Smoke"/> 石尘</summary>
    internal class PRT_MarbleChip : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;

        private float gravity;
        private float spin;

        public PRT_MarbleChip Configure(int lifetime, float gravityStrength = 0.22f) {
            Lifetime = lifetime;
            gravity = gravityStrength;
            spin = Main.rand.NextFloat(0.18f, 0.3f) * (Main.rand.NextBool() ? 1f : -1f);
            return this;
        }

        public override void Reset() {
            base.Reset();
            gravity = 0f;
            spin = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            //防漏 Configure 兜底
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(20, 32);
            }
            if (gravity == 0f) {
                gravity = 0.22f;
            }
            if (spin == 0f) {
                spin = Main.rand.NextFloat(0.18f, 0.3f) * (Main.rand.NextBool() ? 1f : -1f);
            }
        }

        public override void AI() {
            if (Velocity.Y < 14f) {
                Velocity.Y += gravity;
            }
            Velocity.X *= 0.98f;
            Rotation += spin;
            Opacity = MathHelper.Clamp((1f - LifetimeCompletion) * 3f, 0f, 1f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = TexValue;
            Vector2 pos = Position - Main.screenPosition;
            Vector2 origin = tex.Size() / 2f;
            Vector2 scale = new Vector2(0.24f, 0.66f) * Scale;
            Color gold = Color; gold.A = 0;

            //速反向三重残影
            for (int i = 3; i >= 1; i--) {
                float k = i / 3f;
                spriteBatch.Draw(tex, pos - Velocity * (i * 0.85f), null, gold * ((1f - k) * 0.28f + 0.08f) * Opacity
                    , Rotation - spin * i * 2f, origin, scale * (1f - k * 0.3f), SpriteEffects.None, 0f);
            }
            //金边+白芯
            spriteBatch.Draw(tex, pos, null, gold * 0.9f * Opacity, Rotation, origin, scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, Color.White * 0.8f * Opacity, Rotation, origin
                , scale * new Vector2(0.5f, 0.78f), SpriteEffects.None, 0f);
            return false;
        }
    }
}
