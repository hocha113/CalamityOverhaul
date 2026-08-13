using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>蜜滴，重力坠落速度拉伸，渐凝渐暗</summary>
    internal class PRT_SHPCHoneyDrop : BasePRT
    {
        //SoftGlow 亮度型黑底，只进加色批
        public override string Texture => CWRConstant.Masking + "SoftGlow";
        public override bool CanPool => true;

        private Color brightHoney;
        private Color darkHoney;

        public PRT_SHPCHoneyDrop Configure(int lifeTime) {
            Lifetime = lifeTime;
            brightHoney = Color;
            darkHoney = new Color(140, 80, 15);
            return this;
        }

        public override void SetProperty() => PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;

        public override void Reset() {
            base.Reset();
            brightHoney = default;
            darkHoney = default;
        }

        public override void AI() {
            //黏滞坠落，横向阻尼+重力
            Velocity = new Vector2(Velocity.X * 0.98f, MathF.Min(Velocity.Y + 0.14f, 7f));
            float t = LifetimeCompletion;
            Opacity = MathF.Min(t * 6f, 1f) * (1f - MathF.Pow(t, 4f));
            //渐凝渐暗
            Color = Color.Lerp(brightHoney, darkHoney, t * 0.8f);
            Rotation = Velocity.ToRotation();
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 pos = Position - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            float vlen = Velocity.Length();
            //速度拉伸，快则长慢则圆
            Vector2 squish = new(1f + vlen * 0.10f, MathHelper.Clamp(1f - vlen * 0.04f, 0.55f, 1f));
            spriteBatch.Draw(tex, pos, null, Color * Opacity, Rotation, origin, Scale * 0.16f * squish, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, new Color(255, 240, 190) * Opacity * 0.5f, Rotation, origin, Scale * 0.075f * squish, SpriteEffects.None, 0f);
            return false;
        }
    }
}
