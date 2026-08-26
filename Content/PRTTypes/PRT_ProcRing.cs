using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 加工链完成闪光环:DiffusionCircle4 薄锐缘小环扩张衰减
    /// (Ring01 禁令下的合规小环载体)。Additive
    /// </summary>
    internal class PRT_ProcRing : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "DiffusionCircle4";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 60;

        private float startRadius;
        private float endRadius;
        private Color baseColor;

        public PRT_ProcRing Configure(float startR, float endR, int lifetime) {
            startRadius = startR;
            endRadius = endR;
            baseColor = Color;
            Lifetime = lifetime;
            return this;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            if (Lifetime <= 0) {
                Lifetime = 16;
            }
        }

        public override void Reset() {
            base.Reset();
            startRadius = 0f;
            endRadius = 0f;
            baseColor = default;
        }

        public override void AI() {
            float t = LifetimeCompletion;
            float ease = 1f - (1f - t) * (1f - t);
            Scale = MathHelper.Lerp(startRadius, endRadius, ease);
            Color = baseColor * MathF.Pow(1f - t, 1.5f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            //Scale 语义 = 环半径(px),按贴图宽折算
            float drawScale = Scale * 2f / tex.Width;
            spriteBatch.Draw(tex, Position - Main.screenPosition, null, Color, Rotation,
                tex.Size() * 0.5f, drawScale, SpriteEffects.None, 0f);
            return false;
        }
    }
}
