using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Items.Melee.Abyssrends
{
    /// <summary>
    /// 裂渊色板与绘制。贴图是左下柄、右上尖的斜向长兵器，
    /// 握点/尖点按不透明像素实测，左右朝向镜像与 <see cref="DivineSourceBlades.DivineSourceBladeHeld"/> 同构
    /// </summary>
    internal static class AbyssrendFX
    {
        public const string ItemTexture = CWRConstant.Item_Melee + "Abyssrend";

        /// <summary>柄端，持握原点</summary>
        public static readonly Vector2 GripPixel = new(34f, 173f);
        /// <summary>刃尖</summary>
        public static readonly Vector2 TipPixel = new(166f, 27f);

        /// <summary>Burst/Clamp 着色器盘体半径(UV)，C# quadPx = 可见半径px / DiskR * 2</summary>
        public const float DiskR = 0.42f;

        public static readonly Color Deep = new(8, 16, 32);
        public static readonly Color Body = new(18, 42, 78);
        public static readonly Color Cyan = new(48, 230, 242);
        public static readonly Color Foam = new(168, 228, 245);
        public static readonly Color Core = new(210, 250, 255);

        public static float QuadPx(float visibleRadiusPx) => visibleRadiusPx / DiskR * 2f;

        /// <summary>
        /// 对角长兵器朝左用垂直翻转+镜像支点 Y（鬼切 OniBladePose），
        /// 反手斩再异或一次让钳口刃始终领先。禁止 FlipHorizontally，对角贴图会翻成刀背朝前
        /// </summary>
        public static void ComputeBladeDrawXform(Texture2D tex, float worldAngle, int facingDir, bool edgeFlip,
            out Vector2 origin, out float bladeRot, out SpriteEffects flip) {
            bool flipY = (facingDir < 0) != edgeFlip;
            origin = GripPixel;
            Vector2 tip = TipPixel;
            if (flipY) {
                flip = SpriteEffects.FlipVertically;
                origin.Y = tex.Height - origin.Y;
                tip.Y = tex.Height - tip.Y;
            }
            else {
                flip = SpriteEffects.None;
            }
            bladeRot = worldAngle - (tip - origin).ToRotation();
        }

        public static float BladeLength => (TipPixel - GripPixel).Length();

        /// <summary>
        /// 弧带。rots[0] 最新(刃口 u=1) → rots[n-1] 最旧(尾 u=0)。
        /// 纠缠之怨式轨迹在头部插补新样本，倒着喂会把刃口画在挥砍起点
        /// </summary>
        public static void DrawArcStrip(Vector2 center, float[] rots, int count,
            float inner, float outer, float fade) {
            if (count < 3 || fade <= 0.02f) {
                return;
            }
            var bars = new VertexPositionColorTexture[count * 2];
            float denom = Math.Max(count - 1, 1);
            for (int i = 0; i < count; i++) {
                float factor = 1f - i / denom;
                Vector2 dir = rots[i].ToRotationVector2();
                bars[i * 2] = new VertexPositionColorTexture((center + dir * outer).ToVector3()
                    , Color.White, new Vector2(factor, 0f));
                bars[i * 2 + 1] = new VertexPositionColorTexture((center + dir * inner).ToVector3()
                    , Color.White, new Vector2(factor, 1f));
            }
            DrawStrip("TechSlash", bars, fade);
        }

        /// <summary>沿路径的暗流管，UV.x 0 尾→1 头</summary>
        public static void DrawPathStrip(Vector2[] path, int count, Func<int, float> widthAt, float fade) {
            if (count < 2 || fade <= 0.02f) {
                return;
            }
            var bars = new VertexPositionColorTexture[count * 2];
            for (int i = 0; i < count; i++) {
                Vector2 dir;
                if (i == 0) {
                    dir = (path[1] - path[0]).SafeNormalize(Vector2.UnitX);
                }
                else if (i == count - 1) {
                    dir = (path[i] - path[i - 1]).SafeNormalize(Vector2.UnitX);
                }
                else {
                    dir = (path[i + 1] - path[i - 1]).SafeNormalize(Vector2.UnitX);
                }
                Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
                float w = widthAt(i);
                float u = i / (float)Math.Max(count - 1, 1);
                bars[i * 2] = new VertexPositionColorTexture((path[i] + perp * w).ToVector3()
                    , Color.White, new Vector2(u, 0f));
                bars[i * 2 + 1] = new VertexPositionColorTexture((path[i] - perp * w).ToVector3()
                    , Color.White, new Vector2(u, 1f));
            }
            DrawStrip("TechCurrent", bars, fade);
        }

        public static void DrawStrip(string tech, VertexPositionColorTexture[] bars, float fade) {
            if (bars == null || bars.Length < 4) {
                return;
            }
            Effect fx = EffectLoader.Abyssrend?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (fx == null || noise == null || fx.Techniques[tech] == null) {
                return;
            }
            fx.CurrentTechnique = fx.Techniques[tech];

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;
            device.Textures[1] = noise;
            device.SamplerStates[1] = SamplerState.LinearWrap;

            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["fadeAlpha"]?.SetValue(fade);
            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, bars, 0, bars.Length - 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }

        /// <summary>SpriteBatch 圆形技法(Burst/Clamp)。调用前后会拆合批次</summary>
        public static void DrawCanvasTech(string tech, Vector2 worldCenter, float quadPx, float progress, float fade) {
            Effect fx = EffectLoader.Abyssrend?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            Texture2D canvas = CWRAsset.Extra_98?.Value;
            if (fx == null || noise == null || canvas == null || fx.Techniques[tech] == null) {
                return;
            }
            fx.CurrentTechnique = fx.Techniques[tech];
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uProgress"]?.SetValue(progress);
            fx.Parameters["fadeAlpha"]?.SetValue(fade);

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            device.Textures[1] = noise;
            device.SamplerStates[1] = SamplerState.LinearWrap;

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp
                , DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);

            float scale = quadPx / canvas.Width;
            Main.spriteBatch.Draw(canvas, worldCenter - Main.screenPosition, null, Color.White
                , 0f, canvas.Size() * 0.5f, scale, SpriteEffects.None, 0f);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState
                , DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }
    }
}
