using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 加工链碎屑:粉碎机岩屑/回收机零件碎片共用。AlphaBlend 暗体薄片,
    /// 重力坠落带翻滚,矿彩/金属亮缘走 A=0 加亮;metallic 抬高翻面镜闪读作钣金
    /// </summary>
    internal class PRT_ProcChip : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 240;

        private Color bodyColor;
        private Color glintColor;
        private float metallic;
        private float spin;
        private float glintPhase;
        private float aspect;

        public PRT_ProcChip Configure(Color glint, int lifetime, float metallicStrength = 0.3f) {
            bodyColor = Color;
            glintColor = glint;
            metallic = MathHelper.Clamp(metallicStrength, 0f, 1f);
            Lifetime = lifetime;
            return this;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            spin = Main.rand.NextFloat(0.09f, 0.26f) * (Main.rand.NextBool() ? 1f : -1f);
            glintPhase = Main.rand.NextFloat(MathHelper.TwoPi);
            aspect = Main.rand.NextFloat(0.16f, 0.26f);
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(24, 40);
            }
        }

        public override void Reset() {
            base.Reset();
            bodyColor = default;
            glintColor = default;
            metallic = 0f;
            spin = 0f;
            glintPhase = 0f;
            aspect = 0f;
        }

        public override void AI() {
            Velocity.X *= 0.97f;
            Velocity.Y = Math.Min(Velocity.Y + 0.24f, 9f);
            Rotation += spin;
            float t = LifetimeCompletion;
            float fade = MathHelper.Clamp((t - 0.62f) / 0.38f, 0f, 1f);
            Color = bodyColor * (1f - MathF.Pow(fade, 1.6f));
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;
            float alpha = Color.A / 255f;
            //薄片翻滚,横宽随翻面相位呼吸
            float flip = 0.40f + 0.60f * MathF.Abs(MathF.Sin(Rotation + glintPhase));
            Vector2 scale = new Vector2(aspect * flip, 0.46f) * Scale;
            spriteBatch.Draw(tex, pos, null, Color, Rotation, origin, scale, SpriteEffects.None, 0f);
            //矿彩/金属亮缘,A=0 在预乘 AlphaBlend 里作加亮
            Color rim = new Color(glintColor.R, glintColor.G, glintColor.B, 0) * (0.42f * alpha);
            spriteBatch.Draw(tex, pos, null, rim, Rotation, origin,
                scale * new Vector2(1.25f, 1.04f), SpriteEffects.None, 0f);
            //翻面对齐时的窄镜闪,金属件更亮
            float glint = MathF.Pow(MathF.Max(MathF.Cos((Rotation + glintPhase) * 2f), 0f), 20f);
            float glintStrength = 0.18f + metallic * 0.5f;
            if (glint > 0.2f) {
                Color gc = new Color(glintColor.R, glintColor.G, glintColor.B, 0) * (glint * glintStrength * alpha);
                spriteBatch.Draw(tex, pos, null, gc, Rotation, origin,
                    scale * new Vector2(0.5f, 1.3f), SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
