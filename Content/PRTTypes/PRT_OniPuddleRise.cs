using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 鬼雨立伞水洼呼出的上浮黑水滴：倒着落的雨，自水面渗出般淡入，
    /// 越升越快（有封顶），横向微摆，速度纵向拉伸，头顶一线青灰湿亮，
    /// 尾段散作潮气。Extra_98 真 alpha，暗体非加色。
    /// </summary>
    internal class PRT_OniPuddleRise : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 140;

        //头顶湿亮的冷灰青，A=0 在 AlphaBlend 批里当加色使
        private static readonly Color HeadSheen = new(176, 192, 196);

        private Color initialColor;
        private float swayPhase;
        private float riseAccel;
        private float alphaNow;

        public PRT_OniPuddleRise Configure(int lifetime, float accel = 0.014f) {
            Lifetime = lifetime;
            riseAccel = accel;
            initialColor = Color;
            swayPhase = Main.rand.NextFloat(MathHelper.TwoPi);
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            swayPhase = 0f;
            riseAccel = 0.014f;
            alphaNow = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            if (Lifetime <= 0) {
                Lifetime = 60;
            }
            if (initialColor == default) {
                initialColor = Color;
            }
        }

        public override void AI() {
            //越升越快但封顶：水挣脱水面，不是被弹上去
            Velocity.Y = MathF.Max(Velocity.Y - riseAccel, -2.4f);
            swayPhase += 0.09f;
            Velocity.X = MathF.Sin(swayPhase) * 0.22f;
            if (Velocity.LengthSquared() > 0.01f) {
                Rotation = Velocity.ToRotation() + MathHelper.PiOver2;
            }

            //前段自水面渗出般淡入，尾段散作潮气
            float t = LifetimeCompletion;
            alphaNow = MathHelper.Clamp(t / 0.16f, 0f, 1f)
                * (1f - MathHelper.Clamp((t - 0.68f) / 0.32f, 0f, 1f));
            Color = initialColor * alphaNow;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;

            //上行越快拉得越长：头圆尾细的倒挂水滴
            float stretch = MathHelper.Clamp(-Velocity.Y * 0.55f, 0f, 1f);
            Vector2 body = new Vector2(0.12f * (1f - stretch * 0.3f),
                0.3f * (1f + stretch * 1.9f)) * Scale;
            spriteBatch.Draw(tex, pos, null, Color, Rotation, origin, body,
                SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, Color * 0.55f, Rotation, origin,
                body * new Vector2(0.5f, 1.05f), SpriteEffects.None, 0f);

            //头顶一线湿亮：迎着伞的那一面反着光
            Color sheen = (HeadSheen with { A = 0 }) * (alphaNow * 0.3f);
            spriteBatch.Draw(tex, pos - new Vector2(0f, 2.5f * Scale), null, sheen,
                Rotation, origin, body * new Vector2(0.55f, 0.28f), SpriteEffects.None, 0f);
            return false;
        }
    }
}
