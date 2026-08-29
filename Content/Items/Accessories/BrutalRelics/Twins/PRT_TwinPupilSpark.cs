using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.Twins
{
    /// <summary>
    /// 双瞳系绳专属火花，结构承 PRT_TwinsSpark，色板换遗物双色：
    /// mode 0 视界红激光 / 1 焚瞳青焰
    /// </summary>
    internal class PRT_TwinPupilSpark : BasePRT
    {
        //声明与实画对齐(PreDraw 画 SoftGlow)，免加载无用贴图
        public override string Texture => CWRConstant.Masking + "SoftGlow";

        public Color InitialColor;
        public Color TrailGlow;
        private float baseScale;
        private float wobble;

        public override bool CanPool => true;

        public void Configure(int lifetime, int mode) {
            Lifetime = lifetime;
            baseScale = Scale;
            wobble = Main.rand.NextFloat(MathHelper.TwoPi);
            if (mode == 1) {
                InitialColor = TwinPupilTether.FlameColor;
                TrailGlow = TwinPupilTether.FlameGlow;
            }
            else {
                InitialColor = TwinPupilTether.LaserColor;
                TrailGlow = TwinPupilTether.LaserGlow;
            }
            Color = InitialColor;
        }

        public override void Reset() {
            base.Reset();
            InitialColor = default;
            TrailGlow = default;
            baseScale = 0f;
            wobble = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
        }

        public override void AI() {
            float t = LifetimeCompletion;

            Velocity *= 0.93f;
            Vector2 perp = new Vector2(-Velocity.Y, Velocity.X).SafeNormalize(Vector2.Zero);
            Velocity += perp * (float)Math.Sin(wobble + t * 8f) * 0.18f;

            Opacity = (float)Math.Sin(t * Math.PI);
            //后段快收
            Scale = baseScale * (1f - t * t * 0.85f);
            Color = Color.Lerp(InitialColor, TrailGlow, t * 0.7f);
            Rotation = Velocity.ToRotation();
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = CWRAsset.SoftGlow.Value;
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 drawPos = Position - Main.screenPosition;

            float stretch = MathHelper.Clamp(Velocity.Length() * 0.12f, 1f, 4f);
            Vector2 stretchScale = new(Scale * 0.18f, Scale * 0.18f * stretch);

            spriteBatch.Draw(tex, drawPos, null, Color * Opacity * 0.6f,
                Rotation + MathHelper.PiOver2, origin, stretchScale * 2.2f, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, drawPos, null, TrailGlow * Opacity * 0.8f,
                Rotation + MathHelper.PiOver2, origin, stretchScale * 1.2f, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, drawPos, null, Color.White * Opacity,
                Rotation + MathHelper.PiOver2, origin, stretchScale * 0.55f, SpriteEffects.None, 0f);
            return false;
        }
    }
}
