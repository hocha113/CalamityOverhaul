using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience
{
    /// <summary>
    /// 共享雾体绘制入口（<c>AmbientFogBody.fx</c>）。密度场单 pass、合成不透明度有界，
    /// 替代 Fog 贴图多层堆叠（堆叠饱和 1-(1-a)^N 不受控，审计 2026-08-29）。<br/>
    /// 身份不趋同：共享的只是密度场技法与 alpha 上限，调色板/构图参数/点缀层逐环境自持。<br/>
    /// uniform 是设备全局态，每次调用全参数重设；噪声占 s1，画完归还
    /// </summary>
    internal static class AmbientFogDraw
    {
        /// <summary>竖幕/横扫墙参数包（fx 画布契约：U=厚度向 1=前缘，V=长轴）</summary>
        internal struct WallSpec
        {
            /// <summary>画布中心（世界像素；ScreenSpace 时为屏幕像素）</summary>
            public Vector2 Center;
            /// <summary>画布尺寸（厚度, 长轴）像素</summary>
            public Vector2 SizePx;
            /// <summary>行进向（-1 时水平翻转画布，前缘朝左）</summary>
            public int Dir;
            public Color Body;
            public Color Edge;
            public float MaxAlpha;
            /// <summary>密度乘子（包络/呼吸从这里进）</summary>
            public float Density;
            /// <summary>介质内流速 px/s</summary>
            public float FlowPx;
            /// <summary>定向流丝强度 0~1</summary>
            public float Streak;
            public float Seed;
            /// <summary>密度峰 U 位（0.7 前缘浓，0.5 近对称）</summary>
            public float FrontBias;
            /// <summary>1=满幅充盈（镜头雾化层：关厚度向剖面与前缘截止）</summary>
            public float Fill;
            /// <summary>明窗中心 V 比例（负值=无窗）</summary>
            public float SeamV;
            public float SeamHalfV;
            /// <summary>长轴两端收口比例</summary>
            public float TaperV;
            public Vector2 NoiseOffsetPx;
            /// <summary>环境光乘算下限</summary>
            public float LightFloor;
            /// <summary>屏幕空间绘制（镜头雾化层；光照走 LightFlat）</summary>
            public bool ScreenSpace;
            /// <summary>ScreenSpace 时的平光系数</summary>
            public float LightFlat;

            public static WallSpec Default => new() {
                Dir = 1,
                MaxAlpha = 0.62f,
                Density = 1f,
                FlowPx = 200f,
                Streak = 0.6f,
                FrontBias = 0.7f,
                SeamV = -1f,
                SeamHalfV = 0.05f,
                TaperV = 0.13f,
                LightFloor = 0.3f,
                LightFlat = 1f,
            };
        }

        /// <summary>贴地雾带/悬浮雾盘参数包（fx 画布契约：U=横向，V=纵向）</summary>
        internal struct PoolSpec
        {
            public Vector2 Center;
            public Vector2 SizePx;
            public Color Body;
            public Color Edge;
            public float MaxAlpha;
            public float Density;
            public float FlowPx;
            public float Seed;
            /// <summary>1=贴地带（顶冠侵蚀），0=悬浮盘（上下对称）</summary>
            public float Anchor;
            /// <summary>顶冠侵蚀深度（V 比例）</summary>
            public float CrownV;
            /// <summary>横向包络锐度（越大外缘宽限带越长）</summary>
            public float EdgePow;
            /// <summary>盘内缓旋 rad/s</summary>
            public float Swirl;
            public Vector2 NoiseOffsetPx;
            public float LightFloor;

            public static PoolSpec Default => new() {
                MaxAlpha = 0.55f,
                Density = 1f,
                FlowPx = 16f,
                Anchor = 1f,
                CrownV = 0.42f,
                EdgePow = 2.2f,
                LightFloor = 0.3f,
            };
        }

        private static readonly float[] lightBuf = new float[8];

        /// <summary>沿长轴 8 点采样环境光系数（月照泛白，全黑沉没，保下限）</summary>
        private static void FillLight(Vector2 a, Vector2 b, float floor) {
            for (int i = 0; i < 8; i++) {
                Vector2 p = Vector2.Lerp(a, b, i / 7f);
                Color c = Lighting.GetColor((int)(p.X / 16f), (int)(p.Y / 16f));
                lightBuf[i] = floor + (1f - floor) * ((c.R + c.G + c.B) / 765f);
            }
        }

        private static void FillLightFlat(float v) {
            for (int i = 0; i < 8; i++) {
                lightBuf[i] = v;
            }
        }

        private static bool Ready(out Effect fx, out Texture2D noise, out Texture2D pixel) {
            fx = EffectLoader.AmbientFogBody?.Value;
            noise = CWRAsset.PerlinNoise?.Value;
            pixel = VaultAsset.placeholder2?.Value;
            return fx != null && noise != null && pixel != null;
        }

        /// <summary>全参数上载（共享 fx 的 uniform 跨调用残留，一个不许漏）</summary>
        private static void UploadShared(Effect fx, Vector2 sizePx, Vector2 noiseOffset,
            Color body, Color edge, float maxAlpha, float density, float flowPx, float seed) {
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uSeed"]?.SetValue(seed);
            fx.Parameters["uCanvasPx"]?.SetValue(sizePx);
            fx.Parameters["uNoiseOffsetPx"]?.SetValue(noiseOffset);
            fx.Parameters["uColorBody"]?.SetValue(body.ToVector4());
            fx.Parameters["uColorEdge"]?.SetValue(edge.ToVector4());
            fx.Parameters["uMaxAlpha"]?.SetValue(maxAlpha);
            fx.Parameters["uDensity"]?.SetValue(density);
            fx.Parameters["uFlowPx"]?.SetValue(flowPx);
            fx.Parameters["uLight"]?.SetValue(lightBuf);
        }

        /// <summary>实体批内绘制（弹幕 PreDraw 语境）：换批画完还原实体批</summary>
        internal static void DrawWallInEntityBatch(in WallSpec s) {
            Main.spriteBatch.End();
            DrawWallDirect(in s);
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>实体批内绘制（弹幕 PreDraw 语境）：换批画完还原实体批</summary>
        internal static void DrawPoolInEntityBatch(in PoolSpec s) {
            Main.spriteBatch.End();
            DrawPoolDirect(in s);
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>无批语境直接绘制（RenderHandle 自管批间调用）</summary>
        internal static void DrawWallDirect(in WallSpec s) {
            if (!Ready(out Effect fx, out Texture2D noise, out Texture2D pixel)) {
                return;
            }
            if (s.ScreenSpace) {
                FillLightFlat(s.LightFlat);
            }
            else {
                //墙长轴=V(竖直)，光沿顶→底采样
                FillLight(s.Center - new Vector2(0f, s.SizePx.Y * 0.5f),
                    s.Center + new Vector2(0f, s.SizePx.Y * 0.5f), s.LightFloor);
            }
            UploadShared(fx, s.SizePx, s.NoiseOffsetPx, s.Body, s.Edge,
                s.MaxAlpha, s.Density, s.FlowPx, s.Seed);
            fx.Parameters["uStreak"]?.SetValue(s.Streak);
            fx.Parameters["uFrontBias"]?.SetValue(s.FrontBias);
            fx.Parameters["uFill"]?.SetValue(s.Fill);
            fx.Parameters["uSeamV"]?.SetValue(s.SeamV);
            fx.Parameters["uSeamHalfV"]?.SetValue(s.SeamHalfV);
            fx.Parameters["uTaperV"]?.SetValue(s.TaperV);
            //池组参数一并复位，防跨调用残值
            fx.Parameters["uAnchor"]?.SetValue(0f);
            fx.Parameters["uCrownV"]?.SetValue(0f);
            fx.Parameters["uEdgePow"]?.SetValue(1f);
            fx.Parameters["uSwirl"]?.SetValue(0f);
            fx.CurrentTechnique = fx.Techniques["TechWall"];
            DrawQuad(fx, noise, pixel, s.Center, s.SizePx, s.Dir < 0, s.ScreenSpace);
        }

        /// <summary>无批语境直接绘制（RenderHandle 自管批间调用）</summary>
        internal static void DrawPoolDirect(in PoolSpec s) {
            if (!Ready(out Effect fx, out Texture2D noise, out Texture2D pixel)) {
                return;
            }
            //池长轴=U(横向)，光沿左→右采样
            FillLight(s.Center - new Vector2(s.SizePx.X * 0.5f, 0f),
                s.Center + new Vector2(s.SizePx.X * 0.5f, 0f), s.LightFloor);
            UploadShared(fx, s.SizePx, s.NoiseOffsetPx, s.Body, s.Edge,
                s.MaxAlpha, s.Density, s.FlowPx, s.Seed);
            fx.Parameters["uAnchor"]?.SetValue(s.Anchor);
            fx.Parameters["uCrownV"]?.SetValue(s.CrownV);
            fx.Parameters["uEdgePow"]?.SetValue(s.EdgePow);
            fx.Parameters["uSwirl"]?.SetValue(s.Swirl);
            //墙组参数一并复位
            fx.Parameters["uStreak"]?.SetValue(0f);
            fx.Parameters["uFrontBias"]?.SetValue(0.7f);
            fx.Parameters["uFill"]?.SetValue(0f);
            fx.Parameters["uSeamV"]?.SetValue(-1f);
            fx.Parameters["uSeamHalfV"]?.SetValue(0.05f);
            fx.Parameters["uTaperV"]?.SetValue(0.13f);
            fx.CurrentTechnique = fx.Techniques["TechPool"];
            DrawQuad(fx, noise, pixel, s.Center, s.SizePx, false, false);
        }

        private static void DrawQuad(Effect fx, Texture2D noise, Texture2D pixel,
            Vector2 center, Vector2 sizePx, bool flip, bool screenSpace) {
            GraphicsDevice device = Main.graphics.GraphicsDevice;
            device.Textures[1] = noise;
            device.SamplerStates[1] = SamplerState.LinearWrap;

            if (screenSpace) {
                Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                    SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, fx);
                Main.spriteBatch.Draw(pixel, center, null, Color.White, 0f, pixel.Size() * 0.5f,
                    new Vector2(sizePx.X / pixel.Width, sizePx.Y / pixel.Height),
                    flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
            }
            else {
                Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                    SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, fx,
                    Main.GameViewMatrix.TransformationMatrix);
                Main.spriteBatch.Draw(pixel, center - Main.screenPosition, null, Color.White, 0f,
                    pixel.Size() * 0.5f, new Vector2(sizePx.X / pixel.Width, sizePx.Y / pixel.Height),
                    flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
            }
            Main.spriteBatch.End();
            device.Textures[1] = null;//归还噪声槽（帧内邻居无自绑时会读到残值）
        }
    }
}
