using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 鬼雨雨峰稀有的脸痕竖丝：一道比雨更慢的细长水痕，
    /// 丝身上两点暗窝，像有五官贴着雨幕淌下来。宁少勿滥。
    /// </summary>
    internal class PRT_GhostRainFaceStreak : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 6;

        private Color initialColor;
        private float wavePhase;

        public PRT_GhostRainFaceStreak Configure(int lifetime) {
            Lifetime = lifetime;
            initialColor = Color;
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            wavePhase = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            wavePhase = Main.rand.NextFloat(MathHelper.TwoPi);
            if (Lifetime <= 0) {
                Lifetime = 60;
            }
            if (initialColor == default) {
                initialColor = Color;
            }
        }

        public override void AI() {
            wavePhase += 0.06f;
            //比雨慢得不对劲，横向极轻的游移
            Velocity.X = MathF.Sin(wavePhase) * 0.18f;

            float t = LifetimeCompletion;
            float envelope = MathF.Sin(MathHelper.Pi * MathF.Min(t * 1.15f, 1f));
            Color = initialColor * envelope;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;

            //细长丝身
            Vector2 body = new Vector2(0.10f, 2.6f) * Scale;
            spriteBatch.Draw(tex, pos, null, Color, 0f, origin, body, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, Color * 0.55f, 0f, origin,
                body * new Vector2(2.1f, 0.92f), SpriteEffects.None, 0f);

            //两点暗窝：比丝身沉一截，读作残颜
            Color socket = new Color(26, 32, 36) * (Color.A / 255f * 0.8f);
            float eyeY = -tex.Height * body.Y * 0.16f;
            float eyeGap = MathF.Max(2.6f, 3.4f * Scale);
            spriteBatch.Draw(tex, pos + new Vector2(-eyeGap, eyeY), null, socket, 0f, origin,
                new Vector2(0.06f, 0.08f) * Scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos + new Vector2(eyeGap, eyeY), null, socket, 0f, origin,
                new Vector2(0.06f, 0.08f) * Scale, SpriteEffects.None, 0f);
            return false;
        }
    }
}
