using CalamityOverhaul.Common;
using InnoVault.Models3D.Runtime;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.Scenarios.Himayo.ToriiShrines
{
    /// <summary>
    /// 鸟居退场的 RT 溶解合成器：退场期间接管 Models3D 的 AfterTiles 层合成，
    /// 对烘焙好的模型画面做世界空间噪声溶解 + 地面线裁剪（沉入部分由此获得土层遮挡）。
    /// 另提供一次性的层 RT 剪影采样，让樱瓣从真实渲染轮廓上剥离。
    /// 纯客户端视觉，由 <see cref="ToriiShrineActor"/> 驱动
    /// </summary>
    internal static class ToriiShrineDissolve
    {
        //委托实例缓存：解钩时用引用相等判断，避免顶掉未来其它订阅者
        private static readonly Model3DRenderer.Model3DCompositeOverride CompositeFn = Composite;
        private static readonly Action<Model3DLayer, RenderTarget2D> CaptureFn = CaptureSilhouette;

        //鸟居的世界空间包围盒（相对地面锚点），比模型实际延展略宽以容纳颤抖位移
        private const float BoundsHalfWidth = 185f;
        private const float BoundsTop = 310f;
        private const float BoundsBottom = 30f;

        private static bool hooked;
        private static bool capturePending;
        private static bool failureLogged;
        private static Vector2 captureAnchor;
        private static List<Vector2> capturedOffsets;

        /// <summary>溶解推进度：0=完好 1=溶尽，由退场状态机逐帧写入</summary>
        public static float Progress { get; set; }
        /// <summary>地面裁剪线（世界Y），之下的模型像素视为已入土</summary>
        public static float GroundY { get; set; }
        /// <summary>鸟居地面锚点，用于计算 shader 作用包围盒</summary>
        public static Vector2 Anchor { get; private set; }

        /// <summary>
        /// 开始接管合成并请求一次剪影采样；在退场动画第一帧调用（此时模型仍完整在屏）
        /// </summary>
        public static void Begin(Vector2 anchor) {
            if (Main.dedServ) {
                return;
            }

            Anchor = anchor;
            Progress = 0f;
            GroundY = anchor.Y + 2f;

            if (!hooked) {
                Model3DRenderer.CompositeOverride = CompositeFn;
                hooked = true;
            }
            if (!capturePending) {
                capturePending = true;
                captureAnchor = anchor;
                Model3DRenderer.OnLayerRendered += CaptureFn;
            }
        }

        /// <summary>
        /// 归还合成权并解除采样订阅；退场结束/中断时调用，幂等
        /// </summary>
        public static void End() {
            if (hooked) {
                if (Model3DRenderer.CompositeOverride == CompositeFn) {
                    Model3DRenderer.CompositeOverride = null;
                }
                hooked = false;
            }
            if (capturePending) {
                Model3DRenderer.OnLayerRendered -= CaptureFn;
                capturePending = false;
            }
            Progress = 0f;
        }

        /// <summary>完全复位（含丢弃已采样剪影），世界卸载/Mod卸载路径调用</summary>
        public static void Reset() {
            End();
            capturedOffsets = null;
            failureLogged = false;
        }

        /// <summary>
        /// 取走剪影采样结果（相对锚点的世界偏移集合）；一次性移交，未就绪或采样失败返回 false
        /// </summary>
        public static bool TryTakeSilhouette(out List<Vector2> points) {
            points = capturedOffsets;
            capturedOffsets = null;
            return points != null && points.Count > 0;
        }

        private static bool Composite(Model3DLayer layer, RenderTarget2D rt, SpriteBatch spriteBatch) {
            if (layer != Model3DLayer.AfterTiles || rt == null || rt.IsDisposed) {
                return false;
            }

            Effect effect = EffectLoader.ToriiDissolve?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            GraphicsDevice graphicsDevice = Main.instance?.GraphicsDevice;
            if (effect == null || noise == null || graphicsDevice == null) {
                return false;
            }

            //uv→世界坐标的仿射映射：取 RT 两角求逆变换，天然兼容缩放/翻转镜头
            Matrix view = Main.GameViewMatrix.TransformationMatrix;
            Matrix inverse = Matrix.Invert(view);
            Vector2 world00 = Vector2.Transform(Vector2.Zero, inverse) + Main.screenPosition;
            Vector2 world11 = Vector2.Transform(new Vector2(rt.Width, rt.Height), inverse) + Main.screenPosition;

            Vector2 rtA = Vector2.Transform(Anchor + new Vector2(-BoundsHalfWidth, -BoundsTop) - Main.screenPosition, view);
            Vector2 rtB = Vector2.Transform(Anchor + new Vector2(BoundsHalfWidth, BoundsBottom) - Main.screenPosition, view);
            Vector4 bounds = new(
                MathF.Min(rtA.X, rtB.X) / rt.Width, MathF.Min(rtA.Y, rtB.Y) / rt.Height,
                MathF.Max(rtA.X, rtB.X) / rt.Width, MathF.Max(rtA.Y, rtB.Y) / rt.Height);

            effect.Parameters["uProgress"]?.SetValue(Progress);
            effect.Parameters["uWorldScale"]?.SetValue(world11 - world00);
            effect.Parameters["uWorldOffset"]?.SetValue(world00);
            effect.Parameters["uGroundY"]?.SetValue(GroundY);
            effect.Parameters["uEdgeColor"]?.SetValue(new Color(255, 106, 143).ToVector3());
            effect.Parameters["uBounds"]?.SetValue(bounds);

            Texture previousTexture1 = graphicsDevice.Textures[1];
            SamplerState previousSampler1 = graphicsDevice.SamplerStates[1];
            bool begun = false;
            try {
                spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                    SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullCounterClockwise);
                begun = true;
                graphicsDevice.Textures[1] = noise;
                graphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
                effect.CurrentTechnique.Passes[0].Apply();
                spriteBatch.Draw(rt, Vector2.Zero, Color.White);
            }
            catch (Exception exception) {
                if (!failureLogged) {
                    failureLogged = true;
                    CWRMod.Instance.Logger.Warn($"[ToriiShrineDissolve] composite failed, falling back: {exception.Message}");
                }
                return false;
            }
            finally {
                if (begun) {
                    spriteBatch.End();
                }
                graphicsDevice.Textures[1] = previousTexture1;
                graphicsDevice.SamplerStates[1] = previousSampler1;
            }
            return true;
        }

        /// <summary>
        /// 一次性剪影采样：读回层 RT 中鸟居包围盒内的不透明像素，转换为相对锚点的世界偏移。
        /// GPU→CPU 读回只发生这一帧，之后立即退订
        /// </summary>
        private static void CaptureSilhouette(Model3DLayer layer, RenderTarget2D rt) {
            if (layer != Model3DLayer.AfterTiles || rt == null || rt.IsDisposed) {
                return;
            }

            Model3DRenderer.OnLayerRendered -= CaptureFn;
            capturePending = false;

            try {
                capturedOffsets = SampleSilhouette(rt, captureAnchor);
            }
            catch (Exception exception) {
                capturedOffsets = null;
                CWRMod.Instance.Logger.Warn($"[ToriiShrineDissolve] silhouette capture failed: {exception.Message}");
            }
        }

        private static List<Vector2> SampleSilhouette(RenderTarget2D rt, Vector2 anchor) {
            Matrix view = Main.GameViewMatrix.TransformationMatrix;
            Vector2 rtA = Vector2.Transform(anchor + new Vector2(-BoundsHalfWidth, -BoundsTop) - Main.screenPosition, view);
            Vector2 rtB = Vector2.Transform(anchor + new Vector2(BoundsHalfWidth, BoundsBottom) - Main.screenPosition, view);

            int x0 = Math.Clamp((int)MathF.Floor(MathF.Min(rtA.X, rtB.X)), 0, rt.Width - 1);
            int y0 = Math.Clamp((int)MathF.Floor(MathF.Min(rtA.Y, rtB.Y)), 0, rt.Height - 1);
            int x1 = Math.Clamp((int)MathF.Ceiling(MathF.Max(rtA.X, rtB.X)), 0, rt.Width);
            int y1 = Math.Clamp((int)MathF.Ceiling(MathF.Max(rtA.Y, rtB.Y)), 0, rt.Height);
            int width = x1 - x0;
            int height = y1 - y0;
            if (width < 16 || height < 16) {
                //鸟居几乎不在屏上：放弃读回，交给调用方的程序化兜底
                return null;
            }

            Color[] data = new Color[width * height];
            rt.GetData(0, new Rectangle(x0, y0, width, height), data, 0, data.Length);

            Matrix inverse = Matrix.Invert(view);
            List<Vector2> points = new(360);
            for (int y = 0; y < height; y += 3) {
                for (int x = 0; x < width; x += 3) {
                    if (data[y * width + x].A < 64) {
                        continue;
                    }
                    Vector2 world = Vector2.Transform(new Vector2(x0 + x + 0.5f, y0 + y + 0.5f), inverse)
                        + Main.screenPosition;
                    points.Add(world - anchor);
                }
            }

            //控制点数：均匀抽稀到发射点池的合理规模
            const int MaxPoints = 340;
            if (points.Count > MaxPoints) {
                List<Vector2> trimmed = new(MaxPoints);
                float step = points.Count / (float)MaxPoints;
                for (int i = 0; i < MaxPoints; i++) {
                    trimmed.Add(points[(int)(i * step)]);
                }
                points = trimmed;
            }
            return points.Count > 0 ? points : null;
        }
    }

    internal sealed class ToriiShrineDissolveLoader : ICWRLoader
    {
        void ICWRLoader.UnLoadData() => ToriiShrineDissolve.Reset();
    }
}
