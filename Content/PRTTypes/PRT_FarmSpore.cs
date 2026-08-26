using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 农牧组孢子微尘:普通蘑菇的米白浮尘走 AlphaBlend,发光蘑菇的蓝辉光点走加色并明灭闪烁。
    /// 初速泄掉后转入布朗式漂移;普通孢子缓慢沉降,发光孢子受菌光浮力微微上浮
    /// </summary>
    internal class PRT_FarmSpore : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "DiffusionCircle";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 280;

        private Color initialColor;
        private bool glow;
        private float waftPhase;
        private float waftFreq;
        private float twinkleSeed;
        private float drag;

        /// <summary>glowMode=发光蘑菇孢子(加色);beamDrag 越接近 1 初速保持越久,飞线粒子用</summary>
        public PRT_FarmSpore Configure(int lifetime, bool glowMode, float beamDrag = 0.90f) {
            Lifetime = lifetime;
            glow = glowMode;
            drag = beamDrag;
            initialColor = Color;
            PRTDrawMode = glow ? PRTDrawModeEnum.AdditiveBlend : PRTDrawModeEnum.AlphaBlend;
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            glow = false;
            waftPhase = 0f;
            waftFreq = 0f;
            twinkleSeed = 0f;
            drag = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = glow ? PRTDrawModeEnum.AdditiveBlend : PRTDrawModeEnum.AlphaBlend;
            waftPhase = Main.rand.NextFloat(MathHelper.TwoPi);
            waftFreq = Main.rand.NextFloat(0.03f, 0.07f);
            twinkleSeed = Main.rand.NextFloat(MathHelper.TwoPi);
            //遗漏 Configure 时的护栏,防止永生粒子堆积
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(90, 150);
            }
            if (drag <= 0f) {
                drag = 0.90f;
            }
            if (initialColor == default) {
                initialColor = Color;
            }
        }

        public override void AI() {
            waftPhase += waftFreq;
            Velocity *= drag;
            Velocity.X += MathF.Sin(waftPhase) * 0.012f;
            //沉降与浮力方向相反,是米白尘和蓝辉点的行为分野之一
            Velocity.Y += glow ? -0.003f : 0.0045f;
            Velocity = new Vector2(MathHelper.Clamp(Velocity.X, -2.6f, 2.6f), MathHelper.Clamp(Velocity.Y, -1.4f, 1.4f));

            float t = LifetimeCompletion;
            float envelope = MathF.Min(t * 4f, 1f) * (1f - MathF.Pow(t, 3f));
            float flicker = glow ? 0.72f + 0.28f * MathF.Sin(Time * 0.23f + twinkleSeed) : 1f;
            Opacity = envelope * flicker * (glow ? 0.9f : 0.5f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 drawPos = Position - Main.screenPosition;
            //按贴图实际宽度归一,目标直径十几像素的微尘
            float drawScale = Scale * (glow ? 16f : 13f) / tex.Width;
            spriteBatch.Draw(tex, drawPos, null, initialColor * Opacity, Rotation, origin, drawScale, SpriteEffects.None, 0f);
            if (glow) {
                //亮芯,读作一粒有温度的光点而不是均匀圆斑
                spriteBatch.Draw(tex, drawPos, null, new Color(205, 235, 255) * (Opacity * 0.55f),
                    Rotation, origin, drawScale * 0.45f, SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
