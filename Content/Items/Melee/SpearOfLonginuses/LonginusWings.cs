using CalamityOverhaul.Content.UIs.UIEffect;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Items.Melee.SpearOfLonginuses
{
    /// <summary>
    /// 光之翼几何与绘制（二稿）：SVG 翼骨弧长重采样 + 预计算法线，
    /// 展开拆成窜长/扇开两段，常驻态骨架行波起伏（极光式），
    /// 由 <see cref="LonginusWingsRender"/> 在玩家身后图层调度
    /// </summary>
    internal static class LonginusWings
    {
        public const int FeatherCount = 6;
        private const int Samples = 22;

        /// <summary>展开姿态翼骨，归一空间根在原点，+X 外展 -Y 上扬，每羽一条子路径，上长下短</summary>
        private const string WingPathData =
            "M0 0 C0.10 -0.42 0.42 -0.78 0.86 -0.94" +
            "M0 0 C0.18 -0.30 0.55 -0.60 0.98 -0.66" +
            "M0 0 C0.24 -0.18 0.62 -0.36 1.06 -0.38" +
            "M0 0 C0.28 -0.06 0.66 -0.14 1.08 -0.12" +
            "M0 0 C0.26 0.05 0.62 0.04 1.00 0.10" +
            "M0 0 C0.22 0.13 0.48 0.22 0.74 0.32";

        private static Vector2[][] feathers;
        private static Vector2[][] boneNormals;
        private static readonly Vector2[] pointBuf = new Vector2[Samples];
        private static readonly VertexPositionColorTexture[] stripBuf = new VertexPositionColorTexture[Samples * 2];

        private static void EnsureFeathers() {
            if (feathers != null) {
                return;
            }
            SvgPath path = SvgPathPen.Path(WingPathData);
            if (path == null || path.Lines.Length < FeatherCount) {
                feathers = [];
                boneNormals = [];
                return;
            }
            feathers = new Vector2[FeatherCount][];
            boneNormals = new Vector2[FeatherCount][];
            for (int f = 0; f < FeatherCount; f++) {
                Vector2[] bone = Resample(path.Lines[f], path.Arcs[f], Samples);
                Vector2[] normal = new Vector2[Samples];
                for (int i = 0; i < Samples; i++) {
                    Vector2 tangent = (bone[Math.Min(i + 1, Samples - 1)] - bone[Math.Max(i - 1, 0)]).UnitVector();
                    if (tangent == Vector2.Zero) {
                        tangent = Vector2.UnitX;
                    }
                    normal[i] = tangent.RotatedBy(MathHelper.PiOver2);
                }
                feathers[f] = bone;
                boneNormals[f] = normal;
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

        /// <summary>单羽错帧展开度，上方长羽先开</summary>
        public static float FeatherOpen(float openT, int feather)
            => MathHelper.Clamp(openT * 1.42f - feather * 0.07f, 0f, 1f);

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
            Vector2 anchor = owner.MountedCenter + new Vector2(0, -8f * gravDir);

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.Additive;
            device.RasterizerState = RasterizerState.CullNone;

            //常驻微闪，克制的呼吸热度
            float shimmer = 0.28f + 0.16f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 1.7f);
            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uNoiseTex"]?.SetValue(noise);
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uHot"]?.SetValue(shimmer + wingOpen * 0.2f);

            for (int side = -1; side <= 1; side += 2) {
                float sidePhase = side > 0 ? 0f : 1.19f;
                for (int f = 0; f < FeatherCount; f++) {
                    float fOpen = FeatherOpen(wingOpen, f);
                    if (fOpen <= 0.01f) {
                        continue;
                    }
                    //两段式：先向下束状窜长，再扇形甩开带过冲回弹
                    float lengthT = MathHelper.Clamp(fOpen / 0.45f, 0f, 1f);
                    float sweepT = MathHelper.Clamp((fOpen - 0.26f) / 0.74f, 0f, 1f);
                    float eased = VaultUtils.EaseOutBack(sweepT);

                    BuildFeather(f, side, anchor, span, eased, fOpen, sidePhase, gravDir, alphaMul);

                    effect.Parameters["uOpen"]?.SetValue(lengthT * 1.06f);
                    effect.Parameters["uPhase"]?.SetValue(f * 0.41f + (side > 0 ? 0.5f : 0f));
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
            , float eased, float fOpen, float sidePhase, float gravDir, float alphaMul) {
            Vector2[] bone = feathers[f];
            Vector2[] normal = boneNormals[f];

            //收拢角：sweep 未完成时整束压向背后下方；过冲时(eased>1)反向微甩
            float collapse = (1f - eased) * (2.55f - f * 0.26f);
            //整羽慢摆，行波之外的低频体感
            float sway = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 0.9f + f * 0.55f + sidePhase) * 0.022f * eased;
            float rot = collapse + sway;
            float cs = (float)Math.Cos(rot);
            float sn = (float)Math.Sin(rot);

            //极光行波：根部固定端部甩动，从爆心向端部传播，两侧相位不同步
            float wavePhase = f * 1.13f + sidePhase;
            float waveAmp = 0.042f * fOpen;

            for (int i = 0; i < Samples; i++) {
                float t = i / (Samples - 1f);
                float wob = (float)Math.Sin(t * 7.6f - Main.GlobalTimeWrappedHourly * 3.1f + wavePhase)
                    * waveAmp * t * (float)Math.Sqrt(t);
                Vector2 p = bone[i] + normal[i] * wob;
                Vector2 r = new(p.X * cs - p.Y * sn, p.X * sn + p.Y * cs);
                pointBuf[i] = anchor + new Vector2(r.X * side, r.Y * gravDir) * span;
            }

            //羽色：上方长羽偏白，下方短羽偏金
            Color tint = Color.Lerp(new Color(255, 246, 225), new Color(255, 196, 96), f / (FeatherCount - 1f));
            Color strand = tint * (alphaMul * (0.85f - f * 0.035f));

            //镜像翻转前缘符号，保证 uv.y=0 白热锐边在两侧翅膀都朝上
            float leadSign = -side * gravDir;

            for (int i = 0; i < Samples; i++) {
                float t = i / (Samples - 1f);
                Vector2 tangent = (pointBuf[Math.Min(i + 1, Samples - 1)] - pointBuf[Math.Max(i - 1, 0)]).UnitVector();
                if (tangent == Vector2.Zero) {
                    tangent = Vector2.UnitX;
                }
                Vector2 perp = tangent.RotatedBy(MathHelper.PiOver2) * leadSign;
                float halfW = span * (0.075f - f * 0.006f) * (1f - t * 0.62f);

                stripBuf[i * 2] = new(new Vector3(pointBuf[i].X + perp.X * halfW, pointBuf[i].Y + perp.Y * halfW, 0f)
                    , strand, new Vector2(t, 0f));
                stripBuf[i * 2 + 1] = new(new Vector3(pointBuf[i].X - perp.X * halfW, pointBuf[i].Y - perp.Y * halfW, 0f)
                    , strand, new Vector2(t, 1f));
            }
        }
    }
}
