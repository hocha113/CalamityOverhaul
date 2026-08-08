using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 生樱瓣：樱流散场后仍在飘的那些。半透薄瓣、亮樱到瓣白，
    /// 真翻面（宽度过零，侧棱那一帧只剩一条线）、初速拖尾拉伸后快速衰减、末段横飘不直落。
    /// 与绯嫁干瓣（<see cref="PRT_BrideDryPetal"/>）分野：那是哑光干血带瓣缘沉色，这是透光的活瓣
    /// </summary>
    internal class PRT_OniSakuraDrift : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 120;

        private Color initialColor;
        private float flipPhase;
        private float flipRate;
        private float spin;
        private float driftDir;
        private float fallCap;

        public PRT_OniSakuraDrift Configure(int lifetime, float fallSpeed = 0.42f) {
            Lifetime = lifetime;
            initialColor = Color;
            fallCap = fallSpeed;
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            flipPhase = 0f;
            flipRate = 0f;
            spin = 0f;
            driftDir = 0f;
            fallCap = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            Opacity = 1f;
            flipPhase = Main.rand.NextFloat(MathHelper.TwoPi);
            flipRate = Main.rand.NextFloat(0.10f, 0.19f);
            spin = Main.rand.NextFloat(0.035f, 0.085f) * (Main.rand.NextBool() ? 1f : -1f);
            driftDir = Main.rand.NextBool() ? 1f : -1f;
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(80, 130);
            }
            if (fallCap <= 0f) {
                fallCap = 0.42f;
            }
            if (initialColor == default) {
                initialColor = Color;
            }
        }

        public override void AI() {
            flipPhase += flipRate;
            //被甩出去那一下快速衰减，之后横风主导，重力只把落速缓推到帽值
            Velocity *= 0.90f;
            Velocity.X += driftDir * 0.030f + MathF.Sin(flipPhase * 0.62f) * 0.020f;
            Velocity.Y = Math.Min(Velocity.Y + 0.010f, fallCap);
            Rotation += spin;

            float t = LifetimeCompletion;
            float fade = MathHelper.Clamp((t - 0.62f) / 0.38f, 0f, 1f);
            Color = Color.Lerp(initialColor, Color.Transparent, MathF.Pow(fade, 1.25f));
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;

            //宽度收到零再张开 = 翻面(贴图对称，镜像不携带信息；负缩放会翻转绕序
            //撞上 SpriteBatch 默认的背面剔除，故取绝对值)；纵向随残速拉伸
            float flip = MathF.Abs(MathF.Cos(flipPhase));
            float speedStretch = 1f + MathHelper.Clamp(Velocity.Length() / 7f, 0f, 0.55f);
            Vector2 scale = new Vector2(0.34f * flip, 0.50f * speedStretch) * Scale;

            spriteBatch.Draw(tex, pos, null, Color, Rotation, origin, scale, SpriteEffects.None, 0f);
            //瓣面正对时压一层更白的小瓣，读作薄到透光（干瓣走的是瓣缘沉色，不是这个）
            float facing = MathHelper.Clamp(flip * 1.2f - 0.25f, 0f, 1f);
            if (facing > 0.02f) {
                Color lit = new Color(255, 244, 248) * (Color.A / 255f * facing * 0.42f);
                spriteBatch.Draw(tex, pos, null, lit, Rotation, origin
                    , scale * new Vector2(0.58f, 0.72f), SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
