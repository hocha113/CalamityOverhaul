using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Woodsong
{
    /// <summary>
    /// 林语光尘：五种飘浮介质共用一个颗粒类（花粉/柳絮/蝶尘/萤火/鬼火烬）。
    /// 微粒是颗粒介质而非效果本体，SoftGlow 只以 2~6px 尺度出现；加色批绘制。
    /// </summary>
    internal class PRT_WoodsongMote : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "SoftGlow";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 220;

        internal const int ModePollen = 0;
        internal const int ModeCatkin = 1;
        internal const int ModeButterfly = 2;
        internal const int ModeFirefly = 3;
        internal const int ModeWispEmber = 4;

        private int mode;
        private float phase;
        private int blinkCycle;
        private Color baseColor;

        public PRT_WoodsongMote Configure(int flightMode, int lifetime) {
            mode = flightMode;
            Lifetime = lifetime;
            baseColor = Color;
            return this;
        }

        public override void Reset() {
            base.Reset();
            mode = ModePollen;
            phase = 0f;
            blinkCycle = 0;
            baseColor = default;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            phase = Main.rand.NextFloat(100f);
            blinkCycle = Main.rand.Next(70, 112);
            if (Lifetime <= 0) {
                Lifetime = 240;
            }
            if (baseColor == default) {
                baseColor = Color;
            }
        }

        public override void AI() {
            float lc = LifetimeCompletion;
            float env = MathF.Sin(MathHelper.Pi * lc);
            switch (mode) {
                case ModeCatkin:
                    //柳絮：极慢坠+大幅横向荡，吃风最重
                    Velocity.X = MathHelper.Lerp(Velocity.X,
                        Main.windSpeedCurrent * 2.2f + MathF.Sin(Time * 0.06f + phase) * 0.24f, 0.012f);
                    Velocity.Y = MathHelper.Lerp(Velocity.Y,
                        0.10f + MathF.Sin(Time * 0.045f + phase * 1.7f) * 0.14f, 0.04f);
                    Opacity = 0.55f * env;
                    break;
                case ModeButterfly:
                    //蝶尘：航向缓慢盘卷，微微向上
                    float heading = phase + MathF.Sin(Time * 0.035f + phase) * 1.9f;
                    Vector2 want = heading.ToRotationVector2() * 0.85f + new Vector2(0f, -0.06f);
                    Velocity = Vector2.Lerp(Velocity, want, 0.05f);
                    Opacity = 0.60f * env;
                    break;
                case ModeFirefly:
                    //萤火：近似悬停的游移+占空比明灭，亮相时给一点冷绿光
                    Velocity = Vector2.Lerp(Velocity, new Vector2(
                        MathF.Sin(Time * 0.021f + phase) * 0.30f,
                        MathF.Sin(Time * 0.017f + phase * 1.6f) * 0.22f), 0.03f);
                    bool lit = (Time + (int)(phase * 13f)) % blinkCycle < 26;
                    Opacity = MathHelper.Lerp(Opacity, lit ? 0.9f * env : 0.05f, 0.25f);
                    if (Opacity > 0.3f) {
                        Lighting.AddLight(Position, 0.10f * Opacity, 0.15f * Opacity, 0.04f * Opacity);
                    }
                    break;
                case ModeWispEmber:
                    //鬼火烬屑：缓升渐灭
                    Velocity *= 0.985f;
                    Velocity.Y -= 0.006f;
                    Opacity = (1f - lc) * 0.7f;
                    break;
                default:
                    //花粉：轻坠+顺风漂
                    Velocity.X = MathHelper.Lerp(Velocity.X, Main.windSpeedCurrent * 1.1f, 0.01f);
                    Velocity.Y = MathHelper.Lerp(Velocity.Y,
                        0.16f + MathF.Sin(Time * 0.05f + phase) * 0.10f, 0.05f);
                    Opacity = 0.50f * env;
                    break;
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            if (Opacity <= 0.01f) {
                return false;
            }
            Texture2D tex = TexValue;
            Vector2 pos = Position - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            //晕层在下、亮芯在上；保留 A 分量，加色批下源因子无论何种配置都可见
            spriteBatch.Draw(tex, pos, null, baseColor * (Opacity * 0.35f), 0f,
                origin, Scale * 1.6f, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, baseColor * Opacity, 0f,
                origin, Scale * 0.5f, SpriteEffects.None, 0f);
            return false;
        }
    }
}
