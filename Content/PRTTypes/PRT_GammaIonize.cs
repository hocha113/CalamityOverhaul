using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 伽马电离粒子 - 模拟高能辐射击中物质时的电离散射效果
    /// 表现为锐利的紫蓝色短线段，快速闪烁后消散
    /// </summary>
    internal class PRT_GammaIonize : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "LightShot";

        private Color initialColor;
        private float initialScale;
        private float flickerPhase;
        private float deceleration;

        public override bool CanPool => true;
        public PRT_GammaIonize Configure(int lt, float flickerOffset = 0f) {
            Lifetime = lt;
            initialColor = Color;
            initialScale = Scale;
            flickerPhase = flickerOffset;
            deceleration = 0.88f;
            Rotation = Velocity.ToRotation();
            return this;
        }
        public override void Reset() {
            base.Reset();
            initialColor = default;
            initialScale = 0f;
            flickerPhase = 0f;
            deceleration = 0.88f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
        }

        public override void AI() {
            //快速减速
            Velocity *= deceleration;
            if (deceleration > 0.82f) {
                deceleration -= 0.003f;
            }

            //朝速度方向对齐
            if (Velocity.LengthSquared() > 0.5f) {
                Rotation = Velocity.ToRotation();
            }

            float life = LifetimeCompletion;

            //电离闪烁：高频sin叠加随机噪声
            float flicker = (float)Math.Sin((Time + flickerPhase) * 1.2f);
            flicker = flicker > 0 ? 1f : 0.3f; //二值化闪烁

            //缩放：前1/4快速膨胀，之后线性收缩
            if (life < 0.25f) {
                Scale = initialScale * (life / 0.25f);
            }
            else {
                Scale = initialScale * (1f - (life - 0.25f) / 0.75f);
            }

            //颜色渐变：初始色 → 深靛蓝消散
            float fade = (float)Math.Pow(life, 1.5);
            Color = Color.Lerp(initialColor, new Color(40, 20, 100, 0), fade);

            Opacity = (1f - fade) * flicker;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D texture = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = texture.Size() * 0.5f;
            Vector2 drawPosition = Position - Main.screenPosition;

            //非对称缩放：X方向拉长（射线形态），Y方向收窄
            Vector2 drawScale = new Vector2(Scale * 1.6f, Scale * 0.35f);

            //外层辉光 - 宽大柔和
            spriteBatch.Draw(
                texture,
                drawPosition,
                null,
                Color * Opacity * 0.35f,
                Rotation,
                origin,
                drawScale * 2.2f,
                SpriteEffects.None,
                0f
            );

            //核心尖锐线段
            spriteBatch.Draw(
                texture,
                drawPosition,
                null,
                Color * Opacity,
                Rotation,
                origin,
                drawScale,
                SpriteEffects.None,
                0f
            );

            return false;
        }
    }
}
