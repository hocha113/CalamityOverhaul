using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 鬼雨雨喉收束丝：向喉点加速收拢的雨线，速度拉伸，近喉即熄。
    /// </summary>
    internal class PRT_GhostRainYank : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 60;

        private Color initialColor;
        private Vector2 throat;

        public PRT_GhostRainYank Configure(Vector2 throatPos, int lifetime) {
            throat = throatPos;
            Lifetime = lifetime;
            initialColor = Color;
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            throat = Vector2.Zero;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            if (Lifetime <= 0) {
                Lifetime = 22;
            }
            if (initialColor == default) {
                initialColor = Color;
            }
        }

        public override void AI() {
            if (throat == Vector2.Zero) {
                active = false;
                return;
            }
            //向喉点加速收拢
            Vector2 pull = (throat - Position).SafeNormalize(-Vector2.UnitY);
            Vector2 next = Velocity + pull * 1.25f;
            float speed = next.Length();
            if (speed > 19f) {
                next *= 19f / speed;
            }
            Velocity = next;
            Rotation = Velocity.ToRotation() + MathHelper.PiOver2;

            if (Vector2.DistanceSquared(Position, throat) < 14f * 14f) {
                active = false;
                return;
            }
            float t = LifetimeCompletion;
            if (t > 0.7f) {
                Color = Color.Lerp(initialColor, Color.Transparent, (t - 0.7f) / 0.3f);
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;
            float stretch = MathHelper.Clamp(Velocity.Length() * 0.06f, 0f, 1.1f);
            Vector2 scale = new Vector2(0.11f * (1f - stretch * 0.3f),
                0.4f * (1f + stretch * 2.8f)) * Scale;
            spriteBatch.Draw(tex, pos, null, Color, Rotation, origin, scale,
                SpriteEffects.None, 0f);
            return false;
        }
    }
}
