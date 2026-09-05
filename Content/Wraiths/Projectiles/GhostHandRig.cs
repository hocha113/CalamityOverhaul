using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Wraiths.Projectiles
{
    /// <summary>
    /// 焦黑枯手的骨架与条带构建，供 <see cref="GhostHandProj"/> 与夺身处决演出共用。<br/>
    /// 六节 FABRIK 臂 + 掌 + 五指（三节骨 + 爪尖），条带 uv.x 分段：
    /// 肩到腕 0~0.70、掌 0.70~0.84、指爪 0.84~1.0，交给 <c>GhostHandSheath.fx</c> 换质。<br/>
    /// 所有像素量都乘 <see cref="Scale"/>，处决演出据此长成巨手而不必另写一套画法。
    /// </summary>
    internal sealed class GhostHandRig
    {
        internal const int ArmSegmentCount = 6;
        internal const int FingerCount = 5;

        private const float BaseSegmentLength = 52f;
        private const float BasePalmLength = 24f;
        private static readonly float[] FingerSpread = [-0.72f, -0.36f, 0f, 0.36f, 0.72f];
        private static readonly float[] KnuckleOffsets = [-13f, -6.5f, 0f, 6.5f, 13f];
        private static readonly float[] FingerLengths = [30f, 40f, 46f, 40f, 32f];
        private static readonly float[] FingerSegFractions = [0.40f, 0.32f, 0.28f];

        /// <summary>条带 uv.x 段位：肩→腕 0~0.70，掌 0.70~0.84，指爪 0.84~1.0</summary>
        private const float ArmUMax = 0.70f;
        private const float PalmUMax = 0.84f;

        private readonly Vector2[] armSegments = new Vector2[ArmSegmentCount];
        //[指, 关节] 0=指根 1..3=骨节 4=爪尖
        private readonly Vector2[,] fingerJoints = new Vector2[FingerCount, 5];
        private Vector2 knuckleCenter;
        private float scale = 1f;

        /// <summary>骨节歪扭的确定性种子源，各端一致</summary>
        internal float Identity { get; set; }

        internal float Scale {
            get => scale;
            set => scale = MathF.Max(value, 0.05f);
        }

        internal float SegmentLength => BaseSegmentLength * scale;
        internal float MaxReach => ArmSegmentCount * SegmentLength;
        internal float PalmLength => BasePalmLength * scale;

        internal Vector2 Wrist => armSegments[0];
        internal Vector2 Shoulder => armSegments[ArmSegmentCount - 1];
        internal Vector2 KnuckleCenter => knuckleCenter;
        internal Vector2 Joint(int segment) => armSegments[Math.Clamp(segment, 0, ArmSegmentCount - 1)];
        internal Vector2 FingerTip(int finger) => fingerJoints[Math.Clamp(finger, 0, FingerCount - 1), 4];

        /// <summary>把整条臂摊成肩到手的直线，用于出生或重钉，避免先立正再甩过去。</summary>
        internal void Snap(Vector2 hand, Vector2 shoulder) {
            for (int i = 0; i < ArmSegmentCount; i++) {
                armSegments[i] = Vector2.Lerp(hand, shoulder, i / (float)(ArmSegmentCount - 1));
            }
        }

        /// <summary>
        /// FABRIK 双向解算，返回夹到臂展内的手位（调用方据此回写自己的位置）。<br/>
        /// <paramref name="tension"/> 越高臂越绷直，越低越松垮弓出。
        /// </summary>
        internal Vector2 SolveArm(Vector2 shoulder, Vector2 hand, float tension, int direction) {
            float reach = MaxReach;
            if (Vector2.Distance(shoulder, hand) > reach * 0.98f) {
                hand = shoulder + (hand - shoulder).SafeNormalize(Vector2.Zero) * (reach * 0.98f);
            }

            float segLen = SegmentLength;
            //前向：手→肩
            armSegments[0] = hand;
            for (int i = 1; i < ArmSegmentCount; i++) {
                Vector2 direction2 = (armSegments[i - 1]
                    - (i == ArmSegmentCount - 1 ? shoulder : armSegments[i])).SafeNormalize(Vector2.Zero);
                float bendFactor = MathF.Sin(i / (float)ArmSegmentCount * MathHelper.Pi) * tension;
                Vector2 perpendicular = new Vector2(-direction2.Y, direction2.X)
                    * bendFactor * 16f * scale * direction;
                armSegments[i] = armSegments[i - 1] - direction2 * segLen + perpendicular;
            }
            //反向：肩→手
            armSegments[ArmSegmentCount - 1] = shoulder;
            for (int i = ArmSegmentCount - 2; i >= 0; i--) {
                Vector2 direction2 = (armSegments[i] - armSegments[i + 1]).SafeNormalize(Vector2.Zero);
                float bendFactor = MathF.Sin(i / (float)ArmSegmentCount * MathHelper.Pi) * tension;
                Vector2 perpendicular = new Vector2(-direction2.Y, direction2.X)
                    * bendFactor * 16f * scale * direction;
                armSegments[i] = armSegments[i + 1] + direction2 * segLen + perpendicular;
            }
            return armSegments[0];
        }

        /// <summary>
        /// 掌指前向解算：腕 → 掌根线 → 三节骨 → 爪尖。<br/>
        /// 每指长度不同、骨节带种子化歪扭，包拢角随各指有效包拢度。
        /// </summary>
        internal void SolveFingers(float curl, ReadOnlySpan<float> curlOffsets) {
            Vector2 handDir = (armSegments[0] - armSegments[1]).SafeNormalize(Vector2.UnitX);
            float handAng = handDir.ToRotation();
            Vector2 perp = new(-handDir.Y, handDir.X);
            knuckleCenter = armSegments[0] + handDir * PalmLength;

            for (int k = 0; k < FingerCount; k++) {
                float offset = k < curlOffsets.Length ? curlOffsets[k] : 0f;
                float fingerCurl = MathHelper.Clamp(curl + offset, -0.2f, 1.05f);
                float lenScale = 1f + (JointHash(k, 9) - 0.5f) * 0.16f;
                float total = FingerLengths[k] * lenScale * scale;
                float spread = FingerSpread[k] * (1f - fingerCurl * 0.42f);
                float bendSign = FingerSpread[k] == 0f ? -0.55f : -MathF.Sign(FingerSpread[k]);
                float ang = handAng + spread;

                Vector2 p = knuckleCenter
                    + perp * (KnuckleOffsets[k] * scale * (1f - fingerCurl * 0.22f));
                fingerJoints[k, 0] = p;
                for (int j = 0; j < 3; j++) {
                    ang += (JointHash(k, j) - 0.5f) * 0.36f
                        + bendSign * fingerCurl * (0.42f + j * 0.34f);
                    p += ang.ToRotationVector2() * (total * FingerSegFractions[j]);
                    fingerJoints[k, j + 1] = p;
                }
                //爪尖：额外硬弯的尖钩
                float clawAng = ang + bendSign * (0.42f + fingerCurl * 0.55f);
                fingerJoints[k, 4] = p + clawAng.ToRotationVector2() * (8f * scale + total * 0.12f);
            }
        }

        /// <summary>骨节僵直歪扭的确定性种子，客户端间一致</summary>
        private float JointHash(int k, int j) {
            float h = MathF.Sin(Identity * 7.31f + k * 13.7f + j * 5.3f) * 43758.547f;
            return h - MathF.Floor(h);
        }

        /// <summary>指节隆起包络</summary>
        private static float KnuckleBump(float x) => MathF.Exp(-x * x / 0.004f);

        /// <summary>
        /// 六节 IK 曲线 Catmull-Rom 加宽条带，肩(u=0)→腕(u=0.70)。<br/>
        /// 中线低频扭结蠕动、种子化骨瘤与肘部隆块；上侧加宽让烟向上散；臂中线 v 进顶点色 R。
        /// </summary>
        internal VertexPositionColorTexture[] BuildArmStrip(float gripBlend, float time) {
            const int sampleCount = 26;
            Span<Vector2> raw = stackalloc Vector2[sampleCount];
            Span<Vector2> pts = stackalloc Vector2[sampleCount];
            for (int i = 0; i < sampleCount; i++) {
                float t = i / (float)(sampleCount - 1);
                //armSegments[0]=手 ... [^1]=肩；条带 0=肩 → 1=腕
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
            //扭结蠕动：两端固定，中段缓慢蠕行
            pts[0] = raw[0];
            pts[sampleCount - 1] = raw[sampleCount - 1];
            for (int i = 1; i < sampleCount - 1; i++) {
                float t = i / (float)(sampleCount - 1);
                Vector2 tangent = (raw[i + 1] - raw[i - 1]).SafeNormalize(Vector2.UnitX);
                Vector2 normal = new(-tangent.Y, tangent.X);
                float gnarl = MathF.Sin(t * 8.6f + Identity * 29f + time * 0.4f)
                    * 4.5f * scale * MathF.Sin(t * MathHelper.Pi);
                pts[i] = raw[i] + normal * gnarl;
            }

            //宽度包络：肩根细 → 中段饱满带骨瘤 → 腕略收；攥握整体收紧
            float tighten = 1f - gripBlend * 0.16f;
            const float upBias = 0.30f;
            var verts = new VertexPositionColorTexture[sampleCount * 2];
            for (int i = 0; i < sampleCount; i++) {
                float t = i / (float)(sampleCount - 1);
                Vector2 tangent = i < sampleCount - 1
                    ? (pts[i + 1] - pts[i]).SafeNormalize(Vector2.UnitX)
                    : (pts[i] - pts[i - 1]).SafeNormalize(Vector2.UnitX);
                Vector2 normal = new(-tangent.Y, tangent.X);

                float knot = MathF.Sin(t * 21f + Identity * 37f) * 0.5f + 0.5f;
                float elbow = MathF.Exp(-(t - 0.46f) * (t - 0.46f) / 0.006f);
                float width = (MathHelper.Lerp(10f, 9f, t)
                    + MathF.Sin(t * MathHelper.Pi) * 5.5f
                    + knot * knot * 3.2f + elbow * 2.5f) * scale * tighten;
                float upDot = Vector2.Dot(-Vector2.UnitY, normal);
                float w0 = width * (1f + upBias * upDot);
                float w1 = width * (1f - upBias * upDot);
                Color vCenter = new(w0 / (w0 + w1), 0f, 0f);

                float u = t * ArmUMax;
                verts[i * 2] = new VertexPositionColorTexture((pts[i] + normal * w0).ToVector3()
                    , vCenter, new Vector2(u, 0f));
                verts[i * 2 + 1] = new VertexPositionColorTexture((pts[i] - normal * w1).ToVector3()
                    , vCenter, new Vector2(u, 1f));
            }
            return verts;
        }

        /// <summary>手掌条带，腕(u=0.70)→掌根线(u=0.84)：腕口收窄向指根线展开成掌</summary>
        internal VertexPositionColorTexture[] BuildPalmStrip(float gripBlend) {
            const int sampleCount = 6;
            Vector2 root = armSegments[0];
            Vector2 axis = knuckleCenter - root;
            Vector2 dir = axis.SafeNormalize(Vector2.UnitX);
            Vector2 normal = new(-dir.Y, dir.X);
            var verts = new VertexPositionColorTexture[sampleCount * 2];
            Color vCenter = new(0.5f, 0f, 0f);
            for (int i = 0; i < sampleCount; i++) {
                float t = i / (float)(sampleCount - 1);
                Vector2 p = root + axis * t;
                float half = MathHelper.Lerp(9f, 16f, t) * scale * (1f - gripBlend * 0.10f);
                float u = ArmUMax + t * (PalmUMax - ArmUMax);
                verts[i * 2] = new VertexPositionColorTexture((p + normal * half).ToVector3()
                    , vCenter, new Vector2(u, 0f));
                verts[i * 2 + 1] = new VertexPositionColorTexture((p - normal * half).ToVector3()
                    , vCenter, new Vector2(u, 1f));
            }
            return verts;
        }

        /// <summary>
        /// 单指条带，掌根(u=0.84)→爪尖(u=1.0)：三节骨带指节隆起，末段收成锐利硬爪；<br/>
        /// 顶点色 G 通道 0=焦肉 → 1=爪面，交由 shader 换质
        /// </summary>
        internal VertexPositionColorTexture[] BuildFingerStrip(int k) {
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
                    width = MathHelper.Lerp(5.8f, 3.4f, t / 0.75f) * (1f + bump * 0.42f);
                }
                else {
                    width = MathHelper.Lerp(3.2f, 0.6f, (t - 0.75f) / 0.25f);
                }
                width *= scale;

                Color vCol = new(0.5f, MathHelper.Clamp((t - 0.70f) / 0.16f, 0f, 1f), 0f);
                float u = PalmUMax + t * (1f - PalmUMax);
                verts[i * 2] = new VertexPositionColorTexture((pts[i] + normal * width).ToVector3()
                    , vCol, new Vector2(u, 0f));
                verts[i * 2 + 1] = new VertexPositionColorTexture((pts[i] - normal * width).ToVector3()
                    , vCol, new Vector2(u, 1f));
            }
            return verts;
        }
    }
}
