using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Narrative;
using CalamityOverhaul.Content.Narrative.Presentation;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Scenarios.Shepel
{
    internal class ShepelFullBodyPortrait : FullBodyPortraitBase
    {
        public override string PortraitKey => "ShepelFullBody";

        protected override float FadeInDuration => 20f;

        internal Face currentFace = Face.None;
        internal enum Face
        {
            None,
            Blank,
            Happy,
            Pain,
            Sad,
            Serious,
            Shocked,
            Sleep,
            Smirk,
        }

        private float glitchTimer;       //剩余帧数
        private float glitchIntensity;   //0~1
        private float glitchTimeAccum;   //着色器时间

        /// <param name="durationSeconds">秒</param>
        /// <param name="intensity">0~1</param>
        public void TriggerGlitch(float durationSeconds, float intensity) {
            glitchTimer = durationSeconds * 60f;
            glitchIntensity = Math.Clamp(intensity, 0f, 1f);
            glitchTimeAccum = 0f;
        }

        public void StopGlitch() {
            glitchTimer = 0f;
            glitchIntensity = 0f;
        }

        public bool IsGlitching => glitchTimer > 0f;

        protected override void OnInitialize() {
            scale = 1.2f;
            glitchTimer = 0f;
            glitchIntensity = 0f;
            glitchTimeAccum = 0f;
        }

        protected override void OnUpdate() {
            scale = 1.2f;
            drawColor = Color.White;

            if (glitchTimer > 0f) {
                glitchTimer--;
                glitchTimeAccum += 0.016f;
                if (glitchTimer <= 0f) {
                    glitchTimer = 0f;
                    glitchIntensity = 0f;
                }
            }
        }

        protected override void OnDraw(SpriteBatch spriteBatch, float alpha) {
            Texture2D portrait = ADVAsset.Shepel;
            if (portrait == null || portrait.IsDisposed) {
                return;
            }

            Rectangle rectangle = new Rectangle(0, 0, portrait.Width, portrait.Height);
            position = OwnerDialogue.GetPanelRect().Top() + new Vector2(-160, -portrait.Height + 100) * scale;

            Color color = drawColor * alpha;
            bool useGlitch = glitchTimer > 0f && glitchIntensity > 0f && EffectLoader.ShepelGlitch?.Value != null;

            if (useGlitch) {
                Effect effect = EffectLoader.ShepelGlitch.Value;
                effect.Parameters["uTime"]?.SetValue(glitchTimeAccum);
                effect.Parameters["uIntensity"]?.SetValue(glitchIntensity);

                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                    SamplerState.AnisotropicClamp, DepthStencilState.None,
                    RasterizerState.CullNone, effect, Main.UIScaleMatrix);
            }

            spriteBatch.Draw(portrait, position, rectangle, color, rotation, Vector2.Zero, scale, SpriteEffects.None, 0f);
            position.X += 18 * scale;

            Texture2D faceTexture = currentFace switch {
                Face.Blank => ADVAsset.Shepel_Blank,
                Face.Happy => ADVAsset.Shepel_Happy,
                Face.Pain => ADVAsset.Shepel_Pain,
                Face.Sad => ADVAsset.Shepel_Sad,
                Face.Serious => ADVAsset.Shepel_Serious,
                Face.Shocked => ADVAsset.Shepel_Shocked,
                Face.Sleep => ADVAsset.Shepel_Sleep,
                Face.Smirk => ADVAsset.Shepel_Smirk,
                _ => null
            };

            if (faceTexture != null) {
                spriteBatch.Draw(faceTexture, position, null, color, rotation, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }

            if (useGlitch) {
                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                    SamplerState.AnisotropicClamp, DepthStencilState.None,
                    RasterizerState.CullNone, null, Main.UIScaleMatrix);
            }
        }
    }
}
