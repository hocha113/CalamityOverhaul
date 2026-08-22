using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.Scenarios.Shenyo
{
    /// <summary>
    /// 沈幽立绘黑雨汇聚：雨丝坠向立绘不透明像素，命中处向密度RT盖竖向湿痕，
    /// 模糊后交给 ShenyoRainForm 合成，黑水剪影先灌满，末拍澄清本色
    /// 架构镜像 <see cref="Himayo.HimayoPortraitAssemblyRenderer"/>
    /// </summary>
    internal sealed class ShenyoPortraitRainRenderer : INeedRenderTargetContent
    {
        //88f 落雨暴→黑水灌满→澄清定形
        private const int TotalFrames = 88;
        private const int DropCount = 260;
        private const int TargetPadding = 36;
        //湿痕印记基准尺寸（RT像素）：窄条竖向拉长
        private const float StampWidth = 6f;
        private const float StampHeight = 24f;

        //黑雨色板：近黑雨体 + 湿墨冷青亮头（鬼雨湿墨系，禁暖）
        private static readonly Color DropDark = new(16, 21, 25);
        private static readonly Color DropPale = new(136, 202, 216);
        private static readonly Color MurkColor = new(14, 18, 21);
        private static readonly Color EdgeColor = new(196, 214, 218);
        private static readonly Color StreakColor = new(136, 202, 216);
        private static readonly List<ShenyoPortraitRainRenderer> Instances = [];

        private sealed class RainDrop
        {
            public Vector2 Target;
            public Vector2 StartPosition;
            public Vector2 ResidualVelocity;
            public float Delay;
            public float TravelFrames;
            public float Scale;
            public float DriftPhase;
            public bool MergeIntoPortrait;
            public bool FrontLayer;
        }

        private readonly struct DropPose
        {
            public readonly Vector2 Position;
            public readonly Vector2 Velocity;
            public readonly float Alpha;
            public readonly float Merge;

            public DropPose(Vector2 position, Vector2 velocity, float alpha, float merge) {
                Position = position;
                Velocity = velocity;
                Alpha = alpha;
                Merge = merge;
            }
        }

        private readonly List<RainDrop> drops = [];

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

        internal ShenyoPortraitRainRenderer() {
            Instances.Add(this);
        }

        internal static void UnloadAll() {
            foreach (ShenyoPortraitRainRenderer renderer in Instances) {
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
            BuildDrops(portrait);
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
            drops.Clear();
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

            if (drops.Count == 0) {
                BuildDrops(portrait);
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
                DrawRainLayer(spriteBatch, portraitPosition, portraitScale, portraitRotation, alpha, false);

                if (renderPrepared && TargetsReady()) {
                    DrawComposite(spriteBatch, graphicsDevice, portraitPosition, portraitScale,
                        portraitRotation, drawColor, alpha);
                }
                else {
                    DrawFallbackPortrait(spriteBatch, portrait, faceTexture, faceOffset, portraitPosition,
                        portraitScale, portraitRotation, drawColor, alpha);
                }

                DrawRainLayer(spriteBatch, portraitPosition, portraitScale, portraitRotation, alpha, true);
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
            Effect rainEffect = EffectLoader.ShenyoRainForm?.Value;
            Texture2D white = VaultAsset.placeholder2?.Value;
            if (portrait == null || portrait.IsDisposed || rainEffect == null || white == null) {
                return;
            }

            Exception failure = null;
            try {
                EnsureTargets(graphicsDevice, portrait.Width + TargetPadding * 2, portrait.Height + TargetPadding * 2);
                BuildPortraitTarget(spriteBatch, graphicsDevice, portrait, faceTexture, requestedFaceOffset);
                BuildRainMask(spriteBatch, graphicsDevice, white);
                BlurMask(spriteBatch, graphicsDevice, rainEffect);
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

        private void BuildRainMask(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, Texture2D white) {
            UnbindAllTextures(graphicsDevice);
            graphicsDevice.SetRenderTarget(maskTargetA);
            graphicsDevice.Clear(Color.Transparent);

            //湿痕是普通矩形印记，无需shader；模糊pass负责把它们洇开
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
            try {
                Vector2 origin = white.Size() * 0.5f;
                Vector2 padding = new(TargetPadding);
                foreach (RainDrop drop in drops) {
                    if (!drop.MergeIntoPortrait) {
                        continue;
                    }

                    float merge = GetMerge(drop);
                    if (merge <= 0.005f) {
                        continue;
                    }

                    //湿痕随汇聚长高：雨水落点向下淌出一道
                    float grow = MathHelper.Lerp(0.8f, 1.5f, merge);
                    float w = StampWidth * drop.Scale * grow;
                    float h = StampHeight * drop.Scale * grow;
                    Vector2 stampScale = new(w / white.Width, h / white.Height);
                    Color maskColor = Color.White;
                    maskColor.A = (byte)(255f * merge);
                    //印记锚在上缘：湿痕自命中点向下垂
                    spriteBatch.Draw(white, padding + drop.Target, null, maskColor,
                        0f, new Vector2(origin.X, 0f), stampScale, SpriteEffects.None, 0f);
                }
            } finally {
                spriteBatch.End();
            }
        }

        private void BlurMask(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, Effect rainEffect) {
            rainEffect.CurrentTechnique = rainEffect.Techniques["BlurTech"];
            float texelX = 1f / maskTargetA.Width;
            float texelY = 1f / maskTargetA.Height;

            for (int i = 0; i < 2; i++) {
                float radius = i == 0 ? 1.35f : 2.8f;

                UnbindAllTextures(graphicsDevice);
                graphicsDevice.SetRenderTarget(maskTargetB);
                graphicsDevice.Clear(Color.Transparent);
                rainEffect.Parameters["uDelta"]?.SetValue(new Vector2(texelX * radius, 0f));
                spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque,
                    SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
                try {
                    rainEffect.CurrentTechnique.Passes[0].Apply();
                    spriteBatch.Draw(maskTargetA, Vector2.Zero, Color.White);
                } finally {
                    spriteBatch.End();
                }

                UnbindAllTextures(graphicsDevice);
                graphicsDevice.SetRenderTarget(maskTargetA);
                graphicsDevice.Clear(Color.Transparent);
                rainEffect.Parameters["uDelta"]?.SetValue(new Vector2(0f, texelY * radius));
                spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque,
                    SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
                try {
                    rainEffect.CurrentTechnique.Passes[0].Apply();
                    spriteBatch.Draw(maskTargetB, Vector2.Zero, Color.White);
                } finally {
                    spriteBatch.End();
                }
            }
        }

        private void DrawComposite(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice,
            Vector2 portraitPosition, float portraitScale, float portraitRotation, Color drawColor, float alpha) {

            Effect effect = EffectLoader.ShenyoRainForm.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value ?? VaultAsset.placeholder2.Value;

            effect.Parameters["uProgress"]?.SetValue(Progress);
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uTexelSize"]?.SetValue(new Vector2(1f / maskTargetA.Width, 1f / maskTargetA.Height));
            effect.Parameters["uMurkColor"]?.SetValue(MurkColor.ToVector3());
            effect.Parameters["uEdgeColor"]?.SetValue(EdgeColor.ToVector3());
            effect.Parameters["uStreakColor"]?.SetValue(StreakColor.ToVector3());

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

            float reveal = SmoothStep(0.06f, 0.90f, Progress);
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

        private void DrawRainLayer(SpriteBatch spriteBatch, Vector2 portraitPosition,
            float portraitScale, float portraitRotation, float alpha, bool frontLayer) {

            Texture2D white = VaultAsset.placeholder2?.Value;
            if (white == null) {
                return;
            }

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.UIScaleMatrix);
            try {
                Vector2 origin = white.Size() * 0.5f;
                foreach (RainDrop drop in drops) {
                    if (drop.FrontLayer != frontLayer || !TryGetPose(drop, out DropPose pose)) {
                        continue;
                    }

                    Vector2 screenPosition = portraitPosition
                        + pose.Position.RotatedBy(portraitRotation) * portraitScale;
                    float speed = pose.Velocity.Length();
                    if (speed < 0.001f) {
                        continue;
                    }

                    //速度拉伸雨丝：长度吃速度，宽度固定窄条
                    float length = MathHelper.Clamp(speed * 2.8f, 8f, 42f) * drop.Scale * portraitScale;
                    float width = 1.8f * drop.Scale * portraitScale;
                    float rotation = pose.Velocity.ToRotation() + MathHelper.PiOver2 + portraitRotation;
                    float opacity = MathHelper.Clamp(alpha * pose.Alpha * (frontLayer ? 0.92f : 0.66f), 0f, 1f);

                    //雨体近黑，融合期向水色让一步
                    Color body = Color.Lerp(DropDark, MurkColor, pose.Merge * 0.6f) * opacity;
                    spriteBatch.Draw(white, screenPosition, null, body, rotation, origin,
                        new Vector2(width / white.Width, length / white.Height), SpriteEffects.None, 0f);

                    //行进端一点湿墨亮头，暗背景上的可读性
                    Color head = DropPale * (opacity * 0.38f);
                    Vector2 headPos = screenPosition + pose.Velocity.SafeNormalize(Vector2.UnitY)
                        * (length * 0.5f) * 0.9f;
                    spriteBatch.Draw(white, headPos, null, head, rotation, origin,
                        new Vector2(width * 0.9f / white.Width, length * 0.22f / white.Height), SpriteEffects.None, 0f);
                }
            } finally {
                spriteBatch.End();
            }
        }

        private bool TryGetPose(RainDrop drop, out DropPose pose) {
            float raw = (timer - drop.Delay) / drop.TravelFrames;
            if (raw < 0f) {
                pose = default;
                return false;
            }

            float u = MathHelper.Clamp(raw, 0f, 1f);
            Vector2 fallVelocity = (drop.Target - drop.StartPosition) / drop.TravelFrames;
            Vector2 position = Vector2.Lerp(drop.StartPosition, drop.Target, u);
            //轻微横向漂移：雨有风感但不飘
            position.X += MathF.Sin(drop.DriftPhase + u * MathHelper.TwoPi * 0.8f) * 3.5f * (1f - u);

            float visibleAlpha = SmoothStep(0f, 0.10f, u);
            float merge = drop.MergeIntoPortrait ? SmoothStep(0.72f, 0.96f, u) : 0f;
            Vector2 velocity = fallVelocity;

            if (drop.MergeIntoPortrait) {
                //抵达即渗入湿痕，雨体让位给遮罩印记
                visibleAlpha *= 1f - merge;
            }
            else if (raw > 1f) {
                //溅开：小段反弹 + 重力回坠，快速消散
                float afterArrival = timer - drop.Delay - drop.TravelFrames;
                Vector2 gravity = new(0f, 0.16f * afterArrival);
                position = drop.Target + drop.ResidualVelocity * afterArrival + gravity * afterArrival * 0.5f;
                velocity = drop.ResidualVelocity + gravity;
                visibleAlpha *= 1f - SmoothStep(3f, 10f, afterArrival);
            }

            if (visibleAlpha <= 0.005f) {
                pose = default;
                return false;
            }

            pose = new DropPose(position, velocity, visibleAlpha, merge);
            return true;
        }

        private float GetMerge(RainDrop drop) {
            float u = MathHelper.Clamp((timer - drop.Delay) / drop.TravelFrames, 0f, 1f);
            return SmoothStep(0.72f, 0.96f, u);
        }

        private void BuildDrops(Texture2D portrait) {
            drops.Clear();
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

            for (int i = 0; i < DropCount; i++) {
                SampleTarget(pixels, width, height, out Vector2 target);
                bool mergeIntoPortrait = !Main.rand.NextBool(6);
                float verticalProgress = MathHelper.Clamp(target.Y / height, 0f, 1f);
                //伞顶先砸，错拍收成一波暴雨
                float delay = mergeIntoPortrait
                    ? verticalProgress * 22f + Main.rand.NextFloat(0f, 6f)
                    : Main.rand.NextFloat(0f, 10f);
                float travelFrames = Main.rand.NextFloat(10f, 16f);

                //雨近乎垂直，起点更高、短行程读成砸落
                Vector2 startPosition = new(
                    target.X + Main.rand.NextFloat(-width * 0.06f, width * 0.06f),
                    Main.rand.NextFloat(-height * 0.58f, -height * 0.18f));
                //溅开的反弹速度：向上外弹一小口
                Vector2 residualVelocity = new(
                    Main.rand.NextFloat(-1.4f, 1.4f),
                    Main.rand.NextFloat(-2.6f, -1.1f));

                drops.Add(new RainDrop {
                    Target = target,
                    StartPosition = startPosition,
                    ResidualVelocity = residualVelocity,
                    Delay = delay,
                    TravelFrames = travelFrames,
                    Scale = Main.rand.NextFloat(0.6f, 1.15f),
                    DriftPhase = Main.rand.NextFloat(MathHelper.TwoPi),
                    MergeIntoPortrait = mergeIntoPortrait,
                    FrontLayer = Main.rand.NextBool(2)
                });
            }
        }

        private static void SampleTarget(Color[] pixels, int width, int height, out Vector2 target) {
            if (pixels != null) {
                for (int attempt = 0; attempt < 80; attempt++) {
                    int x = Main.rand.Next(width);
                    int y = Main.rand.Next(height);
                    Color sample = pixels[x + y * width];
                    if (sample.A < 80) {
                        continue;
                    }

                    target = new Vector2(x + Main.rand.NextFloat(), y + Main.rand.NextFloat());
                    return;
                }
            }

            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            float radius = MathF.Sqrt(Main.rand.NextFloat());
            target = new Vector2(
                width * 0.5f + MathF.Cos(angle) * width * 0.32f * radius,
                height * 0.52f + MathF.Sin(angle) * height * 0.44f * radius);
        }

        private void DisableAdvanced(Exception exception) {
            advancedDisabled = true;
            DisposeTargets();
            if (failureLogged) {
                return;
            }

            failureLogged = true;
            CWRMod.Instance.Logger.Warn($"Shenyo portrait rain RT fallback: {exception.Message}");
        }

        private void DisposeTargets() {
            portraitTarget.SafeDispose();
            maskTargetA.SafeDispose();
            maskTargetB.SafeDispose();
            portraitTarget = null;
            maskTargetA = null;
            maskTargetB = null;
        }

        private static void UnbindAllTextures(GraphicsDevice graphicsDevice) {
            graphicsDevice.Textures[0] = null;
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

    internal sealed class ShenyoPortraitRainLoader : ICWRLoader
    {
        void ICWRLoader.UnLoadData() => ShenyoPortraitRainRenderer.UnloadAll();
    }
}
