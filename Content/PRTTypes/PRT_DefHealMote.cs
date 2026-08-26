using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 治疗光点:治疗站光环内缓升摇曳的微光,SoftGlow 黑底加色批。
    /// 柔和不抢戏:小尺度+低亮度+淡入淡出
    /// </summary>
    internal class PRT_DefHealMote : BasePRT
    {
        public override int InGame_World_MaxCount => 70;
        public override string Texture => CWRConstant.Masking + "SoftGlow";
        public override bool CanPool => true;

        private Color initialColor;
        private float swayPhase;
        private float swayAmp;

        public PRT_DefHealMote Configure(int lifetime) {
            Lifetime = lifetime;
            initialColor = Color with { A = 255 };
            swayPhase = Main.rand.NextFloat(MathHelper.TwoPi);
            swayAmp = Main.rand.NextFloat(0.10f, 0.22f);
            return this;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            swayPhase = 0f;
            swayAmp = 0f;
        }

        public override void AI() {
            float t = LifetimeCompletion;
            //缓升+左右摇曳
            Velocity.Y = MathHelper.Lerp(Velocity.Y, -0.55f, 0.03f);
            Velocity.X = MathF.Sin(t * 9f + swayPhase) * swayAmp;

            //出生1/5淡入,尾程淡出
            float env = MathF.Min(t / 0.2f, 1f) * MathF.Pow(1f - t, 1.1f);
            Color = initialColor * env;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;
            spriteBatch.Draw(tex, pos, null, Color, 0f, origin, Scale * 0.12f, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, Color * 0.6f, 0f, origin, Scale * 0.055f, SpriteEffects.None, 0f);
            return false;
        }
    }
}
