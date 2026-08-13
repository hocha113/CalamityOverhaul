using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>积云枪管云瓣消散，Fog 真雾 AlphaBlend，继承云瓣旋转与镜像</summary>
    internal class PRT_SHPCCumulusPuff : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Fog";
        public override bool CanPool => true;

        private float spin;
        private float baseAlpha;
        private SpriteEffects mirror;

        public PRT_SHPCCumulusPuff Configure(float rotation, bool mirrored, int lifeTime, float alpha = 0.75f) {
            Rotation = rotation;
            mirror = mirrored ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            Lifetime = lifeTime;
            baseAlpha = alpha;
            spin = Main.rand.NextFloat(-0.012f, 0.012f);
            return this;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            if (Lifetime <= 0) Lifetime = 26;
            if (baseAlpha <= 0f) baseAlpha = 0.75f;
        }

        public override void Reset() {
            base.Reset();
            spin = 0f;
            baseAlpha = 0f;
            mirror = SpriteEffects.None;
        }

        public override void AI() {
            Velocity *= 0.955f;
            Rotation += spin;
            Scale += 0.005f;  //撕散胀大
            Opacity = (1f - LifetimeCompletion) * baseAlpha;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            spriteBatch.Draw(tex, Position - Main.screenPosition, null, Color * Opacity,
                Rotation, tex.Size() * 0.5f, Scale, mirror, 0f);
            return false;
        }
    }
}
