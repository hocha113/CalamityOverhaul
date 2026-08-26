using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Hollowdeep
{
    /// <summary>「空聆」尘埃光斑：亮处缓浮的微尘，轻微布朗漂移与闪烁</summary>
    internal class PRT_HollowdeepMote : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "SoftGlow";
        public override bool CanPool => true;

        private float wobblePhase;

        public PRT_HollowdeepMote Configure(int lifetime) {
            Lifetime = lifetime;
            wobblePhase = Main.rand.NextFloat(MathHelper.TwoPi);
            return this;
        }

        public override void Reset() {
            base.Reset();
            wobblePhase = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            Opacity = 0f;
        }

        public override void AI() {
            wobblePhase += 0.05f;
            Velocity.X += MathF.Sin(wobblePhase) * 0.004f;
            Velocity *= 0.995f;
            //缓入缓出 × 微闪
            float envelope = MathF.Sin(LifetimeCompletion * MathHelper.Pi);
            float twinkle = 0.68f + 0.32f * MathF.Sin(wobblePhase * 2.6f);
            Opacity = envelope * twinkle;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 drawPos = Position - Main.screenPosition;
            Vector2 origin = tex.Size() / 2f;
            spriteBatch.Draw(tex, drawPos, null, Color * (Opacity * 0.3f),
                0f, origin, Scale * 1.7f, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, drawPos, null, Color * (Opacity * 0.42f),
                0f, origin, Scale, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>「萤缀」洞顶萤火：暖光小虫，节律性明灭 + 蜿蜒漫游</summary>
    internal class PRT_HollowdeepFirefly : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "SoftGlow";
        public override bool CanPool => true;

        private float meanderPhase;
        private float blinkPhase;
        private float blinkRate;

        public PRT_HollowdeepFirefly Configure(int lifetime) {
            Lifetime = lifetime;
            meanderPhase = Main.rand.NextFloat(MathHelper.TwoPi);
            blinkPhase = Main.rand.NextFloat(MathHelper.TwoPi);
            blinkRate = Main.rand.NextFloat(0.09f, 0.14f);
            return this;
        }

        public override void Reset() {
            base.Reset();
            meanderPhase = 0f;
            blinkPhase = 0f;
            blinkRate = 0.11f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            Opacity = 0f;
        }

        public override void AI() {
            meanderPhase += 0.04f;
            blinkPhase += blinkRate;
            Velocity.X += MathF.Sin(meanderPhase) * 0.01f;
            Velocity.Y += MathF.Cos(meanderPhase * 0.8f) * 0.008f;
            if (Velocity.LengthSquared() > 0.36f) {
                Velocity *= 0.96f;
            }
            //明灭平方锐化：亮拍短促、暗拍留底光
            float blink = 0.5f + 0.5f * MathF.Sin(blinkPhase);
            float envelope = MathF.Sin(LifetimeCompletion * MathHelper.Pi);
            Opacity = envelope * (0.22f + 0.78f * blink * blink);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 drawPos = Position - Main.screenPosition;
            Vector2 origin = tex.Size() / 2f;
            //外晕 + 亮芯（暖琥珀，不给纯白常驻芯）
            spriteBatch.Draw(tex, drawPos, null, Color * (Opacity * 0.35f),
                0f, origin, Scale * 2.1f, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, drawPos, null, Color * (Opacity * 0.85f),
                0f, origin, Scale * 0.8f, SpriteEffects.None, 0f);
            return false;
        }
    }
}
