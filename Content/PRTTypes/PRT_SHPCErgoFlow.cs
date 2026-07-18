using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>人体工学枪托流线粒子：沿速度方向拉伸的气流光线，ErgonomicStockModule</summary>
    internal class PRT_SHPCErgoFlow : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "LightShot";
        public override int InGame_World_MaxCount => 1200;
        public override bool CanPool => true;

        private Color initialColor;
        private float initialScale;

        public PRT_SHPCErgoFlow Configure(int lifetime) {
            Lifetime = lifetime;
            initialColor = Color;
            initialScale = Scale;
            if (Velocity.LengthSquared() > 0.01f) {
                Rotation = Velocity.ToRotation();
            }
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            initialScale = 0f;
        }

        public override void SetProperty() => PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;

        public override void AI() {
            Velocity *= 0.93f;
            float life = LifetimeCompletion;
            //前20%淡入，之后三次方衰减
            float fadeIn = MathF.Min(life / 0.2f, 1f);
            Color = initialColor * (fadeIn * (1f - MathF.Pow(life, 3f)));
            Scale = initialScale * (1f - life * 0.35f);
            if (Velocity.LengthSquared() > 0.01f) {
                Rotation = Velocity.ToRotation();
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 drawPos = Position - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            //沿运动方向拉伸的细流线：速度越快越长
            float stretch = 0.10f + Velocity.Length() * 0.02f;
            Vector2 scale = new Vector2(stretch, 0.028f) * Scale;
            spriteBatch.Draw(tex, drawPos, null, Color, Rotation, origin, scale, SpriteEffects.None, 0f);
            //亮芯
            spriteBatch.Draw(tex, drawPos, null, Color.Lerp(Color, Color.White, 0.5f) * 0.8f,
                Rotation, origin, scale * new Vector2(0.7f, 0.5f), SpriteEffects.None, 0f);
            return false;
        }
    }
}
