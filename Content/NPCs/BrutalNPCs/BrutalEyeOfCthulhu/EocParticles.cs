using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu
{
    /// <summary>血雾团，Fog 单帧真 alpha，AlphaBlend 染酒红；随机旋转+镜像防贴纸感</summary>
    internal class PRT_EocBloodMist : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Fog";
        public override bool CanPool => true;

        private float baseAlpha;
        private float spin;

        public PRT_EocBloodMist Configure(int lifetime, float alpha = 0.55f) {
            Lifetime = lifetime;
            baseAlpha = alpha;
            return this;
        }

        public override void SetProperty() {
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            spin = Main.rand.NextFloat(-0.012f, 0.012f);
            //ai[0] 存镜像位，多团同屏防同贴纸
            ai[0] = Main.rand.Next(2);
        }

        public override void Reset() {
            base.Reset();
            baseAlpha = 0.55f;
            spin = 0f;
        }

        public override void AI() {
            float t = LifetimeCompletion;
            //前 25% 涨大，后段缓缩
            Scale += t < 0.25f ? 0.012f : -0.002f;
            Rotation += spin;
            Velocity *= 0.94f;
            //浮升趋势
            Velocity.Y -= 0.008f;
            Opacity = baseAlpha * MathF.Sin(MathF.Min(t * 3.4f, 1f) * MathHelper.PiOver2) * (1f - MathF.Pow(t, 2.6f));
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            SpriteEffects flip = ai[0] > 0.5f ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            spriteBatch.Draw(tex, Position - Main.screenPosition, null, Color * Opacity,
                Rotation, tex.Size() * 0.5f, Scale, flip, 0f);
            return false;
        }
    }

    /// <summary>撕皮碎屑：表皮组织块，带重力翻滚，转阶段/死亡演出用</summary>
    internal class PRT_EocSkinShred : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;

        private float spin;
        private Color initialColor;

        public PRT_EocSkinShred Configure(int lifetime) {
            Lifetime = lifetime;
            initialColor = Color;
            return this;
        }

        public override void SetProperty() {
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            spin = Main.rand.NextFloat(-0.24f, 0.24f);
            ai[0] = Main.rand.NextFloat(0.55f, 1f);   //形状压扁率
            ai[1] = Main.rand.Next(2);                //镜像位
        }

        public override void Reset() {
            base.Reset();
            spin = 0f;
            initialColor = default;
        }

        public override void AI() {
            Velocity.X *= 0.985f;
            Velocity.Y += 0.34f;
            if (Velocity.Y > 13f) {
                Velocity.Y = 13f;
            }
            Rotation += spin * (1f - LifetimeCompletion * 0.5f);
            float t = LifetimeCompletion;
            Color = Color.Lerp(initialColor, Color.Transparent, MathF.Pow(t, 3f));
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            SpriteEffects flip = ai[1] > 0.5f ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            //两层错缩，读作不规则组织块而非圆珠
            Vector2 scaleA = new Vector2(0.55f, 0.55f * ai[0]) * Scale;
            Vector2 scaleB = new Vector2(0.34f * ai[0], 0.5f) * Scale;
            Vector2 pos = Position - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            spriteBatch.Draw(tex, pos, null, Color, Rotation, origin, scaleA, flip, 0f);
            spriteBatch.Draw(tex, pos, null, Color * 0.8f, Rotation + 0.8f, origin, scaleB, flip, 0f);
            return false;
        }
    }
}
