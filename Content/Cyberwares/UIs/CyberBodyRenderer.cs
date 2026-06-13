using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Cyberwares.UIs
{
    /// <summary>像素人体渲染：轮廓/电路/植入节点</summary>
    internal class CyberBodyRenderer
    {
        private readonly struct CircuitCurve
        {
            public readonly Vector2 Start;
            public readonly Vector2 Control1;
            public readonly Vector2 Control2;
            public readonly Vector2 End;
            public readonly float PulseOffset;
            public readonly float Thickness;

            public float MidY => (Start.Y + Control1.Y + Control2.Y + End.Y) * 0.25f;

            public CircuitCurve(float x1, float y1, float x2, float y2, float x3, float y3, float x4, float y4,
                float pulseOffset, float thickness) {
                Start = new Vector2(x1, y1);
                Control1 = new Vector2(x2, y2);
                Control2 = new Vector2(x3, y3);
                End = new Vector2(x4, y4);
                PulseOffset = pulseOffset;
                Thickness = thickness;
            }
        }

        private readonly struct CircuitSegment
        {
            public readonly int X1;
            public readonly int Y1;
            public readonly int X2;
            public readonly int Y2;
            public readonly float PulseOffset;
            public readonly float Thickness;

            public float MidY => (Y1 + Y2) * 0.5f;

            public CircuitSegment(int x1, int y1, int x2, int y2, float pulseOffset, float thickness) {
                X1 = x1;
                Y1 = y1;
                X2 = x2;
                Y2 = y2;
                PulseOffset = pulseOffset;
                Thickness = thickness;
            }
        }

        private readonly struct CapillaryBranch
        {
            public readonly int X1;
            public readonly int Y1;
            public readonly int X2;
            public readonly int Y2;
            public readonly int X3;
            public readonly int Y3;
            public readonly float FlickerOffset;

            public float MidY => (Y1 + Y2 + Y3) / 3f;

            public CapillaryBranch(int x1, int y1, int x2, int y2, int x3, int y3, float flickerOffset) {
                X1 = x1;
                Y1 = y1;
                X2 = x2;
                Y2 = y2;
                X3 = x3;
                Y3 = y3;
                FlickerOffset = flickerOffset;
            }
        }

        /// <summary>像素缩放倍率</summary>
        public const float PixelScale = 4.5f;

        /// <summary>人体节点数 11</summary>
        public const int NodeCount = 11;

        private static readonly Vector2 BodyGridCenter = new(16f, 28f);

        #region 像素人体数据

        //轮廓线段 32x56 网格，裆 y=28
        private static readonly int[,] OutlineSegments = {
            //头部（圆角矩形，所有角用斜线连接）
            {12, 0, 20, 0},     //颅顶
            {11, 1, 12, 0},     //左上圆角
            {20, 0, 21, 1},     //右上圆角
            {11, 1, 11, 5},     //左太阳穴
            {21, 1, 21, 5},     //右太阳穴
            {11, 5, 12, 6},     //左颧骨
            {21, 5, 20, 6},     //右颧骨
            //下颌线过渡到颈部（斜线确保连接）
            {12, 6, 14, 8},     //左下颌
            {20, 6, 18, 8},     //右下颌
            {14, 8, 18, 8},     //下巴
            //颈部（共享下颌端点）
            {14, 8, 14, 10},    //左颈
            {18, 8, 18, 10},    //右颈
            //肩膀（自然斜坡，共享颈部端点）
            {14, 10, 8, 12},    //左肩斜坡
            {18, 10, 24, 12},   //右肩斜坡
            //躯干上段（连接肩膀到手臂分叉处）
            {8, 12, 8, 13},     //左上胸侧
            {24, 12, 24, 13},   //右上胸侧
            {10, 14, 22, 14},   //胸顶横线
            //左侧躯干（胸→腰收窄→髋外扩，总14格）
            {10, 14, 10, 21},   //左胸侧
            {10, 21, 11, 24},   //左腰收窄斜线
            {11, 24, 11, 26},   //左腰窄段
            {11, 26, 10, 28},   //左髋外扩
            //右侧躯干（镜像）
            {22, 14, 22, 21},   //右胸侧
            {22, 21, 21, 24},   //右腰收窄斜线
            {21, 24, 21, 26},   //右腰窄段
            {21, 26, 22, 28},   //右髋外扩
            //左臂（肩→上臂→肘关节→前臂→腕→手，指尖到大腿中段）
            {5, 13, 8, 13},     //肩顶
            {5, 13, 5, 29},     //外侧连续线
            {8, 13, 8, 20},     //上臂内侧
            {8, 20, 7, 21},     //肘关节内斜
            {7, 21, 7, 29},     //前臂内侧
            {5, 29, 4, 30},     //腕外展
            {7, 29, 8, 30},     //腕内展
            {4, 30, 4, 32},     //手外侧
            {8, 30, 8, 32},     //手内侧
            {4, 32, 9, 32},     //手掌底
            //右臂（镜像）
            {24, 13, 27, 13},   //肩顶
            {27, 13, 27, 29},   //外侧连续线
            {24, 13, 24, 20},   //上臂内侧
            {24, 20, 25, 21},   //肘关节内斜
            {25, 21, 25, 29},   //前臂内侧
            {27, 29, 28, 30},   //腕外展
            {25, 29, 24, 30},   //腕内展
            {28, 30, 28, 32},   //手外侧
            {24, 30, 24, 32},   //手内侧
            {23, 32, 28, 32},   //手掌底
            //髋部（分叉+裆部+衔接腿部，y=28正中线）
            {10, 28, 15, 28},   //左髋横线
            {17, 28, 22, 28},   //右髋横线
            {10, 28, 10, 29},   //左外侧髋→腿衔接
            {22, 28, 22, 29},   //右外侧髋→腿衔接
            {15, 28, 15, 29},   //左裆内侧
            {17, 28, 17, 29},   //右裆内侧
            //左腿（大腿→膝关节→小腿→踝→足，总28格）
            {10, 29, 10, 40},   //大腿外侧
            {15, 29, 15, 40},   //大腿内侧
            {10, 40, 11, 42},   //膝外斜
            {15, 40, 14, 42},   //膝内斜
            {11, 42, 11, 52},   //小腿外侧
            {14, 42, 14, 52},   //小腿内侧
            {11, 52, 9, 56},    //踝→足跟
            {14, 52, 16, 56},   //踝→脚趾
            {9, 56, 16, 56},    //左足底
            //右腿（镜像）
            {22, 29, 22, 40},   //大腿外侧
            {17, 29, 17, 40},   //大腿内侧
            {22, 40, 21, 42},   //膝外斜
            {17, 40, 18, 42},   //膝内斜
            {21, 42, 21, 52},   //小腿外侧
            {18, 42, 18, 52},   //小腿内侧
            {21, 52, 23, 56},   //踝→足跟
            {18, 52, 16, 56},   //踝→脚趾
            {16, 56, 23, 56},   //右足底
        };

        //主经脉
        private static readonly CircuitCurve[] PrimaryCircuits = {
            new(16f, 1f, 15.45f, 2.15f, 16.35f, 4.85f, 16f, 6f, -0.25f, 1.7f),
            new(16f, 6f, 15.1f, 12.1f, 17.15f, 21.2f, 16f, 28f, 0f, 3.05f),
            new(10f, 14f, 11.55f, 13.1f, 13.9f, 12.5f, 16f, 13f, 0.18f, 2f),
            new(22f, 14f, 20.45f, 13.1f, 18.1f, 12.5f, 16f, 13f, 0.26f, 2f),
            new(16f, 28f, 14.7f, 28.55f, 13.05f, 30.05f, 12f, 31f, 0.52f, 2.1f),
            new(16f, 28f, 17.3f, 28.55f, 18.95f, 30.05f, 20f, 31f, 0.6f, 2.1f),
            new(6f, 14f, 5.15f, 15.55f, 5.35f, 18.2f, 6f, 20f, 0.9f, 1.42f),
            new(6f, 21f, 5.25f, 22.75f, 5.4f, 26.7f, 6f, 29f, 1.12f, 1.26f),
            new(26f, 14f, 26.85f, 15.55f, 26.65f, 18.2f, 26f, 20f, 1.02f, 1.42f),
            new(26f, 21f, 26.75f, 22.75f, 26.6f, 26.7f, 26f, 29f, 1.24f, 1.26f),
            new(12f, 29f, 11.15f, 31.75f, 11.35f, 36.55f, 12f, 40f, 1.48f, 1.7f),
            new(12f, 42f, 11.1f, 44.35f, 11.25f, 49.25f, 12f, 52f, 1.76f, 1.48f),
            new(20f, 29f, 20.85f, 31.75f, 20.65f, 36.55f, 20f, 40f, 1.6f, 1.7f),
            new(20f, 42f, 20.9f, 44.35f, 20.75f, 49.25f, 20f, 52f, 1.88f, 1.48f),
        };

        //次级结构线
        private static readonly CircuitSegment[] SecondaryCircuits = {
            new(13, 2, 19, 2, 0.12f, 1.2f),
            new(14, 4, 18, 4, 0.2f, 1.2f),
            new(14, 3, 15, 3, 0.32f, 1f),
            new(17, 3, 18, 3, 0.36f, 1f),
            new(13, 15, 19, 18, 0.58f, 1f),
            new(19, 15, 13, 18, 0.64f, 1f),
            new(12, 24, 20, 24, 1.08f, 1.1f),
            new(13, 26, 19, 26, 1.18f, 1.1f),
            new(11, 28, 16, 32, 1.34f, 1.2f),
            new(21, 28, 16, 32, 1.42f, 1.2f),
            new(11, 41, 14, 41, 1.86f, 1f),
            new(18, 41, 21, 41, 1.92f, 1f),
            new(10, 55, 14, 55, 2.1f, 1f),
            new(18, 55, 22, 55, 2.16f, 1f),
            new(5, 31, 8, 31, 1.46f, 1f),
            new(24, 31, 27, 31, 1.58f, 1f),
            new(5, 21, 7, 21, 1.08f, 1f),
            new(25, 21, 27, 21, 1.16f, 1f),
        };

        //胸腔肋骨弧线
        private static readonly CircuitCurve[] RibCageCurves = {
            new(15.95f, 16.55f, 14.65f, 15.95f, 12.15f, 16.55f, 10.55f, 17.75f, 0.68f, 1f),
            new(16.05f, 16.55f, 17.35f, 15.95f, 19.85f, 16.55f, 21.45f, 17.75f, 0.74f, 1f),
            new(15.95f, 18.65f, 14.65f, 18.05f, 12.45f, 18.7f, 10.95f, 19.75f, 0.82f, 0.98f),
            new(16.05f, 18.65f, 17.35f, 18.05f, 19.55f, 18.7f, 21.05f, 19.75f, 0.88f, 0.98f),
            new(15.95f, 20.85f, 14.85f, 20.35f, 13.05f, 21.05f, 11.75f, 21.95f, 0.96f, 0.92f),
            new(16.05f, 20.85f, 17.15f, 20.35f, 18.95f, 21.05f, 20.25f, 21.95f, 1.02f, 0.92f),
        };

        //毛细分支
        private static readonly CapillaryBranch[] CapillaryBranches = {
            new(16, 8, 14, 10, 12, 12, 0.12f),
            new(16, 8, 18, 10, 20, 12, 0.18f),
            new(16, 14, 13, 16, 11, 18, 0.34f),
            new(16, 14, 19, 16, 21, 18, 0.38f),
            new(16, 18, 13, 20, 12, 22, 0.5f),
            new(16, 18, 19, 20, 20, 22, 0.56f),
            new(16, 23, 14, 24, 12, 25, 0.74f),
            new(16, 23, 18, 24, 20, 25, 0.8f),
            new(6, 18, 5, 20, 5, 23, 0.94f),
            new(26, 18, 27, 20, 27, 23, 1.02f),
            new(12, 34, 11, 36, 10, 38, 1.38f),
            new(20, 34, 21, 36, 22, 38, 1.46f),
            new(12, 46, 11, 48, 10, 50, 1.66f),
            new(20, 46, 21, 48, 22, 50, 1.74f),
        };

        //赛博植入节点坐标(x,y)
        private static readonly int[,] NodePositions = {
            {16, 4},   //头部芯片
            {16, 17},  //胸腔核心
            {6, 19},   //左臂改造
            {26, 19},  //右臂改造
            {12, 25},  //左手植入
            {20, 25},  //右手植入
            {16, 28},  //腰椎接口
            {12, 36},  //左腿改造
            {20, 36},  //右腿改造
            {12, 48},  //左足增强
            {20, 48},  //右足增强
        };

        #endregion

        #region 动画状态

        private float breathePhase;
        private float scanLineY;
        private float energyFlowPhase;
        private readonly float[] nodePulsePhase = new float[NodeCount];
        private Vector2 currentFocusGrid = BodyGridCenter;
        private Vector2 targetFocusGrid = BodyGridCenter;
        private float focusStrength;
        private float targetFocusStrength;

        #endregion

        #region 公共方法

        /// <summary>
        ///设置当前人体聚焦的节点和强度，用于选中义体时放大并平移人体
        /// </summary>
        public void SetFocusNode(int nodeIndex, float strength) {
            targetFocusStrength = MathHelper.Clamp(strength, 0f, 1f);
            if (nodeIndex >= 0 && nodeIndex < NodeCount) {
                targetFocusGrid = new Vector2(NodePositions[nodeIndex, 0], NodePositions[nodeIndex, 1]);
            }
            else {
                targetFocusGrid = BodyGridCenter;
            }
        }

        public float FocusVisualStrength => EaseOutFocus(focusStrength);

        /// <summary>
        ///获取指定节点在屏幕上的世界坐标
        /// </summary>
        public Vector2 GetNodeWorldPosition(int nodeIndex, Vector2 bodyOrigin) {
            float breathe = MathF.Sin(breathePhase) * 0.8f;
            GetBodyTransform(bodyOrigin, out float s, out Vector2 offset);
            return offset + new Vector2(NodePositions[nodeIndex, 0] * s, NodePositions[nodeIndex, 1] * s + breathe);
        }

        /// <summary>
        ///推进呼吸和节点脉冲等动画计时器
        /// </summary>
        public void Update() {
            breathePhase += 0.02f;
            if (breathePhase > MathHelper.TwoPi) breathePhase -= MathHelper.TwoPi;

            //扫描线从头到脚循环（0→56网格单位）
            scanLineY += 0.35f;
            if (scanLineY > 60f) scanLineY = -4f;

            //能量流动相位
            energyFlowPhase += 0.04f;
            if (energyFlowPhase > MathHelper.TwoPi) energyFlowPhase -= MathHelper.TwoPi;

            for (int i = 0; i < nodePulsePhase.Length; i++) {
                nodePulsePhase[i] += 0.03f + i * 0.004f;
                if (nodePulsePhase[i] > MathHelper.TwoPi) nodePulsePhase[i] -= MathHelper.TwoPi;
            }

            float focusLerp = targetFocusStrength > focusStrength ? 0.24f : 0.2f;
            focusStrength += (targetFocusStrength - focusStrength) * focusLerp;
            currentFocusGrid = Vector2.Lerp(currentFocusGrid, targetFocusGrid, 0.26f + focusStrength * 0.1f);
        }

        /// <summary>
        ///绘制完整的像素人体，包括填充、内部结构、轮廓和外发光
        /// </summary>
        public void DrawBody(SpriteBatch sb, float alpha, Vector2 bodyOrigin, float globalTimer) {
            Texture2D px = CWRAsset.Placeholder_White?.Value;
            if (px == null) return;

            float breathe = MathF.Sin(breathePhase) * 0.8f;
            GetBodyTransform(bodyOrigin, out float s, out Vector2 bodyOffset);
            float focusScale = s / PixelScale;

            //填充身体主要区域
            DrawBodyFill(sb, px, bodyOffset, s, alpha, breathe);

            //内部骨骼电路（分层主经脉、次级结构和毛细分支）
            DrawInnerCircuits(sb, px, bodyOffset, s, alpha, breathe, globalTimer);

            //轮廓线
            Color outlineColor = CyberwareTheme.BodyOutline * (alpha * 0.7f);
            for (int i = 0; i < OutlineSegments.GetLength(0); i++) {
                Vector2 start = bodyOffset + new Vector2(OutlineSegments[i, 0] * s, OutlineSegments[i, 1] * s + breathe);
                Vector2 end = bodyOffset + new Vector2(OutlineSegments[i, 2] * s, OutlineSegments[i, 3] * s + breathe);
                CyberwareTheme.DrawLine(sb, px, start, end, 2f, outlineColor);
            }

            //全息鬼影，色差伪影
            Color ghostColor = CyberwareTheme.AccentCyan * (alpha * 0.08f);
            float ghostDx = MathF.Sin(globalTimer * 1.5f) * 2f;
            float ghostDy = MathF.Cos(globalTimer * 1.8f) * 1f;
            for (int i = 0; i < OutlineSegments.GetLength(0); i++) {
                Vector2 gStart = bodyOffset + new Vector2(OutlineSegments[i, 0] * s + ghostDx, OutlineSegments[i, 1] * s + breathe + ghostDy);
                Vector2 gEnd = bodyOffset + new Vector2(OutlineSegments[i, 2] * s + ghostDx, OutlineSegments[i, 3] * s + breathe + ghostDy);
                CyberwareTheme.DrawLine(sb, px, gStart, gEnd, 1f, ghostColor);
            }

            //人体扫描线，头到脚循环
            float scanWorldY = bodyOffset.Y + scanLineY * s + breathe;
            if (scanWorldY > bodyOffset.Y && scanWorldY < bodyOffset.Y + 56 * s) {
                Color bodyScanColor = CyberwareTheme.Accent * (alpha * 0.15f);
                int scanLeft = (int)(bodyOffset.X + 4 * s);
                int scanWidth = (int)(24 * s);
                sb.Draw(px, new Rectangle(scanLeft, (int)scanWorldY, scanWidth, 2),
                    new Rectangle(0, 0, 1, 1), bodyScanColor);
                for (int j = 1; j <= 5; j++) {
                    float tf = 1f - j / 5f;
                    sb.Draw(px, new Rectangle(scanLeft, (int)scanWorldY - j * 3, scanWidth, 1),
                        new Rectangle(0, 0, 1, 1), bodyScanColor * (tf * 0.4f));
                }
            }

            //能量粒子沿躯干与四肢主经脉流动
            DrawSpineEnergyFlow(sb, px, bodyOffset, s, alpha, breathe, globalTimer);

            //外发光
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null) {
                float glowPulse = MathF.Sin(globalTimer * 1.2f) * 0.1f + 0.9f;
                Color bodyGlow = CyberwareTheme.Accent * (alpha * 0.08f * glowPulse);
                bodyGlow.A = 0;
                sb.Draw(glow, bodyOrigin + new Vector2(0, breathe),
                    null, bodyGlow, 0, glow.Size() / 2, 5.5f * focusScale, SpriteEffects.None, 0);
            }
        }

        /// <summary>
        ///绘制赛博植入节点标记
        ///nodeStates数组标记各节点状态：0普通 1已连接 2高亮激活
        /// </summary>
        public void DrawNodes(SpriteBatch sb, float alpha, Vector2 bodyOrigin, int[] nodeStates) {
            Texture2D px = CWRAsset.Placeholder_White?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (px == null) return;

            GetBodyTransform(bodyOrigin, out float s, out Vector2 bodyOffset);
            float breathe = MathF.Sin(breathePhase) * 0.8f;
            float focusScale = s / PixelScale;

            for (int i = 0; i < NodeCount; i++) {
                Vector2 nodePos = bodyOffset + new Vector2(NodePositions[i, 0] * s, NodePositions[i, 1] * s + breathe);
                float pulse = MathF.Sin(nodePulsePhase[i]) * 0.3f + 0.7f;

                int state = i < nodeStates.Length ? nodeStates[i] : 0;
                bool isHighlighted = state == 2;

                Color nodeColor = state switch {
                    2 => CyberwareTheme.AccentGold,
                    1 => CyberwareTheme.Accent,
                    _ => CyberwareTheme.AccentCyan
                };
                nodeColor *= alpha * pulse;

                //节点菱形方块
                float nodeSize = (isHighlighted ? 7f : 4f) * MathHelper.Lerp(1f, 1.2f, focusStrength) * focusScale;
                sb.Draw(px, nodePos, new Rectangle(0, 0, 1, 1), nodeColor,
                    MathHelper.PiOver4, new Vector2(0.5f), new Vector2(nodeSize), SpriteEffects.None, 0f);

                //节点光晕
                if (glow != null) {
                    Color nodeGlow = nodeColor * 0.4f;
                    nodeGlow.A = 0;
                    sb.Draw(glow, nodePos, null, nodeGlow, 0, glow.Size() / 2,
                        (0.08f + (isHighlighted ? 0.05f : 0f)) * focusScale, SpriteEffects.None, 0);
                }
            }
        }

        #endregion

        #region 私有方法

        /// <summary>
        ///绘制内部电路线，按主经脉、次级结构和毛细分支分层渲染
        /// </summary>
        private void DrawInnerCircuits(SpriteBatch sb, Texture2D px, Vector2 bodyOffset, float s, float alpha, float breathe, float globalTimer) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            DrawPrimaryCurves(sb, px, glow, bodyOffset, s, alpha, breathe, globalTimer);
            DrawCircuitSegments(sb, px, glow, bodyOffset, s, alpha, breathe, globalTimer, SecondaryCircuits, false);
            DrawRibCageCurves(sb, px, glow, bodyOffset, s, alpha, breathe, globalTimer);
            DrawCranialCluster(sb, px, glow, bodyOffset, s, alpha, breathe, globalTimer);
            DrawThoracicCluster(sb, px, glow, bodyOffset, s, alpha, breathe, globalTimer);
            DrawAbdominalPelvicCluster(sb, px, glow, bodyOffset, s, alpha, breathe, globalTimer);
            DrawCapillaryNetwork(sb, px, bodyOffset, s, alpha, breathe, globalTimer);
        }

        private void GetBodyTransform(Vector2 bodyOrigin, out float scale, out Vector2 bodyOffset) {
            float focusVisualStrength = FocusVisualStrength;
            scale = PixelScale * MathHelper.Lerp(1f, 1.42f, focusVisualStrength);
            Vector2 focusShift = -(currentFocusGrid - BodyGridCenter) * PixelScale * focusVisualStrength * 0.68f;
            bodyOffset = bodyOrigin + focusShift - BodyGridCenter * scale;
        }

        private static float EaseOutFocus(float t) {
            t = MathHelper.Clamp(t, 0f, 1f);
            float inv = 1f - t;
            return 1f - inv * inv * inv;
        }

        /// <summary>
        ///沿主经脉曲线路径绘制流动能量粒子
        /// </summary>
        private void DrawSpineEnergyFlow(SpriteBatch sb, Texture2D px, Vector2 bodyOffset, float s, float alpha, float breathe, float globalTimer) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;

            DrawCurveEnergyFlow(sb, px, glow, bodyOffset, s, alpha, breathe, PrimaryCircuits[0], 2, 1.08f, CyberwareTheme.Accent, 2.5f, 0.026f);
            DrawCurveEnergyFlow(sb, px, glow, bodyOffset, s, alpha, breathe, PrimaryCircuits[1], 5, 1f, CyberwareTheme.Accent, 3.2f, 0.045f);
            DrawCurveEnergyFlow(sb, px, glow, bodyOffset, s, alpha, breathe, PrimaryCircuits[2], 2, 0.82f, CyberwareTheme.AccentCyan, 2.1f, 0.028f);
            DrawCurveEnergyFlow(sb, px, glow, bodyOffset, s, alpha, breathe, PrimaryCircuits[3], 2, 0.82f, CyberwareTheme.AccentCyan, 2.1f, 0.028f);
            DrawCurveEnergyFlow(sb, px, glow, bodyOffset, s, alpha, breathe, PrimaryCircuits[6], 2, 0.74f, CyberwareTheme.AccentCyan, 2.2f, 0.03f);
            DrawCurveEnergyFlow(sb, px, glow, bodyOffset, s, alpha, breathe, PrimaryCircuits[7], 2, 0.72f, CyberwareTheme.AccentCyan, 2.1f, 0.028f);
            DrawCurveEnergyFlow(sb, px, glow, bodyOffset, s, alpha, breathe, PrimaryCircuits[8], 2, 0.74f, CyberwareTheme.AccentCyan, 2.2f, 0.03f);
            DrawCurveEnergyFlow(sb, px, glow, bodyOffset, s, alpha, breathe, PrimaryCircuits[9], 2, 0.72f, CyberwareTheme.AccentCyan, 2.1f, 0.028f);
            DrawCurveEnergyFlow(sb, px, glow, bodyOffset, s, alpha, breathe, PrimaryCircuits[4], 2, 0.74f, CyberwareTheme.Accent, 2.25f, 0.028f);
            DrawCurveEnergyFlow(sb, px, glow, bodyOffset, s, alpha, breathe, PrimaryCircuits[5], 2, 0.74f, CyberwareTheme.Accent, 2.25f, 0.028f);
            DrawCurveEnergyFlow(sb, px, glow, bodyOffset, s, alpha, breathe, PrimaryCircuits[10], 2, 0.72f, CyberwareTheme.Accent, 2.7f, 0.036f);
            DrawCurveEnergyFlow(sb, px, glow, bodyOffset, s, alpha, breathe, PrimaryCircuits[11], 2, 0.7f, CyberwareTheme.Accent, 2.45f, 0.032f);
            DrawCurveEnergyFlow(sb, px, glow, bodyOffset, s, alpha, breathe, PrimaryCircuits[12], 2, 0.72f, CyberwareTheme.Accent, 2.7f, 0.036f);
            DrawCurveEnergyFlow(sb, px, glow, bodyOffset, s, alpha, breathe, PrimaryCircuits[13], 2, 0.7f, CyberwareTheme.Accent, 2.45f, 0.032f);
        }

        private void DrawPrimaryCurves(SpriteBatch sb, Texture2D px, Texture2D glow, Vector2 bodyOffset,
            float s, float alpha, float breathe, float globalTimer) {
            for (int i = 0; i < PrimaryCircuits.Length; i++) {
                CircuitCurve curve = PrimaryCircuits[i];
                bool isSpine = i <= 1;
                bool isLimb = i >= 6;
                float wave = MathF.Sin(energyFlowPhase - curve.MidY * 0.18f + curve.PulseOffset * 2.6f);
                float waveLerp = Math.Clamp(wave * 0.5f + 0.5f, 0f, 1f);
                float flicker = MathF.Sin(globalTimer * (1.9f + curve.PulseOffset * 0.25f) + curve.PulseOffset * 6f) * 0.08f + 0.92f;
                float scanBoost = 1f + MathHelper.Clamp(1f - MathF.Abs(curve.MidY - scanLineY) / 4.25f, 0f, 1f) * 0.5f;
                float brightness = (wave * 0.22f + 0.78f) * flicker * scanBoost;

                Color lowColor = Color.Lerp(CyberwareTheme.BodyInner, CyberwareTheme.Accent, 0.32f);
                Color highColor = Color.Lerp(CyberwareTheme.Accent, CyberwareTheme.AccentCyan, 0.45f);
                Color lineColor = Color.Lerp(lowColor, highColor, waveLerp);
                lineColor *= alpha * brightness * 0.92f;

                float outerBoost = isSpine ? 4.3f : 2.4f;
                float innerBoost = isSpine ? 2.05f : 1.15f;
                int outerSteps = isSpine ? 14 : 11;
                int coreSteps = isSpine ? 18 : 14;

                DrawCurvePath(sb, px, bodyOffset, s, breathe, curve, curve.Thickness + outerBoost, lineColor * (isSpine ? 0.18f : 0.1f), outerSteps, isSpine ? 0.22f : 0.34f, isSpine ? 0.18f : 0.42f);
                DrawCurvePath(sb, px, bodyOffset, s, breathe, curve, curve.Thickness + innerBoost, lineColor * (isSpine ? 0.32f : 0.2f), coreSteps - 2, isSpine ? 0.1f : 0.24f, isSpine ? 0.08f : 0.28f);

                if (isLimb) {
                    Color filamentColor = Color.Lerp(lineColor, CyberwareTheme.AccentCyan * alpha, 0.25f) * 0.52f;
                    DrawCurvePath(sb, px, bodyOffset, s, breathe, curve, curve.Thickness * 0.54f, filamentColor, 15, 0.44f, 0.24f);
                    DrawCurveFilament(sb, px, bodyOffset, s, breathe, curve, curve.Thickness * 0.36f, filamentColor * 0.56f, 14, 0.34f, -0.48f * s);
                    DrawCurveFilament(sb, px, bodyOffset, s, breathe, curve, curve.Thickness * 0.33f, filamentColor * 0.48f, 14, 0.34f, 0.48f * s);
                }
                else {
                    DrawCurvePath(sb, px, bodyOffset, s, breathe, curve, curve.Thickness, lineColor, coreSteps, 0.18f, 0.12f);
                }

                Color coreColor = Color.Lerp(lineColor, CyberwareTheme.AccentCyan * alpha, 0.45f);
                DrawCurvePath(sb, px, bodyOffset, s, breathe, curve, Math.Max(0.8f, curve.Thickness * (isSpine ? 0.34f : 0.28f)), coreColor, coreSteps - 2, isSpine ? 0.04f : 0.18f, isSpine ? 0.04f : 0.2f);

                if (i == 1) {
                    DrawSpineInterfaceNodes(sb, px, glow, bodyOffset, s, alpha, breathe, curve, lineColor, globalTimer);
                }

                if (glow != null) {
                    Vector2 start = ToWorld(bodyOffset, s, breathe, curve.Start);
                    Vector2 mid = ToWorld(bodyOffset, s, breathe, EvaluateCubicBezier(0.5f, curve));
                    Vector2 end = ToWorld(bodyOffset, s, breathe, curve.End);
                    DrawGlowOrb(sb, glow, start, lineColor * 0.22f, 0.024f);
                    DrawGlowOrb(sb, glow, mid, lineColor * 0.16f, 0.02f);
                    DrawGlowOrb(sb, glow, end, lineColor * 0.22f, 0.024f);
                }
            }
        }

        private void DrawThoracicCluster(SpriteBatch sb, Texture2D px, Texture2D glow, Vector2 bodyOffset,
            float s, float alpha, float breathe, float globalTimer) {
            Vector2 clusterCenter = new Vector2(16f, 17.55f);
            float pulse = MathF.Sin(energyFlowPhase * 1.28f + 0.45f) * 0.16f + 0.84f;
            float scanBoost = 1f + MathHelper.Clamp(1f - MathF.Abs(clusterCenter.Y - scanLineY) / 4f, 0f, 1f) * 0.45f;
            Color shellColor = Color.Lerp(CyberwareTheme.BodyInner, CyberwareTheme.Accent, 0.62f) * (alpha * 0.31f * pulse * scanBoost);
            Color membraneColor = Color.Lerp(CyberwareTheme.BodyInner, CyberwareTheme.Accent, 0.74f) * (alpha * 0.49f * pulse * scanBoost);
            Color coreColor = Color.Lerp(CyberwareTheme.Accent, CyberwareTheme.AccentGold, 0.18f) * (alpha * 0.62f * pulse * scanBoost);
            Color vesselColor = Color.Lerp(CyberwareTheme.BodyInner, CyberwareTheme.Accent, 0.52f) * (alpha * 0.23f * pulse);

            DrawEllipticalArc(sb, px, bodyOffset, s, breathe, clusterCenter + new Vector2(-0.15f, 0.05f), 3.95f, 5.35f,
                0.48f + globalTimer * 0.08f, MathHelper.TwoPi - 0.62f + globalTimer * 0.08f, shellColor * 0.58f, 1.15f, 24);
            DrawEllipticalArc(sb, px, bodyOffset, s, breathe, clusterCenter + new Vector2(0.18f, 0.38f), 3.2f, 4.25f,
                0.92f - globalTimer * 0.06f, MathHelper.TwoPi - 1.05f - globalTimer * 0.06f, membraneColor * 0.72f, 0.95f, 20);
            DrawEllipticalArc(sb, px, bodyOffset, s, breathe, clusterCenter + new Vector2(-0.08f, -0.22f), 2.2f, 2.85f,
                1.3f + globalTimer * 0.05f, MathHelper.TwoPi - 1.48f + globalTimer * 0.05f, membraneColor * 0.54f, 0.85f, 16);

            Vector2 worldCenter = ToWorld(bodyOffset, s, breathe, clusterCenter);
            Vector2 upperLink = ToWorld(bodyOffset, s, breathe, new Vector2(16f, 13.95f));
            Vector2 lowerLink = ToWorld(bodyOffset, s, breathe, new Vector2(16f, 21.35f));
            Vector2 leftUpper = ToWorld(bodyOffset, s, breathe, new Vector2(13.65f, 15.5f));
            Vector2 rightUpper = ToWorld(bodyOffset, s, breathe, new Vector2(18.35f, 15.5f));
            Vector2 leftLower = ToWorld(bodyOffset, s, breathe, new Vector2(14.45f, 20.15f));
            Vector2 rightLower = ToWorld(bodyOffset, s, breathe, new Vector2(17.45f, 19.9f));
            CyberwareTheme.DrawLine(sb, px, upperLink, worldCenter + new Vector2(0f, -5.5f), 1.1f, vesselColor * 0.75f);
            CyberwareTheme.DrawLine(sb, px, worldCenter + new Vector2(-1.2f, 4.6f), leftLower, 0.95f, vesselColor * 0.68f);
            CyberwareTheme.DrawLine(sb, px, worldCenter + new Vector2(1.15f, 4.2f), rightLower, 0.95f, vesselColor * 0.62f);
            CyberwareTheme.DrawLine(sb, px, worldCenter + new Vector2(-2.1f, -3.4f), leftUpper, 0.9f, vesselColor * 0.58f);
            CyberwareTheme.DrawLine(sb, px, worldCenter + new Vector2(2.1f, -3.2f), rightUpper, 0.9f, vesselColor * 0.52f);
            CyberwareTheme.DrawLine(sb, px, worldCenter + new Vector2(0.25f, 3.4f), lowerLink, 1.05f, vesselColor * 0.7f);

            Vector2 leftChamber = worldCenter + new Vector2(-5.2f, -0.8f + MathF.Sin(globalTimer * 1.7f) * 0.35f);
            Vector2 rightChamber = worldCenter + new Vector2(4.35f, 1.75f + MathF.Sin(globalTimer * 1.7f + 0.65f) * 0.28f);
            Color chamberColorLeft = Color.Lerp(coreColor, CyberwareTheme.AccentGold, 0.08f) * 0.96f;
            Color chamberColorRight = Color.Lerp(coreColor, CyberwareTheme.AccentGold, 0.04f) * 0.78f;
            Color chamberBridgeColor = Color.Lerp(coreColor, membraneColor, 0.35f) * 0.68f;

            DrawOrganicNode(sb, px, glow, leftChamber, chamberColorLeft, shellColor * 0.35f, 8.4f, 6.6f, -0.32f);
            DrawOrganicNode(sb, px, glow, rightChamber, chamberColorRight, shellColor * 0.28f, 6.8f, 5.4f, 0.28f);
            DrawOrganicNode(sb, px, glow, worldCenter + new Vector2(-0.25f, 0.2f), coreColor, membraneColor * 0.32f, 5.2f, 4.6f, 0.15f);

            CyberwareTheme.DrawLine(sb, px, leftChamber + new Vector2(3.1f, 1.1f), worldCenter + new Vector2(-0.2f, 0.15f), 1.15f, chamberBridgeColor);
            CyberwareTheme.DrawLine(sb, px, worldCenter + new Vector2(0.1f, 0.25f), rightChamber + new Vector2(-2.6f, -0.45f), 1.05f, chamberBridgeColor * 0.9f);
            CyberwareTheme.DrawLine(sb, px, leftChamber + new Vector2(0.9f, 3.25f), rightChamber + new Vector2(-0.6f, 2.1f), 0.85f, membraneColor * 0.45f);

            for (int i = 0; i < 4; i++) {
                float angle = 0.78f + i * 0.92f + globalTimer * 0.07f;
                Vector2 membranePoint = clusterCenter + new Vector2(MathF.Cos(angle) * (2.55f + i * 0.18f), MathF.Sin(angle) * (3.65f - i * 0.22f));
                Vector2 worldMembrane = ToWorld(bodyOffset, s, breathe, membranePoint);
                CyberwareTheme.DrawLine(sb, px, worldCenter, worldMembrane, 0.7f, membraneColor * (0.22f + i * 0.04f));
                if (glow != null) {
                    DrawGlowOrb(sb, glow, worldMembrane, membraneColor * 0.1f, 0.012f);
                }
            }

            if (glow != null) {
                DrawGlowOrb(sb, glow, worldCenter + new Vector2(-1.2f, 0.3f), coreColor * 0.22f, 0.038f);
                DrawGlowOrb(sb, glow, worldCenter + new Vector2(1.1f, 1.1f), membraneColor * 0.18f, 0.058f);
            }
        }

        private void DrawCranialCluster(SpriteBatch sb, Texture2D px, Texture2D glow, Vector2 bodyOffset,
            float s, float alpha, float breathe, float globalTimer) {
            Vector2 clusterCenter = new Vector2(16f, 3.55f);
            float pulse = MathF.Sin(energyFlowPhase * 1.52f + 1.1f) * 0.14f + 0.86f;
            float scanBoost = 1f + MathHelper.Clamp(1f - MathF.Abs(clusterCenter.Y - scanLineY) / 3.1f, 0f, 1f) * 0.35f;
            Color shellColor = Color.Lerp(CyberwareTheme.BodyInner, CyberwareTheme.Accent, 0.66f) * (alpha * 0.22f * pulse * scanBoost);
            Color membraneColor = Color.Lerp(CyberwareTheme.BodyInner, CyberwareTheme.AccentGold, 0.1f) * (alpha * 0.26f * pulse * scanBoost);
            Color coreColor = Color.Lerp(CyberwareTheme.Accent, CyberwareTheme.AccentGold, 0.08f) * (alpha * 0.34f * pulse * scanBoost);
            Color conduitColor = Color.Lerp(CyberwareTheme.BodyInner, CyberwareTheme.Accent, 0.58f) * (alpha * 0.16f * pulse);

            DrawEllipticalArc(sb, px, bodyOffset, s, breathe, clusterCenter + new Vector2(0f, 0.15f), 3.15f, 2.45f,
                0.28f + globalTimer * 0.06f, MathHelper.TwoPi - 0.52f + globalTimer * 0.06f, shellColor * 0.58f, 0.95f, 18);
            DrawEllipticalArc(sb, px, bodyOffset, s, breathe, clusterCenter + new Vector2(-0.1f, -0.05f), 2.35f, 1.82f,
                0.95f - globalTimer * 0.04f, MathHelper.TwoPi - 1.18f - globalTimer * 0.04f, membraneColor * 0.62f, 0.8f, 14);

            Vector2 worldCenter = ToWorld(bodyOffset, s, breathe, clusterCenter);
            Vector2 stemTop = ToWorld(bodyOffset, s, breathe, new Vector2(16f, 5.75f));
            Vector2 leftPort = ToWorld(bodyOffset, s, breathe, new Vector2(14.25f, 3.65f));
            Vector2 rightPort = ToWorld(bodyOffset, s, breathe, new Vector2(17.75f, 3.35f));
            CyberwareTheme.DrawLine(sb, px, worldCenter + new Vector2(-0.15f, 2.1f), stemTop, 0.95f, conduitColor * 0.72f);
            CyberwareTheme.DrawLine(sb, px, worldCenter + new Vector2(-3.1f, 0.45f), leftPort, 0.72f, conduitColor * 0.52f);
            CyberwareTheme.DrawLine(sb, px, worldCenter + new Vector2(2.85f, 0.2f), rightPort, 0.72f, conduitColor * 0.48f);

            Vector2 leftLobe = worldCenter + new Vector2(-3.05f, 0.2f + MathF.Sin(globalTimer * 1.5f) * 0.18f);
            Vector2 rightLobe = worldCenter + new Vector2(2.75f, -0.05f + MathF.Sin(globalTimer * 1.5f + 0.45f) * 0.15f);
            Vector2 midCore = worldCenter + new Vector2(-0.1f, 0.55f);

            DrawOrganicNode(sb, px, glow, leftLobe, coreColor * 0.78f, shellColor * 0.24f, 5.3f, 4.1f, -0.22f);
            DrawOrganicNode(sb, px, glow, rightLobe, coreColor * 0.66f, shellColor * 0.18f, 4.55f, 3.55f, 0.2f);
            DrawOrganicNode(sb, px, glow, midCore, Color.Lerp(coreColor, membraneColor, 0.2f), membraneColor * 0.2f, 3.45f, 2.85f, 0.08f);

            CyberwareTheme.DrawLine(sb, px, leftLobe + new Vector2(1.7f, 0.5f), midCore + new Vector2(-0.4f, 0.2f), 0.9f, membraneColor * 0.74f);
            CyberwareTheme.DrawLine(sb, px, midCore + new Vector2(0.35f, -0.1f), rightLobe + new Vector2(-1.45f, 0.1f), 0.85f, membraneColor * 0.64f);

            for (int i = 0; i < 3; i++) {
                float angle = 1.05f + i * 1.08f + globalTimer * 0.05f;
                Vector2 membranePoint = clusterCenter + new Vector2(MathF.Cos(angle) * (1.95f + i * 0.12f), MathF.Sin(angle) * (1.45f - i * 0.08f));
                Vector2 worldMembrane = ToWorld(bodyOffset, s, breathe, membranePoint);
                CyberwareTheme.DrawLine(sb, px, midCore, worldMembrane, 0.58f, membraneColor * (0.28f + i * 0.06f));
            }

            if (glow != null) {
                DrawGlowOrb(sb, glow, worldCenter + new Vector2(-0.8f, 0.2f), coreColor * 0.1f, 0.02f);
                DrawGlowOrb(sb, glow, worldCenter + new Vector2(0.6f, 0.15f), membraneColor * 0.08f, 0.03f);
            }
        }

        private void DrawAbdominalPelvicCluster(SpriteBatch sb, Texture2D px, Texture2D glow, Vector2 bodyOffset,
            float s, float alpha, float breathe, float globalTimer) {
            Vector2 abdominalCenter = new Vector2(16f, 24.35f);
            Vector2 pelvicCenter = new Vector2(16f, 28.4f);
            float pulse = MathF.Sin(energyFlowPhase * 1.08f + 1.65f) * 0.1f + 0.82f;
            float scanBoost = 1f + MathHelper.Clamp(1f - MathF.Abs(abdominalCenter.Y - scanLineY) / 4.4f, 0f, 1f) * 0.22f;

            Color membraneColor = Color.Lerp(CyberwareTheme.BodyInner, CyberwareTheme.Accent, 0.62f) * (alpha * 0.24f * pulse * scanBoost);
            Color vesselColor = Color.Lerp(CyberwareTheme.BodyInner, CyberwareTheme.AccentGold, 0.08f) * (alpha * 0.16f * pulse);
            Color coreColor = Color.Lerp(CyberwareTheme.Accent, CyberwareTheme.AccentGold, 0.08f) * (alpha * 0.28f * pulse * scanBoost);
            Color pelvicCoreColor = Color.Lerp(CyberwareTheme.Accent, CyberwareTheme.AccentGold, 0.14f) * (alpha * 0.38f * pulse * scanBoost);

            DrawEllipticalArc(sb, px, bodyOffset, s, breathe, abdominalCenter + new Vector2(-0.15f, 0.1f), 2.95f, 2.6f,
                0.92f + globalTimer * 0.03f, MathHelper.TwoPi - 1.05f + globalTimer * 0.03f, membraneColor * 0.58f, 0.72f, 16);
            DrawEllipticalArc(sb, px, bodyOffset, s, breathe, abdominalCenter + new Vector2(0.18f, 0.65f), 2.15f, 1.7f,
                1.26f - globalTimer * 0.025f, MathHelper.TwoPi - 1.52f - globalTimer * 0.025f, membraneColor * 0.4f, 0.62f, 12);
            DrawEllipticalArc(sb, px, bodyOffset, s, breathe, pelvicCenter + new Vector2(0f, 0.2f), 4.2f, 1.85f,
                0.2f, MathHelper.Pi - 0.08f, membraneColor * 0.46f, 0.78f, 14);

            Vector2 worldAbdomen = ToWorld(bodyOffset, s, breathe, abdominalCenter);
            Vector2 worldPelvis = ToWorld(bodyOffset, s, breathe, pelvicCenter);
            Vector2 thoracicLink = ToWorld(bodyOffset, s, breathe, new Vector2(16f, 21.15f));
            Vector2 sacralLink = ToWorld(bodyOffset, s, breathe, new Vector2(16f, 30.2f));
            Vector2 leftAbNode = ToWorld(bodyOffset, s, breathe, new Vector2(14.15f, 24.6f));
            Vector2 rightAbNode = ToWorld(bodyOffset, s, breathe, new Vector2(17.8f, 24.95f));
            Vector2 leftPelvicWing = ToWorld(bodyOffset, s, breathe, new Vector2(12.8f, 28.7f));
            Vector2 rightPelvicWing = ToWorld(bodyOffset, s, breathe, new Vector2(19.2f, 28.55f));

            CyberwareTheme.DrawLine(sb, px, thoracicLink, worldAbdomen + new Vector2(0f, -2.5f), 0.85f, vesselColor * 0.72f);
            CyberwareTheme.DrawLine(sb, px, worldAbdomen + new Vector2(-0.8f, 1.9f), worldPelvis + new Vector2(-0.35f, -1.1f), 0.82f, vesselColor * 0.54f);
            CyberwareTheme.DrawLine(sb, px, worldAbdomen + new Vector2(0.9f, 1.8f), worldPelvis + new Vector2(0.4f, -1.05f), 0.82f, vesselColor * 0.5f);
            CyberwareTheme.DrawLine(sb, px, worldPelvis + new Vector2(0f, 0.8f), sacralLink, 0.9f, vesselColor * 0.62f);
            CyberwareTheme.DrawLine(sb, px, worldPelvis + new Vector2(-0.6f, -0.1f), leftPelvicWing, 0.74f, membraneColor * 0.42f);
            CyberwareTheme.DrawLine(sb, px, worldPelvis + new Vector2(0.65f, -0.15f), rightPelvicWing, 0.74f, membraneColor * 0.38f);

            DrawOrganicNode(sb, px, glow, leftAbNode, coreColor * 0.72f, membraneColor * 0.22f, 4.6f, 3.5f, -0.2f);
            DrawOrganicNode(sb, px, glow, rightAbNode, coreColor * 0.64f, membraneColor * 0.18f, 4.1f, 3.1f, 0.18f);
            DrawOrganicNode(sb, px, glow, worldPelvis + new Vector2(0.1f, 0.55f), pelvicCoreColor * 0.88f, membraneColor * 0.26f, 5.55f, 4.05f, 0.06f);
            DrawEllipticalArc(sb, px, bodyOffset, s, breathe, pelvicCenter + new Vector2(0f, 0.62f), 2.05f, 1.22f,
                0.1f, MathHelper.TwoPi - 0.18f, pelvicCoreColor * 0.32f, 0.72f, 12);

            CyberwareTheme.DrawLine(sb, px, leftAbNode + new Vector2(1.2f, 0.35f), worldAbdomen + new Vector2(-0.2f, 0.6f), 0.66f, membraneColor * 0.48f);
            CyberwareTheme.DrawLine(sb, px, worldAbdomen + new Vector2(0.25f, 0.55f), rightAbNode + new Vector2(-1.05f, 0.2f), 0.62f, membraneColor * 0.44f);
            CyberwareTheme.DrawLine(sb, px, worldAbdomen + new Vector2(-0.15f, 1.8f), worldPelvis + new Vector2(0.05f, -0.55f), 0.58f, membraneColor * 0.34f);

            if (glow != null) {
                DrawGlowOrb(sb, glow, worldAbdomen + new Vector2(-0.4f, 0.2f), coreColor * 0.08f, 0.02f);
                DrawGlowOrb(sb, glow, worldPelvis + new Vector2(0f, 0.55f), pelvicCoreColor * 0.14f, 0.032f);
            }
        }

        private void DrawSpineInterfaceNodes(SpriteBatch sb, Texture2D px, Texture2D glow, Vector2 bodyOffset,
            float s, float alpha, float breathe, CircuitCurve spineCurve, Color lineColor, float globalTimer) {
            float[] segments = { 0.1f, 0.22f, 0.36f, 0.5f, 0.64f, 0.78f, 0.9f };

            for (int i = 0; i < segments.Length; i++) {
                float t = segments[i];
                Vector2 point = EvaluateCubicBezier(t, spineCurve);
                Vector2 tangent = EvaluateCubicBezierTangent(t, spineCurve);
                if (tangent.LengthSquared() < 0.001f) {
                    continue;
                }

                tangent.Normalize();
                Vector2 normal = new Vector2(-tangent.Y, tangent.X);
                float pulse = MathF.Sin(globalTimer * 2.2f + i * 0.7f + energyFlowPhase * 1.3f) * 0.22f + 0.78f;
                float halfSpan = (i == 3 ? 0.95f : 0.72f) * s;
                Vector2 worldPoint = ToWorld(bodyOffset, s, breathe, point);
                Vector2 left = worldPoint - normal * halfSpan;
                Vector2 right = worldPoint + normal * halfSpan;
                Color nodeColor = Color.Lerp(lineColor, CyberwareTheme.AccentGold * alpha, 0.24f) * (0.56f * pulse);
                Color coreColor = Color.Lerp(CyberwareTheme.Accent, CyberwareTheme.AccentCyan, 0.38f) * (alpha * 0.42f * pulse);

                CyberwareTheme.DrawLine(sb, px, left, right, 1.1f, nodeColor);
                sb.Draw(px, worldPoint - new Vector2(1.5f), new Rectangle(0, 0, 1, 1),
                    coreColor, 0f, Vector2.Zero, new Vector2(3f), SpriteEffects.None, 0f);

                if (glow != null) {
                    DrawGlowOrb(sb, glow, worldPoint, coreColor * 0.2f, i == 3 ? 0.022f : 0.017f);
                }
            }
        }

        private void DrawCircuitSegments(SpriteBatch sb, Texture2D px, Texture2D glow, Vector2 bodyOffset,
            float s, float alpha, float breathe, float globalTimer, CircuitSegment[] segments, bool isPrimary) {
            for (int i = 0; i < segments.Length; i++) {
                CircuitSegment segment = segments[i];
                float wave = MathF.Sin(energyFlowPhase - segment.MidY * 0.18f + segment.PulseOffset * 2.6f);
                float waveLerp = Math.Clamp(wave * 0.5f + 0.5f, 0f, 1f);
                float flicker = MathF.Sin(globalTimer * (1.9f + segment.PulseOffset * 0.25f) + segment.PulseOffset * 6f) * 0.08f + 0.92f;
                float scanBoost = 1f + MathHelper.Clamp(1f - MathF.Abs(segment.MidY - scanLineY) / 4.25f, 0f, 1f) * (isPrimary ? 0.5f : 0.25f);
                float brightness = (wave * 0.22f + 0.78f) * flicker * scanBoost;

                Color lowColor = isPrimary
                    ? Color.Lerp(CyberwareTheme.BodyInner, CyberwareTheme.Accent, 0.32f)
                    : Color.Lerp(CyberwareTheme.BodyInner, CyberwareTheme.Accent, 0.14f);
                Color highColor = isPrimary
                    ? Color.Lerp(CyberwareTheme.Accent, CyberwareTheme.AccentCyan, 0.45f)
                    : Color.Lerp(CyberwareTheme.Accent, CyberwareTheme.AccentCyan, 0.62f);
                Color lineColor = Color.Lerp(lowColor, highColor, waveLerp);
                lineColor *= alpha * brightness * (isPrimary ? 0.92f : 0.48f);

                Vector2 start = bodyOffset + new Vector2(segment.X1 * s, segment.Y1 * s + breathe);
                Vector2 end = bodyOffset + new Vector2(segment.X2 * s, segment.Y2 * s + breathe);

                if (isPrimary) {
                    Color outerGlow = lineColor * 0.16f;
                    outerGlow.A = 0;
                    CyberwareTheme.DrawLine(sb, px, start, end, segment.Thickness + 4f, outerGlow);

                    Color innerGlow = lineColor * 0.3f;
                    innerGlow.A = 0;
                    CyberwareTheme.DrawLine(sb, px, start, end, segment.Thickness + 1.8f, innerGlow);
                }
                else {
                    Color haze = lineColor * 0.16f;
                    haze.A = 0;
                    CyberwareTheme.DrawLine(sb, px, start, end, segment.Thickness + 0.9f, haze);
                }

                CyberwareTheme.DrawLine(sb, px, start, end, segment.Thickness, lineColor);

                Color coreColor = Color.Lerp(lineColor, CyberwareTheme.AccentCyan * alpha, isPrimary ? 0.45f : 0.22f);
                coreColor *= isPrimary ? 1f : 0.8f;
                CyberwareTheme.DrawLine(sb, px, start, end, Math.Max(0.8f, segment.Thickness * (isPrimary ? 0.36f : 0.5f)), coreColor);

                if (glow != null && isPrimary) {
                    Vector2 mid = Vector2.Lerp(start, end, 0.5f);
                    DrawGlowOrb(sb, glow, start, lineColor * 0.22f, 0.024f);
                    DrawGlowOrb(sb, glow, mid, lineColor * 0.16f, 0.02f);
                    DrawGlowOrb(sb, glow, end, lineColor * 0.22f, 0.024f);
                }
            }
        }

        private void DrawRibCageCurves(SpriteBatch sb, Texture2D px, Texture2D glow, Vector2 bodyOffset,
            float s, float alpha, float breathe, float globalTimer) {
            for (int i = 0; i < RibCageCurves.Length; i++) {
                CircuitCurve curve = RibCageCurves[i];
                float wave = MathF.Sin(energyFlowPhase - curve.MidY * 0.21f + curve.PulseOffset * 2.8f);
                float waveLerp = Math.Clamp(wave * 0.5f + 0.5f, 0f, 1f);
                float scanBoost = 1f + MathHelper.Clamp(1f - MathF.Abs(curve.MidY - scanLineY) / 3.9f, 0f, 1f) * 0.18f;
                float flicker = MathF.Sin(globalTimer * 1.6f + curve.PulseOffset * 4.6f) * 0.06f + 0.94f;
                Color lowColor = Color.Lerp(CyberwareTheme.BodyInner, CyberwareTheme.Accent, 0.18f);
                Color highColor = Color.Lerp(CyberwareTheme.Accent, CyberwareTheme.AccentGold, 0.08f);
                Color ribColor = Color.Lerp(lowColor, highColor, waveLerp) * (alpha * 0.24f * scanBoost * flicker);

                DrawCurvePath(sb, px, bodyOffset, s, breathe, curve, curve.Thickness + 0.65f, ribColor * 0.16f, 10, 0.36f, 0.24f);
                DrawCurvePath(sb, px, bodyOffset, s, breathe, curve, curve.Thickness, ribColor, 12, 0.18f, 0.08f);
                DrawCurvePath(sb, px, bodyOffset, s, breathe, curve, Math.Max(0.52f, curve.Thickness * 0.42f), Color.Lerp(ribColor, CyberwareTheme.AccentGold * alpha, 0.1f), 10, 0.14f, 0.08f);

                if (glow != null && i % 2 == 0) {
                    Vector2 tip = ToWorld(bodyOffset, s, breathe, curve.End);
                    DrawGlowOrb(sb, glow, tip, ribColor * 0.045f, 0.01f);
                }
            }
        }

        private void DrawCapillaryNetwork(SpriteBatch sb, Texture2D px, Vector2 bodyOffset, float s, float alpha, float breathe, float globalTimer) {
            for (int i = 0; i < CapillaryBranches.Length; i++) {
                CapillaryBranch branch = CapillaryBranches[i];
                float wave = MathF.Sin(energyFlowPhase * 1.15f - branch.MidY * 0.28f + branch.FlickerOffset * 5f);
                float flicker = MathF.Sin(globalTimer * 3.2f + branch.FlickerOffset * 8f) * 0.12f + 0.88f;
                float scanBoost = 1f + MathHelper.Clamp(1f - MathF.Abs(branch.MidY - scanLineY) / 3.75f, 0f, 1f) * 0.2f;
                float brightness = (wave * 0.16f + 0.62f) * flicker * scanBoost;

                Color branchColor = Color.Lerp(CyberwareTheme.BodyInner, CyberwareTheme.AccentCyan, Math.Clamp(wave * 0.5f + 0.45f, 0f, 1f));
                branchColor *= alpha * 0.26f * brightness;

                Vector2 start = bodyOffset + new Vector2(branch.X1 * s, branch.Y1 * s + breathe);
                Vector2 mid = bodyOffset + new Vector2(branch.X2 * s, branch.Y2 * s + breathe);
                Vector2 end = bodyOffset + new Vector2(branch.X3 * s, branch.Y3 * s + breathe);

                CyberwareTheme.DrawLine(sb, px, start, mid, 1f, branchColor);
                CyberwareTheme.DrawLine(sb, px, mid, end, 0.85f, branchColor * 0.82f);
            }
        }

        private void DrawCurveEnergyFlow(SpriteBatch sb, Texture2D px, Texture2D glow, Vector2 bodyOffset,
            float s, float alpha, float breathe, CircuitCurve curve,
            int particleCount, float speed, Color color, float size, float glowScale) {
            for (int i = 0; i < particleCount; i++) {
                float t = (energyFlowPhase / MathHelper.TwoPi * speed + curve.PulseOffset + i / (float)particleCount) % 1f;
                float trailT = t - 0.08f;
                if (trailT < 0f) {
                    trailT += 1f;
                }

                Vector2 worldPos = ToWorld(bodyOffset, s, breathe, EvaluateCubicBezier(t, curve));
                Vector2 worldTrailPos = ToWorld(bodyOffset, s, breathe, EvaluateCubicBezier(trailT, curve));

                float pulse = MathF.Sin((t + curve.PulseOffset) * MathHelper.TwoPi) * 0.28f + 0.72f;
                Color trailColor = color * (alpha * 0.18f * pulse);
                trailColor.A = 0;
                CyberwareTheme.DrawLine(sb, px, worldTrailPos, worldPos, size * 0.52f, trailColor);

                Color particleColor = Color.Lerp(color, CyberwareTheme.AccentCyan, 0.35f) * (alpha * 0.52f * pulse);
                particleColor.A = 0;
                sb.Draw(px, worldPos - new Vector2(size * 0.5f), new Rectangle(0, 0, 1, 1),
                    particleColor, 0f, Vector2.Zero, new Vector2(size), SpriteEffects.None, 0f);

                if (glow != null) {
                    Color glowColor = color * (alpha * 0.22f * pulse);
                    glowColor.A = 0;
                    sb.Draw(glow, worldPos, null, glowColor, 0f, glow.Size() / 2, glowScale, SpriteEffects.None, 0f);
                }
            }
        }

        private static void DrawCurvePath(SpriteBatch sb, Texture2D px, Vector2 bodyOffset, float s, float breathe,
            CircuitCurve curve, float thickness, Color color, int steps, float taperStart, float taperEnd) {
            Vector2 previous = ToWorld(bodyOffset, s, breathe, curve.Start);

            for (int i = 1; i <= steps; i++) {
                float t = i / (float)steps;
                Vector2 current = ToWorld(bodyOffset, s, breathe, EvaluateCubicBezier(t, curve));
                float segmentThickness = thickness * EvaluateCurveThickness(t, taperStart, taperEnd);
                CyberwareTheme.DrawLine(sb, px, previous, current, Math.Max(0.65f, segmentThickness), color);
                previous = current;
            }
        }

        private static void DrawCurveFilament(SpriteBatch sb, Texture2D px, Vector2 bodyOffset, float s, float breathe,
            CircuitCurve curve, float thickness, Color color, int steps, float taper, float lateralOffset) {
            Vector2 previous = OffsetCurvePoint(bodyOffset, s, breathe, curve, 0f, lateralOffset);

            for (int i = 1; i <= steps; i++) {
                float t = i / (float)steps;
                Vector2 current = OffsetCurvePoint(bodyOffset, s, breathe, curve, t, lateralOffset);
                float segmentThickness = thickness * EvaluateCurveThickness(t, taper, taper + 0.08f);
                CyberwareTheme.DrawLine(sb, px, previous, current, Math.Max(0.5f, segmentThickness), color);
                previous = current;
            }
        }

        private static Vector2 ToWorld(Vector2 bodyOffset, float s, float breathe, Vector2 gridPoint) {
            return bodyOffset + new Vector2(gridPoint.X * s, gridPoint.Y * s + breathe);
        }

        private static Vector2 EvaluateCubicBezier(float t, CircuitCurve curve) {
            float oneMinusT = 1f - t;
            float oneMinusTSquared = oneMinusT * oneMinusT;
            float tSquared = t * t;
            return curve.Start * (oneMinusTSquared * oneMinusT)
                + curve.Control1 * (3f * oneMinusTSquared * t)
                + curve.Control2 * (3f * oneMinusT * tSquared)
                + curve.End * (tSquared * t);
        }

        private static Vector2 EvaluateCubicBezierTangent(float t, CircuitCurve curve) {
            float oneMinusT = 1f - t;
            return (curve.Control1 - curve.Start) * (3f * oneMinusT * oneMinusT)
                + (curve.Control2 - curve.Control1) * (6f * oneMinusT * t)
                + (curve.End - curve.Control2) * (3f * t * t);
        }

        private static Vector2 OffsetCurvePoint(Vector2 bodyOffset, float s, float breathe, CircuitCurve curve, float t, float lateralOffset) {
            Vector2 point = EvaluateCubicBezier(t, curve);
            Vector2 tangent = EvaluateCubicBezierTangent(t, curve);
            if (tangent.LengthSquared() < 0.001f) {
                return ToWorld(bodyOffset, s, breathe, point);
            }

            tangent.Normalize();
            Vector2 normal = new Vector2(-tangent.Y, tangent.X);
            return ToWorld(bodyOffset, s, breathe, point) + normal * lateralOffset;
        }

        private static void DrawEllipticalArc(SpriteBatch sb, Texture2D px, Vector2 bodyOffset, float s, float breathe,
            Vector2 center, float radiusX, float radiusY, float startAngle, float endAngle, Color color, float thickness, int steps) {
            Vector2 previous = ToWorld(bodyOffset, s, breathe,
                center + new Vector2(MathF.Cos(startAngle) * radiusX, MathF.Sin(startAngle) * radiusY));

            for (int i = 1; i <= steps; i++) {
                float t = i / (float)steps;
                float angle = MathHelper.Lerp(startAngle, endAngle, t);
                Vector2 current = ToWorld(bodyOffset, s, breathe,
                    center + new Vector2(MathF.Cos(angle) * radiusX, MathF.Sin(angle) * radiusY));
                CyberwareTheme.DrawLine(sb, px, previous, current, thickness, color);
                previous = current;
            }
        }

        private static void DrawOrganicNode(SpriteBatch sb, Texture2D px, Texture2D glow, Vector2 center, Color coreColor,
            Color shellColor, float width, float height, float tilt) {
            Vector2 scale = new Vector2(width, height);
            sb.Draw(px, center, new Rectangle(0, 0, 1, 1), shellColor, tilt, new Vector2(0.5f), scale, SpriteEffects.None, 0f);
            sb.Draw(px, center + new Vector2(-0.35f * width, -0.12f * height), new Rectangle(0, 0, 1, 1),
                coreColor, tilt * 0.7f, new Vector2(0.5f), scale * new Vector2(0.46f, 0.5f), SpriteEffects.None, 0f);
            sb.Draw(px, center + new Vector2(0.18f * width, 0.14f * height), new Rectangle(0, 0, 1, 1),
                coreColor * 0.82f, tilt * 0.95f, new Vector2(0.5f), scale * new Vector2(0.34f, 0.38f), SpriteEffects.None, 0f);

            if (glow != null) {
                DrawGlowOrb(sb, glow, center, shellColor * 0.2f, Math.Max(width, height) / 160f);
                DrawGlowOrb(sb, glow, center + new Vector2(-0.12f * width, -0.08f * height), coreColor * 0.16f, Math.Max(width, height) / 220f);
            }
        }

        private static float EvaluateCurveThickness(float t, float taperStart, float taperEnd) {
            float bodyWeight = 1f;
            if (t < 0.5f) {
                bodyWeight -= MathHelper.Lerp(0f, taperStart, t / 0.5f);
            }
            else {
                bodyWeight -= MathHelper.Lerp(0f, taperEnd, (t - 0.5f) / 0.5f);
            }

            return MathHelper.Clamp(bodyWeight, 0.35f, 1.1f);
        }

        private static void DrawGlowOrb(SpriteBatch sb, Texture2D glow, Vector2 position, Color color, float scale) {
            color.A = 0;
            sb.Draw(glow, position, null, color, 0f, glow.Size() / 2, scale, SpriteEffects.None, 0f);
        }

        private static void DrawBodyFill(SpriteBatch sb, Texture2D px, Vector2 offset, float s, float alpha, float breathe) {
            Color fillColor = CyberwareTheme.BodyFill * (alpha * 0.6f);
            //头部（分层填充以匹配圆角轮廓）
            CyberwareTheme.FillGridRect(sb, px, offset, s, 13, 0, 6, 1, fillColor, breathe);  //颅顶窄
            CyberwareTheme.FillGridRect(sb, px, offset, s, 12, 1, 8, 5, fillColor, breathe);  //面部主体
            CyberwareTheme.FillGridRect(sb, px, offset, s, 13, 6, 6, 1, fillColor, breathe);  //上颌
            CyberwareTheme.FillGridRect(sb, px, offset, s, 14, 7, 4, 1, fillColor, breathe);  //下颌
            //颈部
            CyberwareTheme.FillGridRect(sb, px, offset, s, 14, 8, 4, 2, fillColor, breathe);
            //肩膀过渡（梯形近似）
            CyberwareTheme.FillGridRect(sb, px, offset, s, 13, 10, 6, 1, fillColor, breathe);  //锁骨
            CyberwareTheme.FillGridRect(sb, px, offset, s, 10, 11, 12, 1, fillColor, breathe); //肩中段
            CyberwareTheme.FillGridRect(sb, px, offset, s, 8, 12, 16, 2, fillColor, breathe);  //上胸
            //躯干（分段填充匹配缩短的躯干和腰部收窄）
            CyberwareTheme.FillGridRect(sb, px, offset, s, 10, 14, 12, 7, fillColor, breathe); //胸部 y14-21
            CyberwareTheme.FillGridRect(sb, px, offset, s, 11, 21, 10, 5, fillColor, breathe); //腰部 y21-26（窄）
            CyberwareTheme.FillGridRect(sb, px, offset, s, 10, 26, 12, 3, fillColor, breathe); //髋部 y26-29
            //左臂（分段填充：上臂+前臂+手）
            CyberwareTheme.FillGridRect(sb, px, offset, s, 5, 13, 3, 7, fillColor, breathe);   //上臂 y13-20
            CyberwareTheme.FillGridRect(sb, px, offset, s, 5, 20, 2, 10, fillColor, breathe);  //前臂 y20-30
            CyberwareTheme.FillGridRect(sb, px, offset, s, 4, 30, 5, 2, fillColor, breathe);   //手掌 y30-32
            //右臂
            CyberwareTheme.FillGridRect(sb, px, offset, s, 24, 13, 3, 7, fillColor, breathe);  //上臂
            CyberwareTheme.FillGridRect(sb, px, offset, s, 25, 20, 2, 10, fillColor, breathe); //前臂
            CyberwareTheme.FillGridRect(sb, px, offset, s, 24, 30, 5, 2, fillColor, breathe);  //手掌
            //左腿（分段：大腿+小腿+足）
            CyberwareTheme.FillGridRect(sb, px, offset, s, 10, 29, 5, 13, fillColor, breathe); //大腿+膝 y29-42
            CyberwareTheme.FillGridRect(sb, px, offset, s, 11, 42, 3, 10, fillColor, breathe); //小腿 y42-52
            CyberwareTheme.FillGridRect(sb, px, offset, s, 9, 52, 8, 4, fillColor, breathe);   //足部 y52-56
            //右腿
            CyberwareTheme.FillGridRect(sb, px, offset, s, 17, 29, 5, 13, fillColor, breathe); //大腿+膝
            CyberwareTheme.FillGridRect(sb, px, offset, s, 18, 42, 3, 10, fillColor, breathe); //小腿
            CyberwareTheme.FillGridRect(sb, px, offset, s, 16, 52, 8, 4, fillColor, breathe);  //足部
        }

        #endregion
    }
}