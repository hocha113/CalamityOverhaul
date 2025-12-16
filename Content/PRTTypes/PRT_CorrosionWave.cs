using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 腐蚀波纹粒子，用于毒液扩散和腐蚀效果
    /// </summary>
    internal class PRT_CorrosionWave : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "DiffusionCircle4";

        [VaultLoaden("@CalamityMod/Particles/BloomCircle")]
        internal static Asset<Texture2D> BloomTex = null;

        private float originalScale;
        private float maxScale;
        private Vector2 squish;
        private Color waveColor;
        private Color glowColor;
        private float pulsePhase;

        public PRT_CorrosionWave(Vector2 position, float startScale, float endScale, int lifetime, float rotation = 0f) {
            Position = position;
            Velocity = Vector2.Zero;
            originalScale = startScale;
            maxScale = endScale;
            Scale = startScale;
            Lifetime = lifetime;
            Rotation = rotation;
            pulsePhase = Main.rand.NextFloat(MathHelper.TwoPi);

            squish = new Vector2(
                Main.rand.NextFloat(0.8f, 1.2f),
                Main.rand.NextFloat(0.8f, 1.2f)
            );

            //随机腐蚀颜色
            waveColor = Main.rand.NextBool()
                ? new Color(90, 180, 100)
                : new Color(110, 200, 115);
            glowColor = new Color(150, 230, 160);
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            Opacity = 0f;
        }

        public override void AI() {
            //波纹扩散
            float expandProgress = (float)Math.Sin(LifetimeCompletion * MathHelper.PiOver2);
            Scale = MathHelper.Lerp(originalScale, maxScale, expandProgress);

            //透明度：先快速出现，然后缓慢消失
            if (LifetimeCompletion < 0.2f) {
                Opacity = LifetimeCompletion / 0.2f;
            }
            else {
                Opacity = (float)Math.Sin((1f - LifetimeCompletion) * MathHelper.PiOver2);
            }

            //脉冲效果
            pulsePhase += 0.08f;
            float pulseFactor = MathF.Sin(pulsePhase) * 0.15f + 1f;
            Scale *= pulseFactor;

            //缓慢旋转
            Rotation += 0.005f;

            //颜色变化（模拟腐蚀过程）
            Color = Color.Lerp(waveColor, new Color(60, 120, 70), LifetimeCompletion);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            if (BloomTex == null || BloomTex.IsDisposed) {
                return false;
            }

            Texture2D waveTexture = PRTLoader.PRT_IDToTexture[ID];
            Texture2D bloomTexture = BloomTex.Value;

            Vector2 drawPos = Position - Main.screenPosition;
            Vector2 origin = waveTexture.Size() / 2f;

            //绘制底层光晕
            float bloomScale = Scale * 1.5f;
            spriteBatch.Draw(
                bloomTexture,
                drawPos,
                null,
                glowColor * Opacity * 0.4f,
                Rotation * 0.7f,
                bloomTexture.Size() / 2f,
                bloomScale * squish,
                SpriteEffects.None,
                0f
            );

            //绘制主波纹
            spriteBatch.Draw(
                waveTexture,
                drawPos,
                null,
                Color * Opacity,
                Rotation,
                origin,
                Scale * squish,
                SpriteEffects.None,
                0f
            );

            //绘制内圈高光
            float innerScale = Scale * 0.85f;
            spriteBatch.Draw(
                waveTexture,
                drawPos,
                null,
                glowColor * Opacity * 0.6f,
                -Rotation * 0.8f,
                origin,
                innerScale * squish,
                SpriteEffects.None,
                0f
            );

            return false;
        }
    }
}
