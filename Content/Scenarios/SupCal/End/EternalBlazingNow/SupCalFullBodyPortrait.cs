using CalamityOverhaul.Content.Narrative;
using CalamityOverhaul.Content.Narrative.Presentation;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.Scenarios.SupCal.End.EternalBlazingNow
{
    /// <summary>至尊灾厄女巫全身立绘，告别场景燃烧消失演出</summary>
    internal sealed class SupCalFullBodyPortrait : FullBodyPortraitBase
    {
        public override string PortraitKey => "SupremeCalamitasFullBody";

        protected override float FadeInDuration => 120f;

        private const int SmilePortraitDialogueIndex = 10;
        private const float BurnDuration = 180f;
        private const float PortraitTransitionDuration = 30f;

        private float burnProgress;
        private float burnHeight;
        private float fireAnimationTimer;

        private bool useSmilePortrait;
        private float portraitTransitionProgress;

        private sealed class FireParticle
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Life;
            public float MaxLife;
            public float Scale;
            public float Rotation;
            public float RotationSpeed;
            public float Alpha;
            public int FrameIndex;
        }

        private readonly List<FireParticle> fireParticles = new();
        private int particleSpawnTimer;

        private Color currentTint = Color.White;

        protected override void OnInitialize() {
            burnProgress = 0f;
            burnHeight = 0f;
            fireAnimationTimer = 0f;
            currentTint = Color.White;
            fireParticles.Clear();
            particleSpawnTimer = 0;
            useSmilePortrait = false;
            portraitTransitionProgress = 0f;
        }

        public override void EndPerformance() {
            if (currentPhase != PerformancePhase.Custom) {
                StartBurningDissolve();
            }
        }

        protected override void OnDeactivate() {
            fireParticles.Clear();
            BlockDialogueAdvance = false;
            BlockDialogueClose = false;
        }

        public void StartBurning() {
            useSmilePortrait = true;
            portraitTransitionProgress = 0f;
            StartBurningDissolve();
        }

        public void StartBurningDissolve() {
            EnterCustomPhase();
            burnProgress = 0f;
            burnHeight = 0f;
            fireParticles.Clear();
            BlockDialogueAdvance = true;
            BlockDialogueClose = true;
        }

        protected override void OnCustomPhaseUpdate() {
            UpdateBurningDissolve();
        }

        private void UpdateBurningDissolve() {
            burnProgress++;

            if (burnProgress >= BurnDuration) {
                ForceDeactivate();
                return;
            }

            float t = burnProgress / BurnDuration;

            burnHeight = VaultUtils.EaseInOutQuad(t);

            particleSpawnTimer++;
            if (particleSpawnTimer >= 2 && burnHeight > 0.01f) {
                particleSpawnTimer = 0;
                SpawnFireParticles();
            }

            UpdateFireParticles();

            CurrentFade = 1f - VaultUtils.EaseInCubic(t * 0.8f);
        }

        private void UpdateFireParticles() {
            for (int i = fireParticles.Count - 1; i >= 0; i--) {
                FireParticle particle = fireParticles[i];
                particle.Life++;

                if (particle.Life >= particle.MaxLife) {
                    fireParticles.RemoveAt(i);
                    continue;
                }

                particle.Position += particle.Velocity;
                particle.Velocity.Y -= 0.08f;
                particle.Velocity.X *= 0.98f;
                particle.Rotation += particle.RotationSpeed;

                float lifeRatio = particle.Life / particle.MaxLife;
                particle.Alpha = 1f - VaultUtils.EaseInQuad(lifeRatio);
            }
        }

        private void SpawnFireParticles() {
            Texture2D currentPortrait = GetCurrentPortrait();
            if (currentPortrait == null) {
                return;
            }

            Vector2 portraitSize = currentPortrait.Size() * scale;
            float edgeY = position.Y + portraitSize.Y * (1f - burnHeight);

            int particleCount = (int)(8f + Math.Sin(timer * 0.1f) * 3f);
            for (int i = 0; i < particleCount; i++) {
                float xOffset = Main.rand.NextFloat(-10f, portraitSize.X + 10f);
                float yOffset = Main.rand.NextFloat(-20f, 20f);

                fireParticles.Add(new FireParticle {
                    Position = new Vector2(position.X + xOffset, edgeY + yOffset),
                    Velocity = new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), Main.rand.NextFloat(-2.5f, -0.8f)),
                    Life = 0f,
                    MaxLife = Main.rand.NextFloat(35f, 70f),
                    Scale = Main.rand.NextFloat(0.6f, 1.2f),
                    Rotation = Main.rand.NextFloat(MathHelper.TwoPi),
                    RotationSpeed = Main.rand.NextFloat(-0.08f, 0.08f),
                    Alpha = 1f,
                    FrameIndex = Main.rand.Next(16)
                });
            }
        }

        protected override void OnUpdate() {
            fireAnimationTimer += 0.12f;
            if (fireAnimationTimer > 16f) {
                fireAnimationTimer -= 16f;
            }

            if (useSmilePortrait && portraitTransitionProgress < 1f) {
                portraitTransitionProgress += 1f / PortraitTransitionDuration;
                if (portraitTransitionProgress > 1f) {
                    portraitTransitionProgress = 1f;
                }
            }

            scale = 1.4f;
            currentTint = Color.White;
        }

        private Texture2D GetCurrentPortrait() {
            if (useSmilePortrait) {
                return ADVAsset.SupCalADV;
            }

            return ADVAsset.SupCal_closeEyesADV;
        }

        protected override void OnDraw(SpriteBatch spriteBatch, float alpha) {
            Texture2D currentPortrait = GetCurrentPortrait();
            if (currentPortrait == null || OwnerDialogue == null) {
                return;
            }

            position = OwnerDialogue.GetPanelRect().Top() + new Vector2(-currentPortrait.Width + 60, -currentPortrait.Height + 80) * scale;
            Vector2 portraitSize = currentPortrait.Size() * scale;

            if (useSmilePortrait && portraitTransitionProgress < 1f && ADVAsset.SupCal_closeEyesADV != null) {
                DrawPortraitTransition(spriteBatch, alpha);
                return;
            }

            if (currentPhase == PerformancePhase.Custom) {
                DrawBurningPortrait(spriteBatch, position, portraitSize, alpha);
            }
            else {
                Color drawColor = currentTint * alpha;
                spriteBatch.Draw(currentPortrait, position, null, drawColor, rotation, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }
        }

        private void DrawPortraitTransition(SpriteBatch spriteBatch, float alpha) {
            float closeEyesAlpha = alpha * (1f - portraitTransitionProgress);
            Color closeEyesColor = currentTint * closeEyesAlpha;
            spriteBatch.Draw(ADVAsset.SupCal_closeEyesADV, position, null, closeEyesColor, rotation, Vector2.Zero, scale, SpriteEffects.None, 0f);

            float smileAlpha = alpha * portraitTransitionProgress;
            Color smileColor = currentTint * smileAlpha;
            if (ADVAsset.SupCalADV != null) {
                spriteBatch.Draw(ADVAsset.SupCalADV, position, null, smileColor, rotation, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }
        }

        private void DrawBurningPortrait(SpriteBatch spriteBatch, Vector2 pos, Vector2 size, float alpha) {
            Texture2D portrait = GetCurrentPortrait();
            Texture2D fireMask = CWRAsset.Fire?.Value;

            if (fireMask == null) {
                spriteBatch.Draw(portrait, pos, null, currentTint * alpha, rotation, Vector2.Zero, scale, SpriteEffects.None, 0f);
                return;
            }

            int frameWidth = fireMask.Width / 4;
            int frameHeight = fireMask.Height / 4;

            DrawUnburntPortion(spriteBatch, portrait, pos, alpha);
            DrawBurningEdge(spriteBatch, portrait, fireMask, pos, size, alpha, frameWidth, frameHeight);
            DrawFireParticles(spriteBatch, fireMask, alpha, frameWidth, frameHeight);
            DrawAshParticles(spriteBatch, pos, size, alpha);
        }

        private void DrawUnburntPortion(SpriteBatch spriteBatch, Texture2D portrait, Vector2 pos, float alpha) {
            if (burnHeight >= 1f) {
                return;
            }

            int unburntSourceHeight = (int)(portrait.Height * (1f - burnHeight));
            if (unburntSourceHeight <= 0) {
                return;
            }

            Rectangle unburntSource = new(0, 0, portrait.Width, unburntSourceHeight);
            Color unburntColor = currentTint * alpha;
            spriteBatch.Draw(portrait, pos, unburntSource, unburntColor, rotation, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        private void DrawBurningEdge(SpriteBatch spriteBatch, Texture2D portrait, Texture2D fireMask, Vector2 pos, Vector2 size, float alpha, int frameWidth, int frameHeight) {
            if (burnHeight <= 0f) {
                return;
            }

            float edgeThickness = 80f / portrait.Height;
            float edgeStart = Math.Max(0f, burnHeight - edgeThickness);
            float edgeEnd = burnHeight;

            if (edgeStart >= edgeEnd) {
                return;
            }

            int edgeSourceHeight = (int)(portrait.Height * (edgeEnd - edgeStart));
            if (edgeSourceHeight <= 0) {
                return;
            }

            Vector2 edgePos = pos + new Vector2(0f, edgeStart * size.Y);
            int fireCount = Math.Max(1, (int)(size.X / (frameWidth * scale * 0.5f)));

            for (int i = 0; i < fireCount; i++) {
                float xPos = i * (size.X / fireCount);
                float waveOffset = (float)Math.Sin(fireAnimationTimer * 0.5f + i * 0.8f) * 10f;

                int frameOffset = (int)(fireAnimationTimer + i * 2) % 16;
                int frameX = frameOffset % 4 * frameWidth;
                int frameY = frameOffset / 4 * frameHeight;
                Rectangle fireFrame = new(frameX, frameY, frameWidth, frameHeight);

                Vector2 firePos = edgePos + new Vector2(xPos, waveOffset);
                float fireScale = scale * 0.8f;

                float gradientT = (float)Math.Sin(fireAnimationTimer * 0.3f + i * 0.5f) * 0.5f + 0.5f;
                Color fireColor1 = new(255, 240, 100);
                Color fireColor2 = new(255, 150, 50);
                Color fireColor3 = new(255, 80, 50);
                Color fireColor = Color.Lerp(Color.Lerp(fireColor1, fireColor2, gradientT), fireColor3, burnHeight * 0.5f);
                fireColor.A = 0;
                fireColor *= alpha * 0.9f;

                spriteBatch.Draw(fireMask, firePos, fireFrame, fireColor, rotation, Vector2.Zero, fireScale, SpriteEffects.None, 0f);
            }
        }

        private void DrawFireParticles(SpriteBatch spriteBatch, Texture2D fireMask, float alpha, int frameWidth, int frameHeight) {
            foreach (FireParticle particle in fireParticles) {
                int frameX = particle.FrameIndex % 4 * frameWidth;
                int frameY = particle.FrameIndex / 4 * frameHeight;
                Rectangle particleFrame = new(frameX, frameY, frameWidth, frameHeight);

                Color particleColor;
                float lifeRatio = particle.Life / particle.MaxLife;

                if (lifeRatio < 0.3f) {
                    particleColor = Color.Lerp(new Color(255, 240, 120), new Color(255, 180, 80), lifeRatio / 0.3f);
                }
                else if (lifeRatio < 0.6f) {
                    particleColor = Color.Lerp(new Color(255, 180, 80), new Color(255, 100, 60), (lifeRatio - 0.3f) / 0.3f);
                }
                else {
                    particleColor = Color.Lerp(new Color(255, 100, 60), new Color(100, 100, 100), (lifeRatio - 0.6f) / 0.4f);
                }

                particleColor.A = 0;
                particleColor *= alpha * particle.Alpha * 0.7f;

                float particleScale = particle.Scale * scale * 0.4f;

                spriteBatch.Draw(
                    fireMask,
                    particle.Position,
                    particleFrame,
                    particleColor,
                    particle.Rotation,
                    new Vector2(frameWidth / 2f, frameHeight / 2f),
                    particleScale,
                    SpriteEffects.None,
                    0f);
            }
        }

        private void DrawAshParticles(SpriteBatch spriteBatch, Vector2 pos, Vector2 size, float alpha) {
            if (burnHeight <= 0.1f) {
                return;
            }

            int ashCount = (int)(burnHeight * 15f);
            Texture2D pixel = VaultAsset.placeholder2.Value;

            for (int i = 0; i < ashCount; i++) {
                float ashTimer = (timer + i * 5f) * 0.025f;
                float ashX = pos.X + (float)Math.Sin(ashTimer * 2f + i) * size.X * 0.2f + size.X * Main.rand.NextFloat(0.4f, 0.6f);
                float ashY = pos.Y + size.Y * (1f - burnHeight) + ashTimer % 1f * size.Y * 0.4f;

                float ashAlpha = (1f - ashTimer % 1f) * 0.6f;
                Color ashColor = new Color(80, 80, 80) * (alpha * ashAlpha);

                Vector2 ashPos = new(ashX, ashY);
                float ashSize = Main.rand.NextFloat(1.5f, 2.5f);

                spriteBatch.Draw(pixel, ashPos, null, ashColor, 0f, new Vector2(0.5f, 0.5f), ashSize, SpriteEffects.None, 0f);
            }
        }
    }
}
