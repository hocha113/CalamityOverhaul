using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>地狱火焰</summary>
    internal class PRT_HellFlame : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "DiffusionCircle3";

        private Color[] hellColors;
        private int timer;
        private float rotationSpeed;
        private int timeLeftMax;
        private float size;
        private float timeLife;
        private float flickerIntensity;
        private float distortionPhase;

        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> StarTexture = null;

        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> SoftGlow = null;

        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> Extra_193 = null;

        public override bool CanPool => true;
        public override void Reset() {
            base.Reset();
            hellColors = null;
            timer = 0;
            rotationSpeed = 0f;
            timeLeftMax = 0;
            size = 0f;
            timeLife = 0f;
            flickerIntensity = 0f;
            distortionPhase = 0f;
        }
        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            float[] origAI = [.. ai];
            ai = new float[4];
            for (int i = 0; i < origAI.Length; i++) {
                ai[i] = origAI[i];
            }

            if (hellColors == null) {
                hellColors = new Color[5];
                hellColors[0] = new Color(255, 200, 80, 255);   //亮橙核
                hellColors[1] = new Color(255, 120, 30, 255);   //橙
                hellColors[2] = new Color(200, 40, 20, 255);    //深红
                hellColors[3] = new Color(140, 20, 40, 255);    //暗红紫
                hellColors[4] = new Color(80, 10, 30, 255);     //边缘暗
            }

            int minLife = ai[2] > 0 ? (int)ai[2] : 60;
            int maxLife = ai[3] > 0 ? (int)ai[3] : 120;
            timeLife = timer = Lifetime = Main.rand.Next(minLife, maxLife);
            timeLeftMax = Lifetime;

            rotationSpeed = Main.rand.NextFloat(-0.15f, 0.15f);
            flickerIntensity = Main.rand.NextFloat(0.6f, 1.0f);
            distortionPhase = Main.rand.NextFloat(0f, MathHelper.TwoPi);

            size = Main.rand.NextFloat(0.6f, 1.4f);

            //ai[0] 0飘/1爆/2螺旋/3环绕
            //ai[1] 强度
            //ai[2]/ai[3] 寿命区间
        }

        public override void AI() {
            float lifeRatio = timeLife / timeLeftMax;

            //快现→持→快灭
            if (lifeRatio > 0.7f) {
                Opacity = MathHelper.Lerp(0f, 1f, (1f - lifeRatio) / 0.3f);
            }
            else if (lifeRatio < 0.2f) {
                Opacity = lifeRatio / 0.2f * flickerIntensity;
            }
            else {
                float flicker = 0.85f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 15f + distortionPhase) * 0.15f;
                Opacity = flicker * flickerIntensity;
            }

            switch ((int)ai[0]) {
                case 0: //上飘+扭
                    ApplyRisingMotion(lifeRatio);
                    break;

                case 1: //爆散
                    ApplyExplosionMotion(lifeRatio);
                    break;

                case 2: //螺旋
                    ApplySpiralMotion(lifeRatio);
                    break;

                case 3: //环绕
                    ApplyOrbitMotion(lifeRatio);
                    break;
            }

            Rotation += rotationSpeed * (1f - lifeRatio * 0.5f);

            //先胀后收
            float sizeCurve = (float)Math.Sin(lifeRatio * MathHelper.Pi);
            Scale = size * (0.5f + sizeCurve * 1.2f) * (ai[1] > 0 ? ai[1] : 1f) * 0.1f;

            timer--;
            timeLife--;
        }

        private void ApplyRisingMotion(float lifeRatio) {
            float sineWave = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 4f + Position.X * 0.01f + distortionPhase);
            Vector2 distortion = new Vector2(sineWave * 0.8f, -1.2f);
            Velocity = Vector2.Lerp(Velocity, distortion, 0.08f);
            Velocity *= 0.98f;
        }

        private void ApplyExplosionMotion(float lifeRatio) {
            float speedCurve = (float)Math.Pow(1f - lifeRatio, 2);
            Velocity *= 0.96f + speedCurve * 0.04f;
        }

        private void ApplySpiralMotion(float lifeRatio) {
            float angle = Rotation + timer * 0.1f;
            Vector2 spiral = new Vector2(
                (float)Math.Cos(angle) * 0.5f,
                -1.5f
            );
            Velocity = Vector2.Lerp(Velocity, spiral, 0.1f);
        }

        private void ApplyOrbitMotion(float lifeRatio) {
            float angle = timer * 0.08f + distortionPhase;
            Vector2 tangent = new Vector2(
                -(float)Math.Sin(angle),
                (float)Math.Cos(angle)
            ) * 2f;
            Velocity = Vector2.Lerp(Velocity, tangent, 0.15f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D mainTex = PRTLoader.PRT_IDToTexture[ID];
            Texture2D starTex = StarTexture?.Value;
            Texture2D glowTex = SoftGlow?.Value;
            Texture2D extraTex = Extra_193?.Value;

            if (mainTex == null) return false;

            float lifeRatio = timeLife / timeLeftMax;
            Vector2 drawPos = Position - Main.screenPosition;

            Color coreColor = GetBlendedColor(lifeRatio, 0f, 0.3f);//核
            Color midColor = GetBlendedColor(lifeRatio, 0.3f, 0.7f);//中
            Color edgeColor = GetBlendedColor(lifeRatio, 0.7f, 1f);//边

            float finalOpacity = Opacity * (ai[1] > 0 ? Math.Min(ai[1], 2f) : 1f);

            //外晕
            if (glowTex != null) {
                spriteBatch.Draw(
                    glowTex,
                    drawPos,
                    new Rectangle(0, 0, 64, 64),
                    edgeColor * (finalOpacity * 0.4f),
                    Rotation * 0.5f,
                    new Vector2(32f, 32f),
                    Scale * 1.8f,
                    SpriteEffects.None,
                    0f
                );
            }

            //主体
            spriteBatch.Draw(
                mainTex,
                drawPos,
                new Rectangle(0, 0, 64, 64),
                midColor * (finalOpacity * 0.8f),
                Rotation,
                new Vector2(32f, 32f),
                Scale * 1.2f,
                SpriteEffects.None,
                0f
            );

            //核亮
            spriteBatch.Draw(
                mainTex,
                drawPos,
                new Rectangle(0, 0, 64, 64),
                coreColor * (finalOpacity * 1.2f),
                Rotation * 1.5f,
                new Vector2(32f, 32f),
                Scale * 0.6f,
                SpriteEffects.None,
                0f
            );

            //星闪
            if (starTex != null && lifeRatio > 0.5f) {
                float starPulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 12f + distortionPhase);
                float starIntensity = (lifeRatio - 0.5f) / 0.5f * (0.6f + starPulse * 0.4f);

                spriteBatch.Draw(
                    starTex,
                    drawPos,
                    null,
                    coreColor * (finalOpacity * starIntensity),
                    Rotation * 2f,
                    starTex.Size() / 2f,
                    Scale * 0.3f,
                    SpriteEffects.None,
                    0f
                );
            }

            //螺旋模式额外扭曲
            if (extraTex != null && ai[0] == 2) {
                float distortPhase = (float)Math.Sin(timer * 0.2f + distortionPhase);
                spriteBatch.Draw(
                    extraTex,
                    drawPos,
                    null,
                    midColor * (finalOpacity * 0.3f * Math.Abs(distortPhase)),
                    Rotation + distortPhase,
                    extraTex.Size() / 2f,
                    Scale * (1f + Math.Abs(distortPhase) * 0.5f),
                    SpriteEffects.None,
                    0f
                );
            }

            return false;
        }

        private Color GetBlendedColor(float lifeRatio, float rangeStart, float rangeEnd) {
            float normalizedLife = 1f - lifeRatio;
            int colorCount = hellColors.Length;
            float colorIndex = normalizedLife * (colorCount - 1);

            int index1 = (int)colorIndex;
            int index2 = Math.Min(index1 + 1, colorCount - 1);
            float blend = colorIndex - index1;

            if (normalizedLife >= rangeStart && normalizedLife <= rangeEnd) {
                float rangeBlend = (normalizedLife - rangeStart) / (rangeEnd - rangeStart);
                blend = MathHelper.Lerp(blend, rangeBlend, 0.5f);
            }

            return Color.Lerp(hellColors[index1], hellColors[index2], blend);
        }
    }
}
