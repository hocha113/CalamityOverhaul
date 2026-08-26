using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 霜晶闪点:塔身挂霜与冻结爆裂的冰晶反光。StarTexture 黑底加色批,
    /// 正弦包络的一闪即逝,微小尺度只做点缀
    /// </summary>
    internal class PRT_DefFrostGlint : BasePRT
    {
        public override int InGame_World_MaxCount => 90;
        public override string Texture => CWRConstant.Masking + "StarTexture";
        public override bool CanPool => true;

        private Color initialColor;
        private float twinkleRate;

        public PRT_DefFrostGlint Configure(int lifetime) {
            Lifetime = lifetime;
            initialColor = Color with { A = 255 };
            twinkleRate = Main.rand.NextFloat(0.8f, 1.3f);
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            return this;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            twinkleRate = 1f;
        }

        public override void AI() {
            Velocity *= 0.94f;
            float t = LifetimeCompletion;
            //正弦包络:亮起再熄,不驻留
            float env = MathF.Sin(MathHelper.Clamp(t, 0f, 1f) * MathHelper.Pi);
            Color = initialColor * (env * twinkleRate);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;
            //十字闪+斜十字弱一层,读作晶面反光
            spriteBatch.Draw(tex, pos, null, Color, Rotation, origin, Scale * 0.10f, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, Color * 0.5f, Rotation + MathHelper.PiOver4, origin,
                Scale * 0.06f, SpriteEffects.None, 0f);
            return false;
        }
    }
}
