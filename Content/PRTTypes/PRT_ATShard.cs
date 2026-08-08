using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>AT力场碎面，玻璃质梭形碎片翻滚微坠渐隐</summary>
    internal class PRT_ATShard : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;
        private float spin;
        private Color initColor;

        public override void SetProperty() => PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;

        public void Configure(int lifetime, float spin) {
            Lifetime = lifetime;
            this.spin = spin;
            initColor = Color;
        }

        public override void Reset() {
            base.Reset();
            spin = 0f;
            initColor = default;
        }

        public override void AI() {
            Velocity *= 0.962f;
            Velocity.Y += 0.11f;
            Rotation += spin;
            float t = LifetimeCompletion;
            Color = initColor * (1f - t * t);
            Scale *= 0.986f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 scale = new Vector2(0.5f, 1.15f) * Scale;
            spriteBatch.Draw(tex, Position - Main.screenPosition, null, Color
                , Rotation, tex.Size() / 2f, scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, Position - Main.screenPosition, null, Color * 0.55f
                , Rotation + 0.6f, tex.Size() / 2f, scale * 0.62f, SpriteEffects.None, 0f);
            return false;
        }
    }
}
