using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 黑曜石碎屑，速冷玻璃薄片。AlphaBlend 暗体，断口热缘指数冷却，
    /// 翻面镜闪，重力坠落带翻滚；灼烧枪管换色板借用作干烧焦屑
    /// </summary>
    internal class PRT_SHPCObsidianChip : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 150;

        private Color bodyColor;
        private Color rimColor;
        private float rimHeat;
        private float spin;
        private float glintPhase;
        private float aspect;

        public PRT_SHPCObsidianChip Configure(Color rim, int lifetime, float heat = 1f) {
            bodyColor = Color;
            rimColor = rim;
            rimHeat = MathHelper.Clamp(heat, 0f, 1f);
            Lifetime = lifetime;
            return this;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            spin = Main.rand.NextFloat(0.1f, 0.28f) * (Main.rand.NextBool() ? 1f : -1f);
            glintPhase = Main.rand.NextFloat(MathHelper.TwoPi);
            aspect = Main.rand.NextFloat(0.14f, 0.24f);
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(26, 44);
            }
        }

        public override void Reset() {
            base.Reset();
            bodyColor = default;
            rimColor = default;
            rimHeat = 0f;
            spin = 0f;
            glintPhase = 0f;
            aspect = 0f;
        }

        public override void AI() {
            Velocity.X *= 0.97f;
            Velocity.Y = Math.Min(Velocity.Y + 0.24f, 9f);
            Rotation += spin;
            rimHeat *= 0.93f;   //断口速冷
            float t = LifetimeCompletion;
            float fade = MathHelper.Clamp((t - 0.6f) / 0.4f, 0f, 1f);
            Color = bodyColor * (1f - MathF.Pow(fade, 1.6f));
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;
            float alpha = Color.A / 255f;
            //薄片翻滚,横宽随翻面相位呼吸
            float flip = 0.42f + 0.58f * MathF.Abs(MathF.Sin(Rotation + glintPhase));
            Vector2 scale = new Vector2(aspect * flip, 0.5f) * Scale;
            spriteBatch.Draw(tex, pos, null, Color, Rotation, origin, scale, SpriteEffects.None, 0f);
            //断口热缘,冷却后熄灭;A=0 在预乘 AlphaBlend 里作加亮
            if (rimHeat > 0.03f) {
                Color rim = new Color(rimColor.R, rimColor.G, rimColor.B, 0) * (rimHeat * alpha);
                spriteBatch.Draw(tex, pos, null, rim, Rotation, origin,
                    scale * new Vector2(1.3f, 1.05f), SpriteEffects.None, 0f);
            }
            //翻面对齐时的窄镜闪
            float glint = MathF.Pow(MathF.Max(MathF.Cos((Rotation + glintPhase) * 2f), 0f), 24f);
            if (glint > 0.2f) {
                Color gc = new Color(226, 214, 248, 0) * (glint * 0.5f * alpha);
                spriteBatch.Draw(tex, pos, null, gc, Rotation, origin,
                    scale * new Vector2(0.5f, 1.3f), SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
