using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 鬼梦犬黑烟：冲刺与扑咬沿途从身上撕下来的一口口暗雾，
    /// 出生带犬的冲量、几帧内交还给空气，边胀边淡微微上浮。
    /// Fog 真 alpha，AlphaBlend 直接染暗红黑
    /// </summary>
    internal class PRT_KikasaHoundSmoke : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Fog";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 200;

        private Color initialColor;
        private float spin;
        private bool mirror;
        private float expand;

        public PRT_KikasaHoundSmoke Configure(int lifetime, float expandPerFrame = 0.013f) {
            Lifetime = lifetime;
            expand = expandPerFrame;
            initialColor = Color;
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            spin = 0f;
            mirror = false;
            expand = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            spin = Main.rand.NextFloat(-0.03f, 0.03f);
            //同屏多团靠镜像与随机朝向区分,不读成同一张贴纸
            mirror = Main.rand.NextBool();
            if (Lifetime <= 0) {
                Lifetime = 24;
            }
            if (initialColor == default) {
                initialColor = Color;
            }
        }

        public override void AI() {
            //猛泄劲,随后轻轻上浮
            Velocity *= 0.86f;
            Velocity.Y -= 0.014f;
            Rotation += spin;
            Scale += expand;

            float t = LifetimeCompletion;
            float envelope = MathF.Min(t * 5f, 1f) * (1f - MathF.Pow(t, 1.7f));
            Opacity = envelope * 0.62f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            SpriteEffects fxs = mirror ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            spriteBatch.Draw(tex, Position - Main.screenPosition, null, initialColor * Opacity,
                Rotation, tex.Size() * 0.5f, Scale, fxs, 0f);
            return false;
        }
    }
}
