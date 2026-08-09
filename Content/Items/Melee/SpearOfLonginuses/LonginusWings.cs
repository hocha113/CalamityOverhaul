using CalamityOverhaul.Content.UIs.UIEffect;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Items.Melee.SpearOfLonginuses
{
    /// <summary>
    /// 光之翼几何与绘制：SVG 翼骨弧长重采样，收拢/呼吸变换后铺条带，
    /// 由 <see cref="LonginusWingsRender"/> 在玩家身后图层调度
    /// </summary>
    internal static class LonginusWings
    {
        public const int FeatherCount = 5;
        private const int Samples = 20;

        /// <summary>展开姿态翼骨，归一空间根在原点，+X 外展 -Y 上扬，每羽一条子路径</summary>
        private const string WingPathData =
            "M0 0 C0.22 -0.30 0.55 -0.60 0.90 -0.80" +
            "M0 0 C0.28 -0.18 0.66 -0.36 1.02 -0.46" +
            "M0 0 C0.34 -0.06 0.74 -0.12 1.06 -0.13" +
            "M0 0 C0.32 0.05 0.68 0.07 0.94 0.15" +
            "M0 0 C0.26 0.11 0.50 0.19 0.70 0.31";

        private static Vector2[][] feathers;
        private static readonly Vector2[] pointBuf = new Vector2[Samples];
        private static readonly VertexPositionColorTexture[] stripBuf = new VertexPositionColorTexture[Samples * 2];

        private static void EnsureFeathers() {
            if (feathers != null) {
                return;
            }
            SvgPath path = SvgPathPen.Path(WingPathData);
            if (path == null || path.Lines.Length < FeatherCount) {
                feathers = [];
                return;
            }
            feathers = new Vector2[FeatherCount][];
            for (int f = 0; f < FeatherCount; f++) {
                feathers[f] = Resample(path.Lines[f], path.Arcs[f], Samples);
            }
        }

        /// <summary>按累计弧长把折线均匀重采样成 n 点</summary>
        private static Vector2[] Resample(Vector2[] pts, float[] arcs, int n) {
            Vector2[] result = new Vector2[n];
            float total = arcs[^1];
            int seg = 1;
            for (int i = 0; i < n; i++) {
                float target = total * i / (n - 1);
                while (seg < arcs.Length - 1 && arcs[seg] < target) {
                    seg++;
                }
                float span = arcs[seg] - arcs[seg - 1];
                float k = span > 0 ? (target - arcs[seg - 1]) / span : 0f;
                result[i] = Vector2.Lerp(pts[seg - 1], pts[seg], k);
            }
            return result;
        }

        /// <summary>单羽错帧展开度，外侧长羽先开</summary>
        public static float FeatherOpen(float openT, int feather)
            => MathHelper.Clamp(openT * 1.55f - feather * 0.11f, 0f, 1f);

        /// <summary>绘制一名玩家的双侧光之翼，wingOpen 0~1 总展开度</summary>
        public static void Draw(Player owner, float wingOpen, float span, float alphaMul) {
            if (wingOpen <= 0.01f || alphaMul <= 0.01f) {
                return;
            }
            EnsureFeathers();
            if (feathers.Length < FeatherCount) {
                return;
            }
            Effect effect = LonginusAssets.LonginusWing?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null) {
                return;
            }

            float gravDir = owner.gravDir;
            Vector2 anchor = owner.MountedCenter + new Vector2(0, -6f * gravDir);

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.Additive;
            device.RasterizerState = RasterizerState.CullNone;

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uNoiseTex"]?.SetValue(noise);
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uHot"]?.SetValue(0.2f + wingOpen * 0.2f);

            for (int side = -1; side <= 1; side += 2) {
                for (int f = 0; f < FeatherCount; f++) {
                    float fOpen = FeatherOpen(wingOpen, f);
                    if (fOpen <= 0.01f) {
                        continue;
                    }
                    //展开带回弹过冲，收拢角随之反向微甩
                    float eased = VaultUtils.EaseOutBack(fOpen);
                    BuildFeather(f, side, anchor, span, eased, gravDir, alphaMul);

                    effect.Parameters["uOpen"]?.SetValue(fOpen * 1.08f);
                    effect.Parameters["uPhase"]?.SetValue(f * 0.37f + (side > 0 ? 0.5f : 0f));
                    foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                        pass.Apply();
                        device.DrawUserPrimitives(PrimitiveType.TriangleStrip, stripBuf, 0, Samples * 2 - 2);
                    }
                }
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }

        private static void BuildFeather(int f, int side, Vector2 anchor, float span
            , float eased, float gravDir, float alphaMul) {
            Vector2[] bone = feathers[f];

            //收拢角：未展开时压向下后成束；呼吸小摆随羽错相
            float collapse = (1f - eased) * (1.25f - f * 0.10f);
            float breathe = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 1.3f + f * 0.7f) * 0.035f * eased;
            float rot = collapse + breathe;
            float cs = (float)Math.Cos(rot);
            float sn = (float)Math.Sin(rot);

            for (int i = 0; i < Samples; i++) {
                Vector2 p = bone[i];
                Vector2 r = new(p.X * cs - p.Y * sn, p.X * sn + p.Y * cs);
                pointBuf[i] = anchor + new Vector2(r.X * side, r.Y * gravDir) * span;
            }

            //羽色：长羽偏白短羽偏金
            Color tint = Color.Lerp(new Color(255, 240, 210), new Color(255, 200, 110), f / (FeatherCount - 1f));
            Color strand = tint * (alphaMul * (0.92f - f * 0.05f));

            for (int i = 0; i < Samples; i++) {
                float t = i / (Samples - 1f);
                Vector2 tangent = (pointBuf[Math.Min(i + 1, Samples - 1)] - pointBuf[Math.Max(i - 1, 0)]).UnitVector();
                if (tangent == Vector2.Zero) {
                    tangent = Vector2.UnitX;
                }
                Vector2 perp = tangent.RotatedBy(MathHelper.PiOver2);
                float halfW = span * (0.052f - f * 0.004f) * (1f - t * 0.55f);

                stripBuf[i * 2] = new(new Vector3(pointBuf[i].X + perp.X * halfW, pointBuf[i].Y + perp.Y * halfW, 0f)
                    , strand, new Vector2(t, 0f));
                stripBuf[i * 2 + 1] = new(new Vector3(pointBuf[i].X - perp.X * halfW, pointBuf[i].Y - perp.Y * halfW, 0f)
                    , strand, new Vector2(t, 1f));
            }
        }
    }
}
