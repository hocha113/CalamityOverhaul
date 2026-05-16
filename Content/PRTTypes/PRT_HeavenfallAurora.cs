using CalamityOverhaul.Content.Items.Ranged.HeavenfallLongbows;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 极光丝带粒子 —— 服务于天堂陨落长弓家族
    /// <br/>使用 Airflow 流线灰度图沿运动方向被拉伸成丝带, 颜色沿生命周期在彩虹间循环
    /// <br/>用于充能爆发、Q 技能万象生成、命中环等需要大体量"震撼"瞬间
    /// </summary>
    internal class PRT_HeavenfallAurora : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Airflow";
        public override int InGame_World_MaxCount => 4000;

        [VaultLoaden(CWRConstant.Masking + "SoftGlow")]
        internal static Asset<Texture2D> AuroraGlow = null;

        private float StretchScale;       //横向拉伸 (像素长度因子)
        private float ThicknessScale;     //纵向厚度 (像素厚度因子)
        private float HuePhase;           //初始色相
        private float HueSpeed;           //色相滚动速度
        private float SpinSpeed;          //轻微旋转
        private float DriftScale;         //寿命结束前再次拉长的强度

        /// <param name="position">起始位置</param>
        /// <param name="velocity">速度向量 (会决定旋转/拉伸方向)</param>
        /// <param name="stretchScale">丝带拉伸的像素长度参考 (推荐 60~180)</param>
        /// <param name="thicknessScale">丝带厚度的像素参考 (推荐 10~30)</param>
        /// <param name="lifetime">寿命帧数</param>
        /// <param name="huePhase">初始色相 0~1, 决定起始颜色</param>
        /// <param name="hueSpeed">色相滚动速度, 推荐 0.005~0.03</param>
        /// <param name="driftScale">末段额外拉长系数, 推荐 0.4~1.2</param>
        public PRT_HeavenfallAurora(Vector2 position, Vector2 velocity
            , float stretchScale, float thicknessScale, int lifetime
            , float huePhase = 0f, float hueSpeed = 0.012f, float driftScale = 0.7f) {
            Position = position;
            Velocity = velocity;
            StretchScale = stretchScale;
            ThicknessScale = thicknessScale;
            Lifetime = lifetime;
            HuePhase = huePhase;
            HueSpeed = hueSpeed;
            DriftScale = driftScale;
            SpinSpeed = Main.rand.NextFloat(-0.015f, 0.015f);
            Scale = 1f;
            Color = ResolveRainbow(HuePhase);
            //初始角度按运动方向 (无速度时随机)
            Rotation = velocity.LengthSquared() > 0.01f
                ? velocity.ToRotation()
                : Main.rand.NextFloat(MathHelper.TwoPi);
        }

        public override void SetProperty() => PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;

        public override void AI() {
            float life = LifetimeCompletion;

            //速度逐渐衰减, 像极光自然飘散
            Velocity *= 0.965f;

            //轻微旋转
            Rotation += SpinSpeed;

            //彩虹色相滚动
            HuePhase += HueSpeed;
            Color = ResolveRainbow(HuePhase);

            //透明度: 快速入场 + 后段平滑退场
            float fadeIn = MathHelper.Clamp(life * 6f, 0f, 1f);
            float fadeOut = 1f - MathHelper.Clamp((life - 0.5f) / 0.5f, 0f, 1f);
            fadeOut = MathF.Pow(fadeOut, 1.6f);
            Opacity = fadeIn * fadeOut;

            //尺寸: 末段额外拉长, 营造"飘散"感
            Scale = 1f + life * DriftScale;

            Lighting.AddLight(Position, Color.R / 255f * Opacity * 0.45f
                , Color.G / 255f * Opacity * 0.45f, Color.B / 255f * Opacity * 0.45f);
        }

        private static Color ResolveRainbow(float phase) {
            //循环采样 HeavenfallLongbow.rainbowColors 数组, 与全家族色调一致
            phase = phase - MathF.Floor(phase);
            if (phase < 0) {
                phase += 1f;
            }
            return VaultUtils.MultiStepColorLerp(phase, HeavenfallLongbow.rainbowColors);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            if (Opacity < 0.02f) {
                return false;
            }

            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Texture2D glow = AuroraGlow?.Value;

            Vector2 drawPos = Position - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;

            //丝带形状: 沿 Rotation 方向拉伸 StretchScale, 垂直方向 ThicknessScale
            float lengthPx = StretchScale * Scale;
            float thicknessPx = ThicknessScale * (0.85f + 0.15f * MathF.Sin(Time * 0.25f));
            Vector2 sizePx = new(lengthPx, thicknessPx);
            Vector2 scaleVec = new(sizePx.X / tex.Width, sizePx.Y / tex.Height);

            //底层柔光晕 (圆形 SoftGlow), 染丝带主色, 让丝带"发光"
            if (glow != null) {
                Color glowColor = Color * Opacity * 0.45f;
                glowColor.A = 0;
                float glowSize = thicknessPx * 1.8f / glow.Width;
                spriteBatch.Draw(glow, drawPos, null, glowColor, 0f
                    , glow.Size() * 0.5f, glowSize, SpriteEffects.None, 0f);
            }

            //外层柔丝 (略大略透明)
            Color softCol = Color * Opacity * 0.35f;
            softCol.A = 0;
            spriteBatch.Draw(tex, drawPos, null, softCol, Rotation, origin
                , scaleVec * new Vector2(1.05f, 1.5f), SpriteEffects.None, 0f);

            //主丝带
            Color mainCol = Color * Opacity;
            mainCol.A = 0;
            spriteBatch.Draw(tex, drawPos, null, mainCol, Rotation, origin, scaleVec
                , SpriteEffects.None, 0f);

            //核心白热细线
            Color core = Color.Lerp(Color, Color.White, 0.65f) * Opacity * 0.85f;
            core.A = 0;
            spriteBatch.Draw(tex, drawPos, null, core, Rotation, origin
                , scaleVec * new Vector2(1f, 0.5f), SpriteEffects.None, 0f);

            return false;
        }
    }
}
