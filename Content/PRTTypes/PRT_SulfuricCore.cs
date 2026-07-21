using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>硫酸爆发核心强光</summary>
    internal class PRT_SulfuricCore : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "DiffusionCircle5";

        private float pulseSpeed;
        private float maxPulseScale;
        private Color coreColor;
        private Color haloColor;
        private int burstPhase; //0胀/1闪/2收

        public override bool CanPool => true;
        public PRT_SulfuricCore() {
            pulseSpeed = 0.2f;
            coreColor = new Color(130, 220, 140);
            haloColor = new Color(180, 255, 190);
        }
        public PRT_SulfuricCore(Vector2 position, float scale, int lifetime) {
            Position = position;
            Velocity = Vector2.Zero;
            Scale = scale;
            Lifetime = lifetime;
            pulseSpeed = Main.rand.NextFloat(0.15f, 0.25f);
            maxPulseScale = scale * Main.rand.NextFloat(2f, 3f);

            coreColor = new Color(130, 220, 140);
            haloColor = new Color(180, 255, 190);
            burstPhase = 0;
        }
        public PRT_SulfuricCore Configure(int lt) {
            Lifetime = lt;
            pulseSpeed = Main.rand.NextFloat(0.15f, 0.25f);
            maxPulseScale = Scale * Main.rand.NextFloat(2f, 3f);
            coreColor = new Color(130, 220, 140);
            haloColor = new Color(180, 255, 190);
            burstPhase = 0;
            return this;
        }
        public override void Reset() {
            base.Reset();
            pulseSpeed = 0.2f;
            maxPulseScale = 0f;
            coreColor = new Color(130, 220, 140);
            haloColor = new Color(180, 255, 190);
            burstPhase = 0;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            Opacity = 0f;
        }

        public override void AI() {
            float progress = LifetimeCompletion;

            if (progress < 0.2f) {
                //胀
                burstPhase = 0;
                float expandProgress = progress / 0.2f;
                Scale = MathHelper.Lerp(Scale * 0.3f, maxPulseScale, VaultUtils.EaseOutCubic(expandProgress));
                Opacity = expandProgress;
            }
            else if (progress < 0.4f) {
                //闪
                burstPhase = 1;
                float flashProgress = (progress - 0.2f) / 0.2f;
                float flashIntensity = MathF.Sin(flashProgress * MathHelper.Pi);
                Opacity = 1f + flashIntensity * 0.5f; //过曝
                Scale = maxPulseScale * (1f + flashIntensity * 0.2f);
            }
            else {
                //收+脉
                burstPhase = 2;
                float fadeProgress = (progress - 0.4f) / 0.6f;
                Opacity = (float)Math.Sin((1f - fadeProgress) * MathHelper.PiOver2);

                float pulse = MathF.Sin(Time * pulseSpeed) * 0.1f + 1f;
                Scale = MathHelper.Lerp(maxPulseScale, maxPulseScale * 0.5f, fadeProgress) * pulse;
            }

            float hueShift = MathF.Sin(Time * 0.05f) * 0.02f;
            coreColor = Main.hslToRgb(
                (Main.rgbToHsl(coreColor).X + hueShift) % 1,
                Main.rgbToHsl(coreColor).Y,
                Main.rgbToHsl(coreColor).Z
            );

            Rotation += 0.02f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D texture = PRTLoader.PRT_IDToTexture[ID];
            Vector2 drawPos = Position - Main.screenPosition;
            Vector2 origin = texture.Size() / 2f;

            switch (burstPhase) {
                case 0:
                    DrawExpandingCore(spriteBatch, texture, drawPos, origin);
                    break;

                case 1:
                    DrawFlashCore(spriteBatch, texture, drawPos, origin);
                    break;

                case 2:
                    DrawPulsingCore(spriteBatch, texture, drawPos, origin);
                    break;
            }

            return false;
        }

        private void DrawExpandingCore(SpriteBatch sb, Texture2D tex, Vector2 pos, Vector2 origin) {
            for (int i = 0; i < 3; i++) {
                float ringScale = Scale * (1.5f + i * 0.3f);
                float ringAlpha = Opacity * (1f - i * 0.3f);
                sb.Draw(tex, pos, null, haloColor * ringAlpha * 0.4f, Rotation + i * 0.3f,
                    origin, ringScale, SpriteEffects.None, 0f);
            }

            sb.Draw(tex, pos, null, coreColor * Opacity, Rotation, origin, Scale, SpriteEffects.None, 0f);
            sb.Draw(tex, pos, null, Color.White * Opacity * 0.7f, Rotation, origin, Scale * 0.6f, SpriteEffects.None, 0f);
        }

        private void DrawFlashCore(SpriteBatch sb, Texture2D tex, Vector2 pos, Vector2 origin) {
            for (int i = 0; i < 5; i++) {
                float ringScale = Scale * (1.2f + i * 0.4f);
                float ringAlpha = Opacity * (1f - i * 0.2f);
                sb.Draw(tex, pos, null, Color.White * ringAlpha * 0.5f, Rotation + i * 0.5f,
                    origin, ringScale, SpriteEffects.None, 0f);
            }

            sb.Draw(tex, pos, null, Color.White * Opacity, Rotation, origin, Scale * 0.8f, SpriteEffects.None, 0f);
            sb.Draw(tex, pos, null, coreColor * Opacity * 0.8f, -Rotation, origin, Scale, SpriteEffects.None, 0f);
        }

        private void DrawPulsingCore(SpriteBatch sb, Texture2D tex, Vector2 pos, Vector2 origin) {
            float pulse = MathF.Sin(Time * pulseSpeed * 2f) * 0.3f + 0.7f;

            for (int i = 0; i < 4; i++) {
                float ringScale = Scale * (1f + i * 0.25f * pulse);
                float ringAlpha = Opacity * (1f - i * 0.25f);
                sb.Draw(tex, pos, null, haloColor * ringAlpha * 0.3f, Rotation + i * 0.2f,
                    origin, ringScale, SpriteEffects.None, 0f);
            }

            sb.Draw(tex, pos, null, coreColor * Opacity * pulse, Rotation, origin, Scale * 0.9f, SpriteEffects.None, 0f);
            sb.Draw(tex, pos, null, haloColor * Opacity * 0.6f, -Rotation * 0.7f, origin, Scale * 0.7f, SpriteEffects.None, 0f);
        }
    }
}
