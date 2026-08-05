using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 干花瓣：绯嫁散场的冷喜介质。哑光深绯、非加色，
    /// 缓落带侧摆与翻面透视，末段整瓣褪淡，不发光不弹跳。
    /// </summary>
    internal class PRT_BrideDryPetal : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 90;

        private Color initialColor;
        private float swayPhase;
        private float swayRate;
        private float spin;
        private float fallCap;

        public PRT_BrideDryPetal Configure(int lifetime, float fallSpeed = 0.55f) {
            Lifetime = lifetime;
            initialColor = Color;
            fallCap = fallSpeed;
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            swayPhase = 0f;
            swayRate = 0f;
            spin = 0f;
            fallCap = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            Opacity = 1f;
            swayPhase = Main.rand.NextFloat(MathHelper.TwoPi);
            swayRate = Main.rand.NextFloat(0.05f, 0.09f);
            spin = Main.rand.NextFloat(0.02f, 0.05f) * (Main.rand.NextBool() ? 1f : -1f);
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(70, 110);
            }
            if (fallCap <= 0f) {
                fallCap = 0.55f;
            }
            if (initialColor == default) {
                initialColor = Color;
            }
        }

        public override void AI() {
            swayPhase += swayRate;
            //侧摆主导，重力只把落速缓推到帽值
            Velocity.X = Velocity.X * 0.96f + MathF.Sin(swayPhase) * 0.042f;
            Velocity.Y = Math.Min(Velocity.Y + 0.012f, fallCap);
            Rotation += spin + MathF.Sin(swayPhase * 0.7f) * 0.02f;

            float t = LifetimeCompletion;
            float fade = MathHelper.Clamp((t - 0.68f) / 0.32f, 0f, 1f);
            Color = Color.Lerp(initialColor, Color.Transparent, MathF.Pow(fade, 1.4f));
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;
            //翻面透视：横向宽度随摆相呼吸，读作瓣面翻转而非圆点
            float flutter = 0.55f + 0.45f * MathF.Sin(swayPhase * 1.35f);
            Vector2 scale = new Vector2(0.30f * flutter, 0.52f) * Scale;

            spriteBatch.Draw(tex, pos, null, Color, Rotation, origin, scale,
                SpriteEffects.None, 0f);
            //瓣缘沉色一线，压出干瓣的厚度
            Color rim = new Color(Color.R / 255f * 0.55f, Color.G / 255f * 0.5f,
                Color.B / 255f * 0.5f) * (Color.A / 255f);
            spriteBatch.Draw(tex, pos + Rotation.ToRotationVector2() * 1.2f, null, rim,
                Rotation, origin, scale * new Vector2(0.7f, 0.92f), SpriteEffects.None, 0f);
            return false;
        }
    }
}
