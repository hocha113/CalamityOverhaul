using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;

namespace CalamityOverhaul.Content.Items.Melee.SpearOfLonginuses
{
    /// <summary>朗基努斯专用着色器</summary>
    internal class LonginusAssets
    {
        /// <summary>AT力场八边形</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Asset<Effect> LonginusATField { get; set; }
        /// <summary>十字光柱</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Asset<Effect> LonginusCross { get; set; }
        /// <summary>双螺旋尾迹</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Asset<Effect> LonginusHelix { get; set; }
        /// <summary>光轮</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Asset<Effect> LonginusHalo { get; set; }
        ///// <summary>处决冲击帧</summary>
        //[VaultLoaden(CWRConstant.Effects)]
        //public static Asset<Effect> LonginusImpact { get; set; }
    }

    /// <summary>朗基努斯共用图元绘制与配色</summary>
    internal static class LonginusVFX
    {
        /// <summary>AT力场琥珀橙</summary>
        public static readonly Color Amber = new(255, 158, 40);
        /// <summary>圣光金</summary>
        public static readonly Color HolyGold = new(255, 214, 96);
        /// <summary>枪体绯红</summary>
        public static readonly Color Crimson = new(232, 36, 48);

        /// <summary>
        /// 层叠AT力场 quad，沿 normal 方向近大远小错相排开<br/>
        /// normal 指向来袭方向；squash=1 为正对镜头平面，越小越侧倾
        /// </summary>
        public static void DrawATField(Vector2 center, Vector2 normal, float radius, float spread
            , float shatter, float alphaMul, int layers = 3, float phaseSeed = 0f, float squash = 0.62f) {
            Effect effect = LonginusAssets.LonginusATField?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null || alphaMul <= 0.01f || radius < 2f) {
                return;
            }

            normal = normal.UnitVector();
            if (normal == Vector2.Zero) {
                normal = Vector2.UnitX;
            }
            Vector2 perp = normal.RotatedBy(MathHelper.PiOver2);

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.Additive;
            device.RasterizerState = RasterizerState.CullNone;

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uNoiseTex"]?.SetValue(noise);

            for (int i = 0; i < layers; i++) {
                float layerSpread = MathHelper.Clamp(spread * 1.15f - i * 0.18f, 0f, 1f);
                float layerShatter = MathHelper.Clamp(shatter * 1.2f - i * 0.15f, 0f, 1f);
                if (layerSpread <= 0.001f) {
                    continue;
                }
                float scale = 1f - i * 0.13f;
                Vector2 c = center + normal * (radius * 0.24f * i);
                Vector2 a = normal * (radius * squash * scale);
                Vector2 b = perp * (radius * scale);
                Vector2 shear = perp * (radius * 0.07f * i);
                Color tint = Color.White * (alphaMul * (1f - i * 0.24f));

                VertexPositionColorTexture[] quad = new VertexPositionColorTexture[4];
                quad[0] = new((c + a + b + shear).ToVector3(), tint, new Vector2(0f, 0f));
                quad[1] = new((c + a - b + shear).ToVector3(), tint, new Vector2(0f, 1f));
                quad[2] = new((c - a + b - shear).ToVector3(), tint, new Vector2(1f, 0f));
                quad[3] = new((c - a - b - shear).ToVector3(), tint, new Vector2(1f, 1f));

                effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                effect.Parameters["uSpread"]?.SetValue(layerSpread);
                effect.Parameters["uShatter"]?.SetValue(layerShatter);
                effect.Parameters["uPhase"]?.SetValue(phaseSeed + i * 0.37f);
                foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                    pass.Apply();
                    device.DrawUserPrimitives(PrimitiveType.TriangleStrip, quad, 0, 2);
                }
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }

        /// <summary>
        /// 拉丁十字光柱 quad<br/>
        /// up=立轴上端方向，halfLength=立柱半长px，halfWidth=横臂半展px<br/>
        /// widthUnits=柱体半厚(横向归一单位)，fill 自下而上点亮(计量用)
        /// </summary>
        public static void DrawCross(Vector2 center, Vector2 up, float halfLength, float halfWidth
            , float grow, float dissolve, float alphaMul, float widthUnits = 0.16f, float hot = 0f
            , float fill = 1f, Color? tint = null) {
            Effect effect = LonginusAssets.LonginusCross?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null || alphaMul <= 0.01f || grow <= 0.001f || dissolve >= 0.999f) {
                return;
            }

            up = up.UnitVector();
            if (up == Vector2.Zero) {
                up = -Vector2.UnitY;
            }
            Vector2 perp = up.RotatedBy(MathHelper.PiOver2) * halfWidth;
            Vector2 top = center + up * halfLength;
            Vector2 bottom = center - up * halfLength;
            Color color = (tint ?? Color.White) * alphaMul;

            VertexPositionColorTexture[] quad = new VertexPositionColorTexture[4];
            quad[0] = new((top + perp).ToVector3(), color, new Vector2(0f, 0f));
            quad[1] = new((top - perp).ToVector3(), color, new Vector2(0f, 1f));
            quad[2] = new((bottom + perp).ToVector3(), color, new Vector2(1f, 0f));
            quad[3] = new((bottom - perp).ToVector3(), color, new Vector2(1f, 1f));

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.Additive;
            device.RasterizerState = RasterizerState.CullNone;

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uNoiseTex"]?.SetValue(noise);
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uGrow"]?.SetValue(grow);
            effect.Parameters["uDissolve"]?.SetValue(dissolve);
            effect.Parameters["uFill"]?.SetValue(fill);
            effect.Parameters["uAspect"]?.SetValue(halfLength / halfWidth);
            effect.Parameters["uWidth"]?.SetValue(widthUnits);
            effect.Parameters["uHot"]?.SetValue(hot);
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, quad, 0, 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }

        /// <summary>
        /// 圣光光轮 quad，squash 竖向压扁模拟倾斜冠环
        /// </summary>
        public static void DrawHalo(Vector2 center, float radius, float squash, float reveal
            , float pulse, float alphaMul, Color? tint = null) {
            Effect effect = LonginusAssets.LonginusHalo?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null || alphaMul <= 0.01f || reveal <= 0.01f || radius < 2f) {
                return;
            }

            float rx = radius * 1.45f;
            float ry = radius * squash * 1.45f;
            Color color = (tint ?? Color.White) * alphaMul;

            VertexPositionColorTexture[] quad = new VertexPositionColorTexture[4];
            quad[0] = new(new Vector3(center.X - rx, center.Y - ry, 0f), color, new Vector2(0f, 0f));
            quad[1] = new(new Vector3(center.X + rx, center.Y - ry, 0f), color, new Vector2(1f, 0f));
            quad[2] = new(new Vector3(center.X - rx, center.Y + ry, 0f), color, new Vector2(0f, 1f));
            quad[3] = new(new Vector3(center.X + rx, center.Y + ry, 0f), color, new Vector2(1f, 1f));

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.Additive;
            device.RasterizerState = RasterizerState.CullNone;

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uNoiseTex"]?.SetValue(noise);
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uReveal"]?.SetValue(reveal);
            effect.Parameters["uPulse"]?.SetValue(pulse);
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, quad, 0, 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }

        /// <summary>暗红副股</summary>
        private static readonly Color HelixDark = new(126, 18, 30);

        /// <summary>
        /// 双螺旋尾迹，两股相位差 π 缠绕<br/>
        /// points 头→尾世界点列(count 个有效)，spinPhase 推进产生拧转，erode 尾先碎
        /// </summary>
        public static void DrawHelixTrail(Vector2[] points, int count, float baseWidth, float amplitude
            , float spinPhase, float erode, float alphaMul, float hot = 0.2f, float twists = 2.4f) {
            Effect effect = LonginusAssets.LonginusHelix?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null || count < 3 || alphaMul <= 0.01f || erode >= 0.999f) {
                return;
            }

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.Additive;
            device.RasterizerState = RasterizerState.CullNone;

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uNoiseTex"]?.SetValue(noise);
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uErode"]?.SetValue(erode);
            effect.Parameters["uHot"]?.SetValue(hot);

            VertexPositionColorTexture[] strip = new VertexPositionColorTexture[count * 2];
            for (int s = 0; s < 2; s++) {
                float phase0 = spinPhase + s * MathHelper.Pi;
                Color strand = (s == 0 ? Crimson : HelixDark) * alphaMul;
                for (int i = 0; i < count; i++) {
                    float t = i / (count - 1f);
                    Vector2 pos = points[i];
                    Vector2 tangent = (points[System.Math.Min(i + 1, count - 1)] - points[System.Math.Max(i - 1, 0)]).UnitVector();
                    if (tangent == Vector2.Zero) {
                        tangent = Vector2.UnitX;
                    }
                    Vector2 perp = tangent.RotatedBy(MathHelper.PiOver2);

                    float ph = t * twists * MathHelper.TwoPi + phase0;
                    float lateral = (float)System.Math.Sin(ph);
                    float depth01 = (float)System.Math.Cos(ph) * 0.5f + 0.5f;

                    //振幅头部收拢尾部微敛
                    float amp = amplitude * MathHelper.Clamp(t / 0.22f, 0f, 1f) * (1f - t * 0.25f);
                    Vector2 center = pos + perp * lateral * amp;
                    //近侧股略粗
                    float halfW = baseWidth * (1f - t * 0.5f) * (0.72f + 0.42f * depth01);

                    strip[i * 2] = new(new Vector3(center.X + perp.X * halfW, center.Y + perp.Y * halfW, depth01), strand, new Vector2(t, 0f));
                    strip[i * 2 + 1] = new(new Vector3(center.X - perp.X * halfW, center.Y - perp.Y * halfW, depth01), strand, new Vector2(t, 1f));
                }
                foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                    pass.Apply();
                    device.DrawUserPrimitives(PrimitiveType.TriangleStrip, strip, 0, count * 2 - 2);
                }
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }
    }
}
