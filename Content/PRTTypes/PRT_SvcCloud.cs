using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 天气控制机云雾团:Masking/Fog 真 alpha,AlphaBlend 直绘可染白云本体。
    /// 聚云/散云共用——语义差异全在生成端的速度场(向心=聚,径向外=散)
    /// </summary>
    internal class PRT_SvcCloud : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Fog";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 96;

        private Color initialColor;
        private float drift;
        private float grow;

        /// <param name="lifetime">寿命帧</param>
        /// <param name="growRate">尺寸增速,散云给正值读作消散膨胀</param>
        public PRT_SvcCloud Configure(int lifetime, float growRate = 0.0016f) {
            Lifetime = lifetime;
            grow = growRate;
            initialColor = Color;
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            drift = 0f;
            grow = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            drift = Main.rand.NextFloat(-0.008f, 0.008f);
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            ai[0] = Main.rand.NextBool() ? 1f : 0f; //Fog 不对称烟羽,镜像防贴纸感
            if (Lifetime <= 0) {
                Lifetime = 90;
            }
            if (initialColor == default) {
                initialColor = Color;
            }
        }

        public override void AI() {
            Velocity *= 0.965f;
            Rotation += drift;
            Scale += grow;

            //入出场都软:正弦包络压透明度
            float envelope = MathF.Sin(MathHelper.Pi * LifetimeCompletion);
            Color = initialColor * (0.42f * envelope);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            SpriteEffects fx = ai[0] == 1f ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            spriteBatch.Draw(tex, Position - Main.screenPosition, null, Color, Rotation,
                tex.Size() * 0.5f, Scale, fx, 0f);
            return false;
        }
    }
}
