using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.Scenarios.Himayo
{
    /// <summary>
    /// 真夜立绘樱瓣汇聚，SDF花瓣入密度RT模糊作遮罩，运动瓣仍直绘UI
    /// </summary>
    internal sealed class HimayoPortraitAssemblyRenderer : INeedRenderTargetContent
    {
        private const int TotalFrames = 104;
        private const int PetalCount = 180;
        private const int TargetPadding = 36;
        private const float BasePetalSize = 21f;

        private static readonly Color PetalPink = new(255, 211, 224);
        private static readonly Color PetalPinkDeep = new(245, 154, 181);
        private static readonly Color EdgeGlow = new(255, 106, 143);
        private static readonly List<HimayoPortraitAssemblyRenderer> Instances = [];

        private sealed class AssemblyPetal
        {
            public Vector2 Target;
            public Color TargetColor;
            public Color Tint;
            public Vector2 StartPosition;
            public Vector2 ResidualVelocity;
            public float SwayPhase;
            public float SwayCycles;
            public float SwayAmplitude;
            public float Delay;
            public float TravelFrames;
            public float Scale;
            public float StartRotation;
            public float RotationSpeed;
            public float FlipPhase;
            public float FlipSpeed;
            public bool MergeIntoPortrait;
            public bool FrontLayer;
        }

        private readonly struct PetalPose
        {
            public readonly Vector2 Position;
            public readonly float Rotation;
            public readonly float Flip;
            public readonly float Alpha;
            public readonly float Merge;

            public PetalPose(Vector2 position, float rotation, float flip, float alpha, float merge) {
                Position = position;
                Rotation = rotation;
                Flip = flip;
                Alpha = alpha;
                Merge = merge;
            }
        }

        private readonly List<AssemblyPetal> petals = [];

        private RenderTarget2D portraitTarget;
        private RenderTarget2D maskTargetA;
        private RenderTarget2D maskTargetB;

        private int timer;
        private bool active;
        private bool advancedDisabled;
        private bool failureLogged;
        private bool renderRequested;
        private bool renderPrepared;
        private bool registered;
        private Texture2D requestedPortrait;
        private Texture2D requestedFace;
        private Vector2 requestedFaceOffset;

        internal bool Active => active;
        public bool IsReady => renderPrepared;

        private float Progress => MathHelper.Clamp(timer / (float)TotalFrames, 0f, 1f);

        internal HimayoPortraitAssemblyRenderer() {
            Instances.Add(this);
        }

        internal static void UnloadAll() {
            foreach (HimayoPortraitAssemblyRenderer renderer in Instances) {
                renderer.Stop();
            }
            Instances.Clear();
        }

        internal void Start(Texture2D portrait) {
            Stop();
            active = true;
            advancedDisabled = false;
            failureLogged = false;
            timer = 0;
            BuildPetals(portrait);
            if (!Main.dedServ && !registered) {
                Main.ContentThatNeedsRenderTargets.Add(this);
                registered = true;
            }
        }

        /// <returns>本帧刚完成</returns>
        internal bool Update(bool canProgress) {
            if (!active) {
                return false;
            }

            if (canProgress) {
                timer++;
            }

            if (timer < TotalFrames) {
                return false;
            }

            Stop();
            return true;
        }

        internal void Stop() {
            active = false;
            timer = 0;
            petals.Clear();
            renderRequested = false;
            renderPrepared = false;
            requestedPortrait = null;
            requestedFace = null;
            if (registered) {
                Main.ContentThatNeedsRenderTargets.Remove(this);
                registered = false;
            }
            DisposeTargets();
        }

        public void Reset() {
            renderPrepared = false;
            renderRequested = active;
            DisposeTargets();
        }

        internal bool Draw(SpriteBatch spriteBatch, Texture2D portrait, Texture2D faceTexture,
            Vector2 faceOffset, Vector2 portraitPosition, float portraitScale, float portraitRotation,
            Color drawColor, float alpha) {

            if (!active || portrait == null || portrait.IsDisposed) {
                return false;
            }

            if (petals.Count == 0) {
                BuildPetals(portrait);
            }

            GraphicsDevice graphicsDevice = Main.instance?.GraphicsDevice;
            if (graphicsDevice == null) {
                return false;
            }

            requestedPortrait = portrait;
            requestedFace = faceTexture;
            requestedFaceOffset = faceOffset;
            renderRequested = true;

            bool callerBatchEnded = false;
            try {
                //调用方是活动中的Deferred UI批次，须先End
                spriteBatch.End();
                callerBatchEnded = true;
                DrawPetalLayer(spriteBatch, portraitPosition, portraitScale, portraitRotation, alpha, false);

                if (renderPrepared && TargetsReady()) {
                    DrawComposite(spriteBatch, graphicsDevice, portraitPosition, portraitScale,
                        portraitRotation, drawColor, alpha);
                }
                else {
                    DrawFallbackPortrait(spriteBatch, portrait, faceTexture, faceOffset, portraitPosition,
                        portraitScale, portraitRotation, drawColor, alpha);
                }

                DrawPetalLayer(spriteBatch, portraitPosition, portraitScale, portraitRotation, alpha, true);
            } catch (Exception exception) {
                DisableAdvanced(exception);
            } finally {
                if (callerBatchEnded) {
                    BeginDefaultUiBatch(spriteBatch);
                }
            }

            return true;
        }

        public void PrepareRenderTarget(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch) {
            if (!renderRequested || !active || advancedDisabled) {
                return;
            }
            renderRequested = false;
            renderPrepared = false;

            Texture2D portrait = requestedPortrait;
            Texture2D faceTexture = requestedFace;
            Effect assemblyEffect = EffectLoader.HimayoPortraitAssembly?.Value;
            Effect petalEffect = EffectLoader.OniDomainDeco?.Value;
            Texture2D white = VaultAsset.placeholder2?.Value;
            if (portrait == null || portrait.IsDisposed
                || assemblyEffect == null || petalEffect == null || white == null) {
                return;
            }

            Exception failure = null;
            try {
                EnsureTargets(graphicsDevice, portrait.Width + TargetPadding * 2, portrait.Height + TargetPadding * 2);
                BuildPortraitTarget(spriteBatch, graphicsDevice, portrait, faceTexture, requestedFaceOffset);
                BuildPetalMask(spriteBatch, graphicsDevice, petalEffect, white);
                BlurMask(spriteBatch, graphicsDevice, assemblyEffect);
                renderPrepared = true;
            } catch (Exception exception) {
                failure = exception;
            } finally {
                UnbindAllTextures(graphicsDevice);
                graphicsDevice.SetRenderTarget(null);
            }

            if (failure != null) {
                DisableAdvanced(failure);
            }
        }

        private void EnsureTargets(GraphicsDevice graphicsDevice, int width, int height) {
            if (TargetValid(portraitTarget, width, height)
                && TargetValid(maskTargetA, width, height)
                && TargetValid(maskTargetB, width, height)) {
                return;
            }

            DisposeTargets();
            portraitTarget = NewTarget(graphicsDevice, width, height);
            maskTargetA = NewTarget(graphicsDevice, width, height);
            maskTargetB = NewTarget(graphicsDevice, width, height);
        }

        private static RenderTarget2D NewTarget(GraphicsDevice graphicsDevice, int width, int height)
            => new(graphicsDevice, width, height, false, SurfaceFormat.Color,
                DepthFormat.None, 0, RenderTargetUsage.PreserveContents);

        private static bool TargetValid(RenderTarget2D target, int width, int height)
            => target != null && !target.IsDisposed && target.Width == width && target.Height == height;

        private bool TargetsReady()
            => portraitTarget != null && !portraitTarget.IsDisposed
            && maskTargetA != null && !maskTargetA.IsDisposed
            && maskTargetB != null && !maskTargetB.IsDisposed;

        private void BuildPortraitTarget(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice,
            Texture2D portrait, Texture2D faceTexture, Vector2 faceOffset) {

            UnbindAllTextures(graphicsDevice);
            graphicsDevice.SetRenderTarget(portraitTarget);
            graphicsDevice.Clear(Color.Transparent);

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
            try {
                Vector2 padding = new(TargetPadding);
                spriteBatch.Draw(portrait, padding, Color.White);
                if (faceTexture != null && !faceTexture.IsDisposed) {
                    spriteBatch.Draw(faceTexture, padding + faceOffset, Color.White);
                }
            } finally {
                spriteBatch.End();
            }
        }

        private void BuildPetalMask(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice,
            Effect petalEffect, Texture2D white) {

            UnbindAllTextures(graphicsDevice);
            graphicsDevice.SetRenderTarget(maskTargetA);
            graphicsDevice.Clear(Color.Transparent);

            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
            try {
                petalEffect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                petalEffect.CurrentTechnique = petalEffect.Techniques["TechPetal"];
                petalEffect.CurrentTechnique.Passes[0].Apply();

                Vector2 origin = white.Size() * 0.5f;
                Vector2 padding = new(TargetPadding);
                foreach (AssemblyPetal petal in petals) {
                    if (!petal.MergeIntoPortrait) {
                        continue;
                    }

                    float merge = GetMerge(petal);
                    if (merge <= 0.005f) {
                        continue;
                    }

                    float stampSize = BasePetalSize * petal.Scale * MathHelper.Lerp(0.78f, 1.55f, merge);
                    Vector2 stampScale = new(stampSize / white.Width, stampSize * 1.18f / white.Height);
                    float stampRotation = petal.StartRotation + petal.Target.X * 0.007f + petal.Target.Y * 0.003f;
                    Color maskColor = Color.White;
                    maskColor.A = (byte)(255f * merge);
                    spriteBatch.Draw(white, padding + petal.Target, null, maskColor,
                        stampRotation, origin, stampScale, SpriteEffects.None, 0f);
                }
            } finally {
                spriteBatch.End();
            }
        }

        private void BlurMask(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, Effect assemblyEffect) {
            assemblyEffect.CurrentTechnique = assemblyEffect.Techniques["BlurTech"];
            float texelX = 1f / maskTargetA.Width;
            float texelY = 1f / maskTargetA.Height;

            for (int i = 0; i < 2; i++) {
                float radius = i == 0 ? 1.35f : 2.8f;

                UnbindAllTextures(graphicsDevice);
                graphicsDevice.SetRenderTarget(maskTargetB);
                graphicsDevice.Clear(Color.Transparent);
                assemblyEffect.Parameters["uDelta"]?.SetValue(new Vector2(texelX * radius, 0f));
                spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque,
                    SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
                try {
                    assemblyEffect.CurrentTechnique.Passes[0].Apply();
                    spriteBatch.Draw(maskTargetA, Vector2.Zero, Color.White);
                } finally {
                    spriteBatch.End();
                }

                UnbindAllTextures(graphicsDevice);
                graphicsDevice.SetRenderTarget(maskTargetA);
                graphicsDevice.Clear(Color.Transparent);
                assemblyEffect.Parameters["uDelta"]?.SetValue(new Vector2(0f, texelY * radius));
                spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque,
                    SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
                try {
                    assemblyEffect.CurrentTechnique.Passes[0].Apply();
                    spriteBatch.Draw(maskTargetB, Vector2.Zero, Color.White);
                } finally {
                    spriteBatch.End();
                }
            }
        }

        private void DrawComposite(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice,
            Vector2 portraitPosition, float portraitScale, float portraitRotation, Color drawColor, float alpha) {

            Effect effect = EffectLoader.HimayoPortraitAssembly.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value ?? VaultAsset.placeholder2.Value;

            effect.Parameters["uProgress"]?.SetValue(Progress);
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uTexelSize"]?.SetValue(new Vector2(1f / maskTargetA.Width, 1f / maskTargetA.Height));
            effect.Parameters["uEdgeColor"]?.SetValue(EdgeGlow.ToVector3());

            Texture previousTexture1 = graphicsDevice.Textures[1];
            Texture previousTexture2 = graphicsDevice.Textures[2];
            SamplerState previousSampler1 = graphicsDevice.SamplerStates[1];
            SamplerState previousSampler2 = graphicsDevice.SamplerStates[2];
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.UIScaleMatrix);
            try {
                graphicsDevice.Textures[1] = maskTargetA;
                graphicsDevice.SamplerStates[1] = SamplerState.LinearClamp;
                graphicsDevice.Textures[2] = noise;
                graphicsDevice.SamplerStates[2] = SamplerState.LinearWrap;

                effect.CurrentTechnique = effect.Techniques["CompositeTech"];
                effect.CurrentTechnique.Passes[0].Apply();
                spriteBatch.Draw(portraitTarget, portraitPosition, null, drawColor * alpha,
                    portraitRotation, new Vector2(TargetPadding), portraitScale, SpriteEffects.None, 0f);
            } finally {
                spriteBatch.End();
                graphicsDevice.Textures[1] = previousTexture1;
                graphicsDevice.Textures[2] = previousTexture2;
                graphicsDevice.SamplerStates[1] = previousSampler1;
                graphicsDevice.SamplerStates[2] = previousSampler2;
            }
        }

        private void DrawFallbackPortrait(SpriteBatch spriteBatch, Texture2D portrait, Texture2D faceTexture,
            Vector2 faceOffset, Vector2 portraitPosition, float portraitScale, float portraitRotation,
            Color drawColor, float alpha) {

            float reveal = SmoothStep(0.12f, 0.96f, Progress);
            int revealHeight = Math.Clamp((int)(portrait.Height * reveal), 0, portrait.Height);
            Color color = drawColor * alpha;

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.UIScaleMatrix);
            try {
                if (revealHeight > 0) {
                    Rectangle portraitSource = new(0, 0, portrait.Width, revealHeight);
                    spriteBatch.Draw(portrait, portraitPosition, portraitSource, color, portraitRotation,
                        Vector2.Zero, portraitScale, SpriteEffects.None, 0f);
                }

                if (faceTexture != null && !faceTexture.IsDisposed) {
                    int faceRevealHeight = Math.Clamp(revealHeight - (int)faceOffset.Y, 0, faceTexture.Height);
                    Vector2 facePosition = portraitPosition
                        + faceOffset.RotatedBy(portraitRotation) * portraitScale;
                    if (faceRevealHeight > 0) {
                        Rectangle faceSource = new(0, 0, faceTexture.Width, faceRevealHeight);
                        spriteBatch.Draw(faceTexture, facePosition, faceSource,
                            color, portraitRotation, Vector2.Zero, portraitScale, SpriteEffects.None, 0f);
                    }
                }
            } finally {
                spriteBatch.End();
            }
        }

        private void DrawPetalLayer(SpriteBatch spriteBatch, Vector2 portraitPosition,
            float portraitScale, float portraitRotation, float alpha, bool frontLayer) {

            Texture2D white = VaultAsset.placeholder2?.Value;
            if (white == null) {
                return;
            }

            Effect petalEffect = EffectLoader.OniDomainDeco?.Value;
            bool useSdf = petalEffect != null;
            spriteBatch.Begin(useSdf ? SpriteSortMode.Immediate : SpriteSortMode.Deferred,
                BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None,
                RasterizerState.CullNone, null, Main.UIScaleMatrix);
            try {
                if (useSdf) {
                    petalEffect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                    petalEffect.CurrentTechnique = petalEffect.Techniques["TechPetal"];
                    petalEffect.CurrentTechnique.Passes[0].Apply();
                }

                Vector2 origin = white.Size() * 0.5f;
                foreach (AssemblyPetal petal in petals) {
                    if (petal.FrontLayer != frontLayer || !TryGetPose(petal, out PetalPose pose)) {
                        continue;
                    }

                    Vector2 screenPosition = portraitPosition
                        + pose.Position.RotatedBy(portraitRotation) * portraitScale;
                    float mergeTint = petal.MergeIntoPortrait ? pose.Merge * 0.68f : 0f;
                    Color color = Color.Lerp(petal.Tint, petal.TargetColor, mergeTint);
                    float opacity = MathHelper.Clamp(alpha * pose.Alpha * (frontLayer ? 0.95f : 0.72f), 0f, 1f);
                    if (useSdf) {
                        color.A = (byte)(255f * opacity);
                    }
                    else {
                        color *= opacity;
                    }

                    float size = BasePetalSize * petal.Scale * portraitScale;
                    Vector2 petalScale = useSdf
                        ? new Vector2(size * pose.Flip / white.Width, size * 1.16f / white.Height)
                        : new Vector2(size * pose.Flip / white.Width, size * 0.45f / white.Height);

                    spriteBatch.Draw(white, screenPosition, null, color,
                        pose.Rotation + portraitRotation, origin, petalScale, SpriteEffects.None, 0f);
                }
            } finally {
                spriteBatch.End();
            }
        }

        private bool TryGetPose(AssemblyPetal petal, out PetalPose pose) {
            float raw = (timer - petal.Delay) / petal.TravelFrames;
            if (raw < 0f) {
                pose = default;
                return false;
            }

            float u = MathHelper.Clamp(raw, 0f, 1f);
            float eased = Smooth01(u);
            Vector2 position = Vector2.Lerp(petal.StartPosition, petal.Target, eased);
            float swayEnvelope = MathF.Sin(u * MathHelper.Pi) * (1f - eased * 0.32f);
            position.X += MathF.Sin(petal.SwayPhase + u * MathHelper.TwoPi * petal.SwayCycles)
                * petal.SwayAmplitude * swayEnvelope;

            float visibleAlpha = SmoothStep(0f, 0.12f, u);
            float merge = petal.MergeIntoPortrait ? SmoothStep(0.67f, 0.98f, u) : 0f;
            if (petal.MergeIntoPortrait) {
                visibleAlpha *= 1f - merge;
            }
            else if (raw > 1f) {
                float afterArrival = (timer - petal.Delay - petal.TravelFrames);
                position = petal.Target + petal.ResidualVelocity * afterArrival;
                position.X += MathF.Sin(afterArrival * 0.13f + petal.FlipPhase) * 8f;
                visibleAlpha *= 1f - SmoothStep(10f, 38f, afterArrival);
            }

            if (visibleAlpha <= 0.005f) {
                pose = default;
                return false;
            }

            float fallAngle = (petal.Target - petal.StartPosition).ToRotation();
            float rotation = petal.StartRotation + timer * petal.RotationSpeed
                + fallAngle * 0.12f
                + MathF.Sin(petal.SwayPhase + u * MathHelper.TwoPi) * 0.16f;
            float flip = MathHelper.Lerp(0.18f, 1f,
                MathF.Abs(MathF.Sin(timer * petal.FlipSpeed + petal.FlipPhase)));
            pose = new PetalPose(position, rotation, flip, visibleAlpha, merge);
            return true;
        }

        private float GetMerge(AssemblyPetal petal) {
            float u = MathHelper.Clamp((timer - petal.Delay) / petal.TravelFrames, 0f, 1f);
            return SmoothStep(0.67f, 0.98f, u);
        }

        private void BuildPetals(Texture2D portrait) {
            petals.Clear();
            if (portrait == null || portrait.IsDisposed) {
                return;
            }

            int width = portrait.Width;
            int height = portrait.Height;

            Color[] pixels = null;
            try {
                pixels = new Color[width * height];
                portrait.GetData(pixels);
            } catch {
                pixels = null;
            }

            for (int i = 0; i < PetalCount; i++) {
                SampleTarget(pixels, width, height, out Vector2 target, out Color targetColor);
                bool mergeIntoPortrait = !Main.rand.NextBool(6);
                float verticalProgress = MathHelper.Clamp(target.Y / height, 0f, 1f);
                float delay = mergeIntoPortrait
                    ? verticalProgress * 36f + Main.rand.NextFloat(0f, 8f)
                    : Main.rand.NextFloat(0f, 10f);
                float travelFrames = mergeIntoPortrait
                    ? 42f + verticalProgress * 14f + Main.rand.NextFloat(-2f, 3f)
                    : Main.rand.NextFloat(42f, 52f);

                Vector2 startPosition = new(
                    MathHelper.Clamp(target.X + Main.rand.NextFloat(-width * 0.68f, width * 0.68f),
                        -width * 0.32f, width * 1.32f),
                    Main.rand.NextFloat(-height * 0.42f, -height * 0.10f));
                Vector2 residualVelocity = new(
                    Main.rand.NextFloat(-0.45f, 0.45f),
                    Main.rand.NextFloat(0.85f, 1.5f));

                petals.Add(new AssemblyPetal {
                    Target = target,
                    TargetColor = targetColor,
                    Tint = Color.Lerp(PetalPink, PetalPinkDeep, Main.rand.NextFloat()),
                    StartPosition = startPosition,
                    ResidualVelocity = residualVelocity,
                    SwayPhase = Main.rand.NextFloat(MathHelper.TwoPi),
                    SwayCycles = Main.rand.NextFloat(0.65f, 1.25f),
                    SwayAmplitude = Main.rand.NextFloat(22f, 68f),
                    Delay = delay,
                    TravelFrames = travelFrames,
                    Scale = Main.rand.NextFloat(0.58f, 1.16f),
                    StartRotation = Main.rand.NextFloat(MathHelper.TwoPi),
                    RotationSpeed = Main.rand.NextFloat(-0.075f, 0.075f),
                    FlipPhase = Main.rand.NextFloat(MathHelper.TwoPi),
                    FlipSpeed = Main.rand.NextFloat(0.09f, 0.17f),
                    MergeIntoPortrait = mergeIntoPortrait,
                    FrontLayer = Main.rand.NextBool(3)
                });
            }
        }

        private static void SampleTarget(Color[] pixels, int width, int height,
            out Vector2 target, out Color targetColor) {

            if (pixels != null) {
                for (int attempt = 0; attempt < 80; attempt++) {
                    int x = Main.rand.Next(width);
                    int y = Main.rand.Next(height);
                    Color sample = pixels[x + y * width];
                    if (sample.A < 80) {
                        continue;
                    }

                    target = new Vector2(x + Main.rand.NextFloat(), y + Main.rand.NextFloat());
                    targetColor = sample;
                    return;
                }
            }

            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            float radius = MathF.Sqrt(Main.rand.NextFloat());
            target = new Vector2(
                width * 0.5f + MathF.Cos(angle) * width * 0.32f * radius,
                height * 0.52f + MathF.Sin(angle) * height * 0.44f * radius);
            targetColor = PetalPinkDeep;
        }

        private void DisableAdvanced(Exception exception) {
            advancedDisabled = true;
            DisposeTargets();
            if (failureLogged) {
                return;
            }

            failureLogged = true;
            CWRMod.Instance.Logger.Warn($"Himayo portrait assembly RT fallback: {exception.Message}");
        }

        private void DisposeTargets() {
            portraitTarget?.Dispose();
            maskTargetA?.Dispose();
            maskTargetB?.Dispose();
            portraitTarget = null;
            maskTargetA = null;
            maskTargetB = null;
        }

        private static void UnbindAllTextures(GraphicsDevice graphicsDevice) {
            graphicsDevice.Textures[0] = null;
            UnbindAuxiliaryTextures(graphicsDevice);
        }

        private static void UnbindAuxiliaryTextures(GraphicsDevice graphicsDevice) {
            graphicsDevice.Textures[1] = null;
            graphicsDevice.Textures[2] = null;
        }

        private static void BeginDefaultUiBatch(SpriteBatch spriteBatch) {
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None,
                RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);
        }

        private static float Smooth01(float value) {
            value = MathHelper.Clamp(value, 0f, 1f);
            return value * value * (3f - 2f * value);
        }

        private static float SmoothStep(float edge0, float edge1, float value)
            => Smooth01((value - edge0) / Math.Max(edge1 - edge0, 0.0001f));
    }

    internal sealed class HimayoPortraitAssemblyLoader : ICWRLoader
    {
        void ICWRLoader.UnLoadData() => HimayoPortraitAssemblyRenderer.UnloadAll();
    }
}
