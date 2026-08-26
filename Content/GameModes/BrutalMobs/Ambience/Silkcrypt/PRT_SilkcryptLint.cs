using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Silkcrypt
{
    /// <summary>
    /// 丝幕缕絮：蛛巢常态氛围的蛛丝纤维，缓坠伴随横向摆游，亮度极低只作空气质感。
    /// Extra_98 真 alpha 纺锤形走 AlphaBlend，非加色无光晕（暗洞里的丝絮不该发光）
    /// </summary>
    internal class PRT_SilkcryptLint : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 120;

        private float swayPhase;
        private float swayAmp;
        private Color baseColor;

        public PRT_SilkcryptLint Configure(int lifetime, float phase) {
            Lifetime = lifetime;
            swayPhase = phase;
            baseColor = Color;
            return this;
        }

        public override void Reset() {
            base.Reset();
            swayPhase = 0f;
            swayAmp = 0f;
            baseColor = default;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            if (Lifetime <= 0) {
                Lifetime = 300;
            }
            swayAmp = 0.14f + Main.rand.NextFloat(0.2f);
            if (baseColor == default) {
                baseColor = Color;
            }
        }

        public override void AI() {
            //缓坠 + 横摆，纤维随摆向倾斜
            Velocity.Y = Math.Min(Velocity.Y + 0.003f, 0.38f);
            Velocity.X = MathF.Sin((Time + swayPhase) * 0.037f) * swayAmp;
            Rotation = Velocity.X * 1.1f;

            float t = LifetimeCompletion;
            float env = Math.Min(t / 0.18f, 1f) * MathHelper.Clamp((1f - t) / 0.25f, 0f, 1f);
            Color = baseColor * env;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;
            //细长纤维：窄横 + 竖长，双层错宽制造一点绒感
            Vector2 body = new Vector2(0.05f, 0.30f) * Scale;
            spriteBatch.Draw(tex, pos, null, Color, Rotation, origin, body, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, Color * 0.55f, Rotation, origin,
                body * new Vector2(0.5f, 1.12f), SpriteEffects.None, 0f);
            return false;
        }
    }
}
