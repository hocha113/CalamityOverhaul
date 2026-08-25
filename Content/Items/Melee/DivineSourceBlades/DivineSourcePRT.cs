using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Items.Melee.DivineSourceBlades
{
    /// <summary>金源灭却刃科技三角粒子，四边形走 <see cref="PRTTypes.PRT_CyberSquare"/></summary>
    internal class PRT_DivineTechTriangle : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Triangle";
        public override int InGame_World_MaxCount => 4000;
        public override bool CanPool => true;

        /// <summary>Triangle.png 竖排帧数</summary>
        private const int FrameCount = 6;

        private int frameIdx;
        private float spin;
        private float initialScale;
        private float flickerPhase;
        private Color edgeColor;

        public void Configure(Color edgeColor, int lifeTime) {
            this.edgeColor = edgeColor;
            Lifetime = lifeTime;
            initialScale = Scale;
            frameIdx = Main.rand.Next(FrameCount);
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            spin = Main.rand.NextFloat(0.03f, 0.1f) * (Main.rand.NextBool() ? 1f : -1f);
            flickerPhase = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        public override void Reset() {
            base.Reset();
            frameIdx = 0;
            spin = 0f;
            initialScale = 0f;
            flickerPhase = 0f;
            edgeColor = default;
        }

        public override void SetProperty() => PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;

        public override void AI() {
            Velocity *= 0.94f;
            Rotation += spin;
            float life = LifetimeCompletion;
            if (life > 0.75f) {
                Scale = initialScale * (1f - ((life - 0.75f) / 0.25f));
            }
            //量化闪烁，科技件的事件感
            float flicker = 0.65f + 0.35f * MathF.Sin(Time * 0.9f + flickerPhase);
            Opacity = flicker * (1f - MathF.Pow(life, 2.2f));
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            if (Scale < 0.01f || Opacity < 0.01f) {
                return false;
            }
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            int frameH = tex.Height / FrameCount;
            Rectangle src = new(0, frameH * frameIdx, tex.Width, frameH);
            Vector2 origin = src.Size() * 0.5f;
            Vector2 drawPos = Position - Main.screenPosition;

            spriteBatch.Draw(tex, drawPos, src, edgeColor * (Opacity * 0.5f), Rotation,
                origin, Scale * 1.22f, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, drawPos, src, Color * Opacity, Rotation,
                origin, Scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, drawPos, src, Color.Lerp(Color, Color.White, 0.65f) * (Opacity * 0.8f), Rotation,
                origin, Scale * 0.42f, SpriteEffects.None, 0f);
            return false;
        }
    }
}
