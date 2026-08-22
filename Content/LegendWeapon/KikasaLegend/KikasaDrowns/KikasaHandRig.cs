using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDrowns
{
    /// <summary>
    /// 血湖鬼手单臂装备：水面根固定，6 节 FABRIK 追腕，卷指与三种条带装配。
    /// 骨架沿用焦黑枯手（GhostHandProj）的解算与条带口径，
    /// 宽度包络改水柱形（根粗腕细）、骨瘤弱化、爪尖圆润，是抱不是撕。
    /// </summary>
    internal sealed class KikasaHandRig
    {
        public const int ArmSegmentCount = 6;

        //条带 uv.x 段位（与 KikasaHand.fx 对齐）：根→腕 0~0.70，掌 0.70~0.84，指爪 0.84~1.0
        private const float ArmUMax = 0.70f;
        private const float PalmUMax = 0.84f;

        //手指表（沿用焦手口径，装配时按 Scale 缩放）
        private static readonly float[] FingerSpread = [-0.72f, -0.36f, 0f, 0.36f, 0.72f];
        private static readonly float[] KnuckleOffsets = [-13f, -6.5f, 0f, 6.5f, 13f];
        private static readonly float[] FingerLengths = [30f, 40f, 46f, 40f, 32f];
        private static readonly float[] FingerSegFractions = [0.40f, 0.32f, 0.28f];

        /// <summary>水面根，整场固定</summary>
        public Vector2 Root;

        /// <summary>腕位，逐帧由编舞驱动</summary>
        public Vector2 Wrist;

        /// <summary>动态段长：按根到抓点距离定标，拖入期收缩保持绷直</summary>
        public float SegmentLength = 40f;

        /// <summary>弯曲度：够抓 0.7 → 绷紧 0.08，曲率变化就是力量可视化</summary>
        public float Tension = 0.7f;

        /// <summary>肘外拐方向 ±1（由根在目标哪一侧决定，臂间不交叉）</summary>
        public int BendDir = 1;

        /// <summary>卷指 -0.2 张开 → 1.05 攥拢</summary>
        public float Curl;

        public float Opacity;
        public float Drain;
        /// <summary>根部泡沫活性（出水/入水最烈），传 shader</summary>
        public float Foam;
        /// <summary>绷紧参数（传 shader 的 uGrip）</summary>
        public float Grip;
        public float Seed;

        /// <summary>true 画在鬼影之前（越顶/侧箍），false 画在鬼影之后（托底/背箍）</summary>
        public bool FrontLayer = true;

        /// <summary>整体缩放（随目标体型），影响掌指尺寸与条带宽度</summary>
        public float Scale = 1f;

        private readonly Vector2[] armSegments = new Vector2[ArmSegmentCount];
        private Vector2 knuckleCenter;
        private readonly Vector2[,] fingerJoints = new Vector2[5, 5];

        public Vector2 WristSolved => armSegments[0];

        //==================== 解算 ====================

        /// <summary>FABRIK：前向腕→根 + 反向根→腕，弯曲垂线随 Tension；随后掌指前向解算</summary>
        public void Solve() {
            Vector2 handPos = Wrist;
            float maxReach = ArmSegmentCount * SegmentLength * 0.98f;
            float dist = Vector2.Distance(Root, handPos);
            if (dist > maxReach) {
                handPos = Root + (handPos - Root).SafeNormalize(Vector2.Zero) * maxReach;
            }

            float bendAmp = SegmentLength * 0.32f;

            armSegments[0] = handPos;
            for (int i = 1; i < ArmSegmentCount; i++) {
                Vector2 direction = (armSegments[i - 1]
                    - (i == ArmSegmentCount - 1 ? Root : armSegments[i])).SafeNormalize(Vector2.Zero);
                float bend = MathF.Sin(i / (float)ArmSegmentCount * MathHelper.Pi) * Tension;
                Vector2 perpendicular = new Vector2(-direction.Y, direction.X) * bend * bendAmp * BendDir;
                armSegments[i] = armSegments[i - 1] - direction * SegmentLength + perpendicular;
            }
            armSegments[ArmSegmentCount - 1] = Root;
            for (int i = ArmSegmentCount - 2; i >= 0; i--) {
                Vector2 direction = (armSegments[i] - armSegments[i + 1]).SafeNormalize(Vector2.Zero);
                float bend = MathF.Sin(i / (float)ArmSegmentCount * MathHelper.Pi) * Tension;
                Vector2 perpendicular = new Vector2(-direction.Y, direction.X) * bend * bendAmp * BendDir;
                armSegments[i] = armSegments[i + 1] + direction * SegmentLength + perpendicular;
            }

            UpdateFingers();
        }

        private float JointHash(int k, int j) {
            float h = MathF.Sin(Seed * 7.31f + k * 13.7f + j * 5.3f) * 43758.547f;
            return h - MathF.Floor(h);
        }

        /// <summary>腕 → 掌根线 → 三节骨 → 爪尖；卷指角随 Curl，骨节带种子化歪扭</summary>
        private void UpdateFingers() {
            Vector2 handDir = (armSegments[0] - armSegments[1]).SafeNormalize(Vector2.UnitX);
            float handAng = handDir.ToRotation();
            Vector2 perp = new(-handDir.Y, handDir.X);
            float palmLength = 20f * Scale;
            knuckleCenter = armSegments[0] + handDir * palmLength;

            for (int k = 0; k < 5; k++) {
                float curl = MathHelper.Clamp(Curl, -0.2f, 1.05f);
                float lenScale = (1f + (JointHash(k, 9) - 0.5f) * 0.14f) * Scale;
                float total = FingerLengths[k] * lenScale;
                float spread = FingerSpread[k] * (1f - curl * 0.42f);
                float bendSign = FingerSpread[k] == 0f ? -0.55f : -MathF.Sign(FingerSpread[k]);
                float ang = handAng + spread;

                Vector2 p = knuckleCenter + perp * (KnuckleOffsets[k] * Scale * (1f - curl * 0.22f));
                fingerJoints[k, 0] = p;
                for (int j = 0; j < 3; j++) {
                    ang += (JointHash(k, j) - 0.5f) * 0.28f + bendSign * curl * (0.42f + j * 0.34f);
                    p += ang.ToRotationVector2() * (total * FingerSegFractions[j]);
                    fingerJoints[k, j + 1] = p;
                }
                //爪尖：圆钝短钩，是抱不是撕
                float clawAng = ang + bendSign * (0.34f + curl * 0.45f);
                fingerJoints[k, 4] = p + clawAng.ToRotationVector2() * (5f + total * 0.09f);
            }
        }

        //==================== 条带装配 ====================

        private static float KnuckleBump(float x) => MathF.Exp(-x * x / 0.004f);

        /// <summary>臂条带：水柱形宽度（根粗腕细）+ 缓慢水面波动；根(u=0)→腕(u=0.70)</summary>
        public VertexPositionColorTexture[] BuildArmStrip() {
            const int sampleCount = 26;
            Span<Vector2> raw = stackalloc Vector2[sampleCount];
            Span<Vector2> pts = stackalloc Vector2[sampleCount];
            for (int i = 0; i < sampleCount; i++) {
                float t = i / (float)(sampleCount - 1);
                float ft = (1f - t) * (ArmSegmentCount - 1);
                int i0 = (int)ft;
                int i1 = Math.Min(i0 + 1, ArmSegmentCount - 1);
                float frac = ft - i0;
                Vector2 p0 = armSegments[Math.Max(i0 - 1, 0)];
                Vector2 p1 = armSegments[i0];
                Vector2 p2 = armSegments[i1];
                Vector2 p3 = armSegments[Math.Min(i1 + 1, ArmSegmentCount - 1)];
                raw[i] = Vector2.CatmullRom(p0, p1, p2, p3, frac);
            }
            //水面波动：两端固定，中段小幅快波（比焦手的扭结更细更快，是水不是枯肉）
            pts[0] = raw[0];
            pts[sampleCount - 1] = raw[sampleCount - 1];
            for (int i = 1; i < sampleCount - 1; i++) {
                float t = i / (float)(sampleCount - 1);
                Vector2 tangent = (raw[i + 1] - raw[i - 1]).SafeNormalize(Vector2.UnitX);
                Vector2 normal = new(-tangent.Y, tangent.X);
                float wave = MathF.Sin(t * 11.0f + Seed * 23f + Main.GlobalTimeWrappedHourly * 1.4f)
                    * 2.6f * MathF.Sin(t * MathHelper.Pi);
                pts[i] = raw[i] + normal * wave;
            }

            float tighten = 1f - Grip * 0.10f;
            var verts = new VertexPositionColorTexture[sampleCount * 2];
            for (int i = 0; i < sampleCount; i++) {
                float t = i / (float)(sampleCount - 1);
                Vector2 tangent = i < sampleCount - 1
                    ? (pts[i + 1] - pts[i]).SafeNormalize(Vector2.UnitX)
                    : (pts[i] - pts[i - 1]).SafeNormalize(Vector2.UnitX);
                Vector2 normal = new(-tangent.Y, tangent.X);

                //水柱形：根粗腕细，中段一点饱满，无骨瘤
                float width = (MathHelper.Lerp(14f, 8f, t)
                    + MathF.Sin(t * MathHelper.Pi) * 2.2f) * tighten * Scale;
                //下侧微宽：水往下坠
                float downDot = Vector2.Dot(Vector2.UnitY, normal);
                float w0 = width * (1f + 0.14f * downDot);
                float w1 = width * (1f - 0.14f * downDot);
                Color vCenter = new(w0 / (w0 + w1), 0f, 0f);

                float u = t * ArmUMax;
                verts[i * 2] = new VertexPositionColorTexture((pts[i] + normal * w0).ToVector3(),
                    vCenter, new Vector2(u, 0f));
                verts[i * 2 + 1] = new VertexPositionColorTexture((pts[i] - normal * w1).ToVector3(),
                    vCenter, new Vector2(u, 1f));
            }
            return verts;
        }

        /// <summary>掌条带：腕(0.70)→掌根线(0.84)，腕口收窄向指根线展开</summary>
        public VertexPositionColorTexture[] BuildPalmStrip() {
            const int sampleCount = 6;
            Vector2 root = armSegments[0];
            Vector2 axis = knuckleCenter - root;
            Vector2 dir = axis.SafeNormalize(Vector2.UnitX);
            Vector2 normal = new(-dir.Y, dir.X);
            var verts = new VertexPositionColorTexture[sampleCount * 2];
            Color vCenter = new(0.5f, 0.35f, 0f);
            for (int i = 0; i < sampleCount; i++) {
                float t = i / (float)(sampleCount - 1);
                Vector2 p = root + axis * t;
                float half = MathHelper.Lerp(7.5f, 13f, t) * (1f - Grip * 0.08f) * Scale;
                float u = ArmUMax + t * (PalmUMax - ArmUMax);
                verts[i * 2] = new VertexPositionColorTexture((p + normal * half).ToVector3(),
                    vCenter, new Vector2(u, 0f));
                verts[i * 2 + 1] = new VertexPositionColorTexture((p - normal * half).ToVector3(),
                    vCenter, new Vector2(u, 1f));
            }
            return verts;
        }

        /// <summary>单指条带：掌根(0.84)→爪尖(1.0)，指节隆起弱化、末端圆钝</summary>
        public VertexPositionColorTexture[] BuildFingerStrip(int k) {
            const int sampleCount = 11;
            Span<Vector2> pts = stackalloc Vector2[sampleCount];
            for (int i = 0; i < sampleCount; i++) {
                float t = i / (float)(sampleCount - 1);
                float ft = t * 4f;
                int i0 = Math.Min((int)ft, 3);
                float frac = ft - i0;
                Vector2 p0 = fingerJoints[k, Math.Max(i0 - 1, 0)];
                Vector2 p1 = fingerJoints[k, i0];
                Vector2 p2 = fingerJoints[k, i0 + 1];
                Vector2 p3 = fingerJoints[k, Math.Min(i0 + 2, 4)];
                pts[i] = Vector2.CatmullRom(p0, p1, p2, p3, frac);
            }

            var verts = new VertexPositionColorTexture[sampleCount * 2];
            for (int i = 0; i < sampleCount; i++) {
                float t = i / (float)(sampleCount - 1);
                Vector2 tangent = i < sampleCount - 1
                    ? (pts[i + 1] - pts[i]).SafeNormalize(Vector2.UnitX)
                    : (pts[i] - pts[i - 1]).SafeNormalize(Vector2.UnitX);
                Vector2 normal = new(-tangent.Y, tangent.X);

                float width;
                if (t < 0.75f) {
                    float bump = KnuckleBump(t - 0.25f) + KnuckleBump(t - 0.5f) + KnuckleBump(t - 0.75f);
                    width = MathHelper.Lerp(5.2f, 3.0f, t / 0.75f) * (1f + bump * 0.22f);
                }
                else {
                    width = MathHelper.Lerp(2.9f, 0.9f, (t - 0.75f) / 0.25f);
                }
                width *= Scale;

                Color vCol = new(0.5f, MathHelper.Clamp((t - 0.70f) / 0.16f, 0f, 1f), 0f);
                float u = PalmUMax + t * (1f - PalmUMax);
                verts[i * 2] = new VertexPositionColorTexture((pts[i] + normal * width).ToVector3(),
                    vCol, new Vector2(u, 0f));
                verts[i * 2 + 1] = new VertexPositionColorTexture((pts[i] - normal * width).ToVector3(),
                    vCol, new Vector2(u, 1f));
            }
            return verts;
        }

        //==================== CPU 回退 ====================

        /// <summary>无着色器时的线链回退：臂骨折线+五指细线，调用方持有世界矩阵批次</summary>
        public void DrawFallback(SpriteBatch sb, Texture2D pixel) {
            if (Opacity <= 0.02f) {
                return;
            }
            //鬼雨异化时随观看域冷化为浊水灰青
            Color arm = KikasaDomain.CoolTint(new(96, 22, 26), new(38, 52, 58)) * Opacity;
            Color film = KikasaDomain.CoolTint(new(246, 133, 112), new(176, 200, 204)) * (0.5f * Opacity);
            for (int i = 0; i < ArmSegmentCount - 1; i++) {
                DrawLine(sb, pixel, armSegments[i + 1], armSegments[i], 7f * Scale, arm);
            }
            for (int k = 0; k < 5; k++) {
                for (int j = 0; j < 4; j++) {
                    DrawLine(sb, pixel, fingerJoints[k, j], fingerJoints[k, j + 1], 2.4f * Scale, film);
                }
            }
        }

        private static void DrawLine(SpriteBatch sb, Texture2D pixel, Vector2 a, Vector2 b, float width, Color color) {
            Vector2 d = b - a;
            float len = d.Length();
            if (len < 0.5f) {
                return;
            }
            sb.Draw(pixel, a - Main.screenPosition, new Rectangle(0, 0, 1, 1), color,
                MathF.Atan2(d.Y, d.X), new Vector2(0f, 0.5f),
                new Vector2(len, width), SpriteEffects.None, 0f);
        }
    }
}
