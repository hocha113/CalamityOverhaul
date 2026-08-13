using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 鬼梦烬灰：缓慢上浮、横向游移的黑红碎屑；少数是仍在烧的烬点（加色微光）。
    /// Masking/DiffusionCircle 真 alpha，暗片走 AlphaBlend、烬点走加色
    /// </summary>
    internal class PRT_KikasaDreamAsh : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "DiffusionCircle";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 160;

        private Color initialColor;
        private float waftPhase;
        private float waftAmp;
        private bool ember;

        public PRT_KikasaDreamAsh Configure(int lifetime, bool isEmber) {
            Lifetime = lifetime;
            ember = isEmber;
            initialColor = Color;
            PRTDrawMode = isEmber ? PRTDrawModeEnum.AdditiveBlend : PRTDrawModeEnum.AlphaBlend;
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            waftPhase = 0f;
            waftAmp = 0f;
            ember = false;
        }

        public override void SetProperty() {
            PRTDrawMode = ember ? PRTDrawModeEnum.AdditiveBlend : PRTDrawModeEnum.AlphaBlend;
            waftPhase = Main.rand.NextFloat(MathHelper.TwoPi);
            waftAmp = Main.rand.NextFloat(0.05f, 0.16f);
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            if (Lifetime <= 0) {
                Lifetime = 150;
            }
            if (initialColor == default) {
                initialColor = Color;
            }
        }

        public override void AI() {
            waftPhase += 0.045f;
            Velocity.X = Velocity.X * 0.98f + MathF.Sin(waftPhase) * waftAmp * 0.1f;
            Velocity.Y *= 0.992f;
            Rotation += Velocity.X * 0.02f;

            float t = LifetimeCompletion;
            float envelope = MathF.Sin(MathHelper.Pi * t);
            //烬点烧着烧着暗下去，灰片全程低调
            float glow = ember ? 0.85f - t * 0.35f : 0.5f;
            Color = initialColor * (envelope * glow);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            spriteBatch.Draw(tex, Position - Main.screenPosition, null, Color, Rotation,
                origin, Scale, SpriteEffects.None, 0f);
            return false;
        }
    }
}
