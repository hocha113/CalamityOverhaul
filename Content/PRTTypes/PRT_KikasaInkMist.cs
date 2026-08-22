using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 墨雾须:命中/倾覆时在空气里晕开的一小口墨雾
    /// 比空气重,缓慢下沉扩散,Fog 真 alpha 主体可直接染墨色(墨雨普攻自有件)
    /// </summary>
    internal class PRT_KikasaInkMist : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Fog";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 160;

        private Color initialColor;
        private float spin;
        private bool mirror;

        public PRT_KikasaInkMist Configure(int lifetime) {
            Lifetime = lifetime;
            initialColor = Color;
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            spin = 0f;
            mirror = false;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            if (Lifetime <= 0) {
                Lifetime = 36;
            }
            if (initialColor == default) {
                initialColor = Color;
            }
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            spin = Main.rand.NextFloat(-0.02f, 0.02f);
            //同屏多团靠镜像与随机朝向区分,不读成同一张贴纸
            mirror = Main.rand.NextBool();
        }

        public override void AI() {
            //墨雾比空气重:缓慢泄劲、微微下沉、边扩边淡
            Velocity *= 0.94f;
            Velocity.Y += 0.012f;
            Rotation += spin;
            Scale *= 1.009f;

            float t = LifetimeCompletion;
            Color = Color.Lerp(initialColor, KikasaInk.InkBody, t * 0.5f);
            Opacity = (1f - MathF.Pow(t, 1.6f)) * 0.4f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            SpriteEffects fxs = mirror ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            spriteBatch.Draw(tex, Position - Main.screenPosition, null, Color * Opacity,
                Rotation, tex.Size() * 0.5f, 0.24f * Scale, fxs, 0f);
            return false;
        }
    }
}
