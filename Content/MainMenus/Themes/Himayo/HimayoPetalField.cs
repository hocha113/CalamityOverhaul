using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.MainMenus.Themes.Himayo
{
    internal sealed class HimayoPetalField
    {
        internal const float FixedStep = 1f / 60f;

        private const float ReferenceArea = 1920f * 1080f;
        private const float SweepRadius = 64f;
        private const float CaptureRadius = 30f;
        private const float MaxNearSpeed = 18f;
        private const float MaxReleaseSpeed = 30f;

        private readonly List<Petal> farPetals = [];
        private readonly List<Petal> middlePetals = [];
        private readonly List<Petal> nearPetals = [];

        private Petal capturedPetal;
        private Vector2 previousPointer;
        private Vector2 pointerVelocity;
        private bool pointerTrailValid;
        private bool previousMouseLeft;
        private bool captureGestureEligible;
        private int renderWidth;
        private int renderHeight;
        private float time;

        internal void Initialize(Vector2 pointer) {
            farPetals.Clear();
            middlePetals.Clear();
            nearPetals.Clear();
            capturedPetal = null;
            previousPointer = pointer;
            pointerVelocity = Vector2.Zero;
            pointerTrailValid = true;
            previousMouseLeft = Main.mouseLeft;
            captureGestureEligible = false;
            renderWidth = 0;
            renderHeight = 0;
            time = Main.rand.NextFloat(MathHelper.TwoPi);
            EnsureParticleCounts();
        }

        internal void Update(Vector2 pointer, bool titlePage, bool allowNearInteraction)
            => Update(pointer, Main.mouseLeft, titlePage, allowNearInteraction);

        internal void Update(Vector2 pointer, bool mouseLeft, bool titlePage, bool allowNearInteraction) {
            bool screenChanged = EnsureParticleCounts();
            time += FixedStep;

            if (screenChanged) {
                ReleaseCapture();
                pointerTrailValid = false;
            }
            if (!pointerTrailValid) {
                previousPointer = pointer;
                pointerTrailValid = true;
            }

            pointerVelocity = ClampLength(pointer - previousPointer, 52f);
            SnapshotLayer(farPetals);
            SnapshotLayer(middlePetals);
            if (titlePage) {
                SnapshotLayer(nearPetals);
            }

            if (!titlePage || !allowNearInteraction) {
                ReleaseCapture();
            }
            else {
                if (mouseLeft && !previousMouseLeft) {
                    captureGestureEligible = true;
                }
                if (!mouseLeft) {
                    if (capturedPetal != null) {
                        DropCapturedPetal(true);
                    }
                    captureGestureEligible = false;
                }

                ApplyPointerSweep(previousPointer, pointer);
                if (mouseLeft && captureGestureEligible && capturedPetal == null) {
                    TryCapture(pointer);
                }
            }

            UpdateLayer(farPetals, PetalDepth.Far, titlePage ? 1f : 0.34f, pointer);
            UpdateLayer(middlePetals, PetalDepth.Middle, titlePage ? 1f : 0.42f, pointer);
            if (titlePage) {
                UpdateLayer(nearPetals, PetalDepth.Near, 1f, pointer);
            }

            previousPointer = pointer;
            previousMouseLeft = mouseLeft;
        }

        internal void ReleaseCapture() {
            DropCapturedPetal(false);
            captureGestureEligible = false;
        }

        internal void ResetMouseTrail() {
            previousPointer = HimayoMenuInput.PhysicalPointer;
            pointerVelocity = Vector2.Zero;
            pointerTrailValid = false;
            previousMouseLeft = Main.mouseLeft;
            captureGestureEligible = false;
        }

        internal void DrawFarAndMiddle(SpriteBatch spriteBatch, Effect effect,
            bool titlePage, float interpolation) {
            float childAlpha = titlePage ? 1f : 0.42f;
            DrawLayer(spriteBatch, effect, farPetals, PetalDepth.Far, 0.18f,
                0.26f * childAlpha, interpolation);
            DrawLayer(spriteBatch, effect, middlePetals, PetalDepth.Middle, 0.10f,
                0.52f * childAlpha, interpolation);
        }

        internal void DrawNear(SpriteBatch spriteBatch, Effect effect,
            bool titlePage, float interpolation) {
            if (!titlePage) {
                return;
            }
            DrawLayer(spriteBatch, effect, nearPetals, PetalDepth.Near, 0.055f,
                0.88f, interpolation);
        }

        private bool EnsureParticleCounts() {
            int nextWidth = HimayoMenuInput.PhysicalScreenWidth;
            int nextHeight = HimayoMenuInput.PhysicalScreenHeight;
            bool screenChanged = renderWidth > 0 && renderHeight > 0
                && (renderWidth != nextWidth || renderHeight != nextHeight);
            if (screenChanged) {
                float scaleX = nextWidth / (float)renderWidth;
                float scaleY = nextHeight / (float)renderHeight;
                ScaleLayer(farPetals, scaleX, scaleY);
                ScaleLayer(middlePetals, scaleX, scaleY);
                ScaleLayer(nearPetals, scaleX, scaleY);
            }
            renderWidth = nextWidth;
            renderHeight = nextHeight;

            float areaScale = MathHelper.Clamp(
                renderWidth * renderHeight / ReferenceArea, 0.65f, 1.6f);
            ResizeLayer(farPetals, (int)MathF.Round(96f * areaScale), PetalDepth.Far);
            ResizeLayer(middlePetals, (int)MathF.Round(56f * areaScale), PetalDepth.Middle);
            ResizeLayer(nearPetals, (int)MathF.Round(24f * areaScale), PetalDepth.Near);
            return screenChanged;
        }

        private void ResizeLayer(List<Petal> petals, int targetCount, PetalDepth depth) {
            while (petals.Count < targetCount) {
                Petal petal = new();
                Respawn(petal, depth, true);
                petals.Add(petal);
            }
            while (petals.Count > targetCount) {
                Petal removed = petals[^1];
                if (removed == capturedPetal) {
                    DropCapturedPetal(false);
                }
                petals.RemoveAt(petals.Count - 1);
            }
        }

        private static void ScaleLayer(List<Petal> petals, float scaleX, float scaleY) {
            Vector2 scale = new(scaleX, scaleY);
            for (int i = 0; i < petals.Count; i++) {
                petals[i].PreviousPosition *= scale;
                petals[i].Position *= scale;
            }
        }

        private static void SnapshotLayer(List<Petal> petals) {
            for (int i = 0; i < petals.Count; i++) {
                Petal petal = petals[i];
                petal.PreviousPosition = petal.Position;
                petal.PreviousVelocity = petal.Velocity;
                petal.PreviousRotation = petal.Rotation;
                petal.PreviousFlipPhase = petal.FlipPhase;
            }
        }

        private void UpdateLayer(List<Petal> petals, PetalDepth depth,
            float speedMultiplier, Vector2 pointer) {
            for (int i = 0; i < petals.Count; i++) {
                Petal petal = petals[i];
                if (petal.Captured) {
                    Vector2 toPointer = pointer - petal.Position;
                    petal.Velocity += toPointer * 0.17f - petal.Velocity * 0.22f;
                    petal.Velocity = ClampLength(petal.Velocity, 22f);
                    petal.AngularVelocity = MathHelper.Clamp(petal.AngularVelocity
                        + Cross(toPointer, petal.Velocity) * 0.00014f, -0.48f, 0.48f);
                }
                else {
                    float windStrength = depth switch {
                        PetalDepth.Far => 0.10f,
                        PetalDepth.Middle => 0.22f,
                        _ => 0.34f
                    };
                    float response = depth == PetalDepth.Near ? 0.018f : 0.009f;
                    float wind = MathF.Sin(time * petal.WindFrequency + petal.Phase) * windStrength;
                    petal.Velocity.X = MathHelper.Lerp(petal.Velocity.X,
                        petal.BaseDrift + wind, response * speedMultiplier);
                    petal.Velocity.Y = MathHelper.Lerp(petal.Velocity.Y,
                        petal.FallSpeed, response * 0.7f * speedMultiplier);
                    petal.AngularVelocity = MathHelper.Lerp(petal.AngularVelocity,
                        petal.BaseAngularVelocity, response * 0.45f * speedMultiplier);
                }

                petal.Position += petal.Velocity * speedMultiplier;
                petal.Rotation += petal.AngularVelocity * speedMultiplier;
                petal.FlipPhase += petal.FlipSpeed * speedMultiplier;

                float margin = 110f + petal.Size * 2f;
                if (!petal.Captured && (petal.Position.Y > renderHeight + margin
                    || petal.Position.X < -margin * 2f
                    || petal.Position.X > renderWidth + margin * 2f)) {
                    Respawn(petal, depth, false);
                }
            }
        }

        private void ApplyPointerSweep(Vector2 from, Vector2 to) {
            Vector2 segment = to - from;
            float speed = segment.Length();
            if (speed < 0.2f) {
                return;
            }

            Vector2 direction = segment / speed;
            for (int i = 0; i < nearPetals.Count; i++) {
                Petal petal = nearPetals[i];
                if (petal == capturedPetal) {
                    continue;
                }

                Vector2 closest = ClosestPointOnSegment(petal.Position, from, to);
                Vector2 separation = petal.Position - closest;
                float distance = separation.Length();
                if (distance >= SweepRadius) {
                    continue;
                }

                float strength = 1f - distance / SweepRadius;
                Vector2 normal = SafeNormalize(separation,
                    SafeNormalize(new Vector2(-segment.Y, segment.X), Vector2.UnitX));
                Vector2 impulse = pointerVelocity * (0.075f * strength)
                    + normal * ((1.1f + Math.Min(speed, 40f) * 0.10f) * strength);
                petal.Velocity = ClampLength(petal.Velocity + impulse, MaxNearSpeed);
                float spin = Cross(direction, normal) * Math.Min(0.24f, speed * 0.006f) * strength;
                petal.AngularVelocity = MathHelper.Clamp(
                    petal.AngularVelocity + spin, -0.42f, 0.42f);
            }
        }

        private void TryCapture(Vector2 pointer) {
            float nearestDistanceSquared = CaptureRadius * CaptureRadius;
            Petal nearest = null;
            for (int i = 0; i < nearPetals.Count; i++) {
                Petal candidate = nearPetals[i];
                float distanceSquared = Vector2.DistanceSquared(candidate.Position, pointer);
                if (distanceSquared >= nearestDistanceSquared) {
                    continue;
                }
                nearest = candidate;
                nearestDistanceSquared = distanceSquared;
            }

            if (nearest != null) {
                capturedPetal = nearest;
                capturedPetal.Captured = true;
            }
        }

        private void DropCapturedPetal(bool inheritPointerVelocity) {
            if (capturedPetal == null) {
                return;
            }

            Petal released = capturedPetal;
            released.Captured = false;
            if (inheritPointerVelocity) {
                released.Velocity = ClampLength(released.Velocity
                    + ClampLength(pointerVelocity, MaxReleaseSpeed) * 0.72f, MaxReleaseSpeed);
                released.AngularVelocity = MathHelper.Clamp(released.AngularVelocity
                    + pointerVelocity.X * 0.006f, -0.52f, 0.52f);
            }
            capturedPetal = null;
        }

        private static void DrawLayer(SpriteBatch spriteBatch, Effect effect, List<Petal> petals,
            PetalDepth depth, float softness, float layerAlpha, float interpolation) {
            if (effect == null || VaultAsset.placeholder2?.Value is not Texture2D pixel
                || pixel.IsDisposed) {
                return;
            }

            interpolation = MathHelper.Clamp(interpolation, 0f, 1f);
            effect.Parameters["uPetalSoftness"]?.SetValue(softness);
            effect.CurrentTechnique.Passes[0].Apply();
            Vector2 origin = pixel.Size() * 0.5f;

            for (int i = 0; i < petals.Count; i++) {
                Petal petal = petals[i];
                Vector2 position = Vector2.Lerp(petal.PreviousPosition, petal.Position, interpolation);
                Vector2 velocity = Vector2.Lerp(petal.PreviousVelocity, petal.Velocity, interpolation);
                float rotation = petal.PreviousRotation
                    + MathHelper.WrapAngle(petal.Rotation - petal.PreviousRotation) * interpolation;
                float flipPhase = petal.PreviousFlipPhase
                    + MathHelper.WrapAngle(petal.FlipPhase - petal.PreviousFlipPhase) * interpolation;
                float facing = MathF.Cos(flipPhase);
                float flip = 0.16f + MathF.Abs(facing) * 0.84f;
                float stretchStrength = depth switch {
                    PetalDepth.Far => 0.018f,
                    PetalDepth.Middle => 0.035f,
                    _ => 0.065f
                };
                float stretchLimit = depth switch {
                    PetalDepth.Far => 0.14f,
                    PetalDepth.Middle => 0.32f,
                    _ => 0.72f
                };
                float speedStretch = Math.Min(velocity.Length() * stretchStrength, stretchLimit);
                float width = petal.Size * 0.62f * flip;
                float height = petal.Size * (1f + speedStretch);
                Vector2 scale = new(width / pixel.Width, height / pixel.Height);

                Color back = Color.Lerp(new Color(190, 92, 124), new Color(235, 154, 181), petal.Tint);
                Color face = Color.Lerp(new Color(255, 207, 222), new Color(255, 246, 244), petal.Tint);
                Color color = Color.Lerp(back, face, facing * 0.5f + 0.5f);
                float flipLight = 0.70f + flip * 0.30f;
                float opacity = MathHelper.Clamp(layerAlpha * petal.Alpha * flipLight, 0f, 1f);
                color.A = (byte)(opacity * byte.MaxValue);

                spriteBatch.Draw(pixel, position, null, color, rotation, origin,
                    scale, SpriteEffects.None, 0f);
            }
        }

        private void Respawn(Petal petal, PetalDepth depth, bool randomStart) {
            float sizeMin;
            float sizeMax;
            float fallMin;
            float fallMax;
            float drift;
            float alphaMin;
            switch (depth) {
                case PetalDepth.Far:
                    sizeMin = 7f;
                    sizeMax = 14f;
                    fallMin = 0.28f;
                    fallMax = 0.62f;
                    drift = 0.20f;
                    alphaMin = 0.60f;
                    break;
                case PetalDepth.Middle:
                    sizeMin = 11f;
                    sizeMax = 23f;
                    fallMin = 0.66f;
                    fallMax = 1.42f;
                    drift = 0.42f;
                    alphaMin = 0.72f;
                    break;
                default:
                    sizeMin = 23f;
                    sizeMax = 47f;
                    fallMin = 1.20f;
                    fallMax = 2.65f;
                    drift = 0.78f;
                    alphaMin = 0.82f;
                    break;
            }

            petal.Position = new Vector2(
                Main.rand.NextFloat(-80f, Math.Max(81f, renderWidth + 80f)),
                randomStart
                    ? Main.rand.NextFloat(-100f, Math.Max(-99f, renderHeight + 60f))
                    : Main.rand.NextFloat(-170f, -35f));
            petal.PreviousPosition = petal.Position;
            petal.BaseDrift = Main.rand.NextFloat(-drift, drift);
            petal.FallSpeed = Main.rand.NextFloat(fallMin, fallMax);
            petal.Velocity = new Vector2(petal.BaseDrift, petal.FallSpeed);
            petal.PreviousVelocity = petal.Velocity;
            petal.Size = Main.rand.NextFloat(sizeMin, sizeMax);
            petal.Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            petal.PreviousRotation = petal.Rotation;
            petal.BaseAngularVelocity = Main.rand.NextFloat(-0.055f, 0.055f)
                * (depth == PetalDepth.Near ? 1.8f : 1f);
            petal.AngularVelocity = petal.BaseAngularVelocity;
            petal.FlipPhase = Main.rand.NextFloat(MathHelper.TwoPi);
            petal.PreviousFlipPhase = petal.FlipPhase;
            petal.FlipSpeed = Main.rand.NextFloat(0.025f, 0.085f)
                * (depth == PetalDepth.Near ? 1.3f : 1f);
            petal.WindFrequency = Main.rand.NextFloat(0.65f, 1.55f);
            petal.Phase = Main.rand.NextFloat(MathHelper.TwoPi);
            petal.Tint = Main.rand.NextFloat();
            petal.Alpha = Main.rand.NextFloat(alphaMin, 1f);
            petal.Captured = false;
        }

        private static Vector2 ClosestPointOnSegment(Vector2 point, Vector2 start, Vector2 end) {
            Vector2 segment = end - start;
            float lengthSquared = segment.LengthSquared();
            if (lengthSquared <= 0.0001f) {
                return start;
            }
            float amount = MathHelper.Clamp(Vector2.Dot(point - start, segment) / lengthSquared, 0f, 1f);
            return start + segment * amount;
        }

        private static Vector2 ClampLength(Vector2 value, float maxLength) {
            float lengthSquared = value.LengthSquared();
            if (lengthSquared <= maxLength * maxLength) {
                return value;
            }
            return value * (maxLength / MathF.Sqrt(lengthSquared));
        }

        private static Vector2 SafeNormalize(Vector2 value, Vector2 fallback) {
            float lengthSquared = value.LengthSquared();
            return lengthSquared > 0.0001f ? value / MathF.Sqrt(lengthSquared) : fallback;
        }

        private static float Cross(Vector2 left, Vector2 right)
            => left.X * right.Y - left.Y * right.X;

        private enum PetalDepth
        {
            Far,
            Middle,
            Near
        }

        private sealed class Petal
        {
            internal Vector2 PreviousPosition;
            internal Vector2 Position;
            internal Vector2 PreviousVelocity;
            internal Vector2 Velocity;
            internal float PreviousRotation;
            internal float Rotation;
            internal float BaseAngularVelocity;
            internal float AngularVelocity;
            internal float PreviousFlipPhase;
            internal float FlipPhase;
            internal float FlipSpeed;
            internal float WindFrequency;
            internal float Phase;
            internal float BaseDrift;
            internal float FallSpeed;
            internal float Size;
            internal float Tint;
            internal float Alpha;
            internal bool Captured;
        }
    }
}
