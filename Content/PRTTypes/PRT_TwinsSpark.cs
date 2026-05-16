using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 双子魔眼专用火花/激光粒子
    /// 模式 0 = 激光眼(青紫)，模式 1 = 魔焰眼(橙红)
    /// 包含一束方向化的能量条 + 一颗发光内核，飞行轨迹自然收束
    /// </summary>
    internal class PRT_TwinsSpark : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_193_White";

        public int EyeMode;
        public Color InitialColor;
        public Color GlowColor;
        private float baseScale;
        private float wobble;

        /// <summary>
        /// 创建一颗双子魔眼粒子。
        /// </summary>
        /// <param name="position">起始位置</param>
        /// <param name="velocity">初始速度</param>
        /// <param name="lifetime">寿命(帧)</param>
        /// <param name="scale">基础缩放</param>
        /// <param name="eyeMode">0=激光眼 1=魔焰眼</param>
        public PRT_TwinsSpark(Vector2 position, Vector2 velocity, int lifetime, float scale, int eyeMode) {
            Position = position;
            Velocity = velocity;
            Lifetime = lifetime;
            Scale = scale;
            baseScale = scale;
            EyeMode = eyeMode;
            wobble = Main.rand.NextFloat(MathHelper.TwoPi);

            if (eyeMode == 1) {
                //魔焰眼:橙红渐变核心带金色辉光
                InitialColor = new Color(255, 110, 35);
                GlowColor = new Color(255, 220, 120);
            }
            else {
                //激光眼:青蓝渐变核心带紫色辉光
                InitialColor = new Color(120, 200, 255);
                GlowColor = new Color(180, 130, 255);
            }
            Color = InitialColor;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
        }

        public override void AI() {
            float t = LifetimeCompletion;

            //速度衰减 + 微小侧向漂移营造能量流体感
            Velocity *= 0.93f;
            Vector2 perp = new Vector2(-Velocity.Y, Velocity.X).SafeNormalize(Vector2.Zero);
            Velocity += perp * (float)Math.Sin(wobble + t * 8f) * 0.18f;

            //生命中段最亮，两端透明
            Opacity = (float)Math.Sin(t * Math.PI);

            //尺寸:前段保持，后段快速收缩
            Scale = baseScale * (1f - t * t * 0.85f);

            //颜色:从初始色向辉光色过渡，再淡出
            Color = Color.Lerp(InitialColor, GlowColor, t * 0.7f);

            Rotation = Velocity.ToRotation();
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = CWRAsset.SoftGlow.Value;
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 drawPos = Position - Main.screenPosition;

            //速度方向决定纵向拉伸:速度越快越像激光束
            float stretch = MathHelper.Clamp(Velocity.Length() * 0.12f, 1f, 4f);
            Vector2 stretchScale = new Vector2(Scale * 0.18f, Scale * 0.18f * stretch);

            //外层柔光
            spriteBatch.Draw(tex, drawPos, null, Color * Opacity * 0.6f,
                Rotation + MathHelper.PiOver2, origin, stretchScale * 2.2f, SpriteEffects.None, 0f);

            //中层条状能量
            spriteBatch.Draw(tex, drawPos, null, GlowColor * Opacity * 0.8f,
                Rotation + MathHelper.PiOver2, origin, stretchScale * 1.2f, SpriteEffects.None, 0f);

            //内核高光
            spriteBatch.Draw(tex, drawPos, null, Color.White * Opacity,
                Rotation + MathHelper.PiOver2, origin, stretchScale * 0.55f, SpriteEffects.None, 0f);

            return false;
        }
    }
}
