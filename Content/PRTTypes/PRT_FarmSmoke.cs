using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 生物质炉烟:湿生物质烧出来的绿灰烟团,慢升受风,边胀边淡。
    /// Fog 真 alpha 单帧,同屏多团靠随机朝向+镜像区分
    /// </summary>
    internal class PRT_FarmSmoke : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Fog";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 140;

        private Color initialColor;
        private float spin;
        private bool mirror;
        private float buoyancy;

        public PRT_FarmSmoke Configure(int lifetime, float rise = 0.016f) {
            Lifetime = lifetime;
            buoyancy = rise;
            initialColor = Color;
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            spin = 0f;
            mirror = false;
            buoyancy = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            spin = Main.rand.NextFloat(-0.012f, 0.012f);
            mirror = Main.rand.NextBool();
            if (Lifetime <= 0) {
                Lifetime = 130;
            }
            if (buoyancy <= 0f) {
                buoyancy = 0.016f;
            }
            if (initialColor == default) {
                initialColor = Color;
            }
        }

        public override void AI() {
            //受风漂移+浮力缓升,烟柱在风里歪
            Velocity.X = Velocity.X * 0.985f + Main.windSpeedCurrent * 0.03f;
            Velocity.Y = MathF.Max(Velocity.Y - buoyancy, -0.9f);
            Rotation += spin;
            Scale += 0.0035f;

            float t = LifetimeCompletion;
            float envelope = MathF.Min(t * 7f, 1f) * (1f - MathF.Pow(t, 1.6f));
            Opacity = envelope * 0.42f;
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
