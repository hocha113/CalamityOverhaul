using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.Cyberwares.UIs
{
    /// <summary>
    ///赛博义体界面的槽位渲染器
    ///负责义体槽位的排布、交互检测、连接线绘制
    /// </summary>
    internal class CyberSlotRenderer
    {
        #region 槽位定义

        /// <summary>
        ///义体槽位的布局定义
        /// </summary>
        internal readonly struct SlotDef(float xRatio, float yRatio, int nodeIndex)
        {
            ///槽位相对面板的水平位置比例
            public readonly float XRatio = xRatio;
            ///槽位相对面板的垂直位置比例
            public readonly float YRatio = yRatio;
            ///连接到人体节点的索引
            public readonly int NodeIndex = nodeIndex;
            ///是否位于面板左侧
            public bool IsLeft => XRatio < 0.5f;
        }

        /// <summary>
        ///所有义体槽位的布局数据，左右对称各6个
        /// </summary>
        public static readonly SlotDef[] Definitions = [
            //左侧槽位
            new(0.04f, 0.08f,  0),   //额叶皮层 → 头部
            new(0.04f, 0.18f,  0),   //光学系统 → 头部(眼)
            new(0.04f, 0.36f,  2),   //左臂 → 左臂
            new(0.04f, 0.50f,  4),   //手部 → 左手
            new(0.04f, 0.68f,  7),   //左腿 → 左腿
            new(0.04f, 0.82f,  9),   //足部 → 左足
            //右侧槽位
            new(0.76f, 0.08f,  0),   //操作系统 → 头部
            new(0.76f, 0.18f,  1),   //神经系统 → 胸腔(脊椎)
            new(0.76f, 0.36f,  3),   //右臂 → 右臂
            new(0.76f, 0.50f,  1),   //循环系统 → 胸腔(心脏)
            new(0.76f, 0.68f,  6),   //骨骼 → 腰椎
            new(0.76f, 0.82f,  8),   //右腿 → 右腿
        ];

        private static readonly float[] ConnectorLaneFactors = [
            0.26f, 0.31f, 0.35f, 0.38f, 0.41f, 0.45f,
            0.26f, 0.31f, 0.35f, 0.38f, 0.41f, 0.45f,
        ];

        private static readonly Vector2[] ConnectorNodeOffsets = [
            new(-12f, -8f),   //0 额叶皮层 → 头部
            new(-10f, 4f),    //1 光学系统 → 头部
            new(-8f, -2f),    //2 左臂 → 左臂
            new(-7f, 3f),     //3 手部 → 左手
            new(-6f, -3f),    //4 左腿 → 左腿
            new(-5f, 3f),     //5 足部 → 左足
            new(11f, -5f),    //6 操作系统 → 头部
            new(8f, 4f),      //7 神经系统 → 胸腔(脊椎)
            new(8f, -2f),     //8 右臂 → 右臂
            new(8f, 2f),      //9 循环系统 → 胸腔(心脏)
            new(0f, -7f),     //10 骨骼 → 腰椎
            new(7f, 2f),      //11 右腿 → 右腿
        ];

        #endregion

        #region 状态

        private int hoveredSlot = -1;
        private int selectedSlot = -1;
        private readonly float[] slotHoverAnim = new float[12];
        //预分配的节点状态缓存，避免每帧分配
        private readonly int[] nodeStatesCache = new int[CyberBodyRenderer.NodeCount];

        public int HoveredSlot => hoveredSlot;
        public int SelectedSlot { get => selectedSlot; set => selectedSlot = value; }

        public int FocusedNodeIndex {
            get {
                if (selectedSlot >= 0) {
                    return Definitions[selectedSlot].NodeIndex;
                }

                return -1;
            }
        }

        public float FocusStrength {
            get {
                if (selectedSlot >= 0) {
                    return 1f;
                }

                return 0f;
            }
        }

        #endregion

        #region 公共方法

        /// <summary>
        ///检测鼠标悬停和点击交互，返回本帧是否发生了点击
        /// </summary>
        public bool UpdateInteraction(Rectangle panelRect) {
            bool clicked = false;
            hoveredSlot = -1;
            Vector2 mouse = new(Main.mouseX, Main.mouseY);

            for (int i = 0; i < Definitions.Length; i++) {
                Rectangle slotRect = GetSlotRect(i, panelRect);
                if (slotRect.Contains((int)mouse.X, (int)mouse.Y)) {
                    hoveredSlot = i;
                    Main.LocalPlayer.mouseInterface = true;
                    break;
                }
            }

            if (hoveredSlot >= 0 && Main.mouseLeft && Main.mouseLeftRelease) {
                selectedSlot = hoveredSlot == selectedSlot ? -1 : hoveredSlot;
                clicked = true;
            }

            return clicked;
        }

        /// <summary>
        ///更新槽位悬停动画的平滑过渡
        /// </summary>
        public void UpdateAnimations() {
            for (int i = 0; i < slotHoverAnim.Length; i++) {
                float target = i == hoveredSlot ? 1f : i == selectedSlot ? 0.6f : 0f;
                slotHoverAnim[i] += (target - slotHoverAnim[i]) * 0.15f;
            }
        }

        /// <summary>
        ///计算各节点的激活状态数组，0普通 1已连接 2高亮
        ///返回的数组为内部缓存引用，下次调用会覆盖内容
        /// </summary>
        public int[] ComputeNodeStates() {
            Array.Clear(nodeStatesCache);
            for (int i = 0; i < Definitions.Length; i++) {
                int ni = Definitions[i].NodeIndex;
                if (i == hoveredSlot || i == selectedSlot) {
                    nodeStatesCache[ni] = 2;
                }
                else if (nodeStatesCache[ni] < 1) {
                    nodeStatesCache[ni] = 1;
                }
            }
            return nodeStatesCache;
        }

        /// <summary>
        ///绘制所有义体槽位的背景、边框和文字标签
        /// </summary>
        public void DrawSlots(SpriteBatch sb, float alpha, Rectangle panelRect,
            string[] slotLabels, string selectedText, string emptyText, CyberwarePlayer cyberPlayer = null) {
            Texture2D px = CWRAsset.Placeholder_White?.Value;
            if (px == null) return;

            Texture2D slotGlow = CWRAsset.SoftGlow?.Value;

            for (int i = 0; i < Definitions.Length; i++) {
                Rectangle rect = GetSlotRect(i, panelRect);
                var def = Definitions[i];
                float hover = slotHoverAnim[i];
                bool isSelected = i == selectedSlot;
                bool isHovered = i == hoveredSlot;

                //槽位外层背景
                Color outerBg = Color.Lerp(CyberwareTheme.SlotEmpty, CyberwareTheme.BgDark, 0.3f) * (alpha * 0.9f);
                if (isHovered) outerBg = Color.Lerp(outerBg, CyberwareTheme.Accent, 0.06f * hover);
                if (isSelected) outerBg = Color.Lerp(outerBg, CyberwareTheme.Accent, 0.08f);
                sb.Draw(px, rect, new Rectangle(0, 0, 1, 1), outerBg);

                //内凹区域——比外层更暗，制造深度
                Rectangle innerArea = new(rect.X + 2, rect.Y + 2, rect.Width - 4, rect.Height - 4);
                Color innerBg = CyberwareTheme.SlotInnerBg * (alpha * 0.85f);
                if (isSelected) innerBg = Color.Lerp(innerBg, CyberwareTheme.Accent, 0.05f);
                sb.Draw(px, innerArea, new Rectangle(0, 0, 1, 1), innerBg);

                //内侧顶部+左侧阴影条——模拟凹陷
                sb.Draw(px, new Rectangle(innerArea.X, innerArea.Y, innerArea.Width, 2),
                    new Rectangle(0, 0, 1, 1), CyberwareTheme.InnerShadow * (alpha * 0.6f));
                sb.Draw(px, new Rectangle(innerArea.X, innerArea.Y, 2, innerArea.Height),
                    new Rectangle(0, 0, 1, 1), CyberwareTheme.InnerShadow * (alpha * 0.35f));

                //外边框
                Color borderColor = isSelected ? CyberwareTheme.Accent :
                    isHovered ? Color.Lerp(CyberwareTheme.SlotBorder, CyberwareTheme.Accent, hover * 0.6f) :
                    CyberwareTheme.SlotBorder;
                borderColor *= alpha;
                sb.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 1), new Rectangle(0, 0, 1, 1), borderColor);
                sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), new Rectangle(0, 0, 1, 1), borderColor * 0.6f);
                sb.Draw(px, new Rectangle(rect.X, rect.Y, 1, rect.Height), new Rectangle(0, 0, 1, 1), borderColor * 0.7f);
                sb.Draw(px, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), new Rectangle(0, 0, 1, 1), borderColor * 0.7f);

                //斜切角装饰
                int cutSz = 4;
                sb.Draw(px, new Rectangle(rect.X, rect.Y, cutSz, cutSz),
                    new Rectangle(0, 0, 1, 1), CyberwareTheme.BgPanel * alpha);
                CyberwareTheme.DrawLine(sb, px, new Vector2(rect.X, rect.Y + cutSz),
                    new Vector2(rect.X + cutSz, rect.Y), 1f, borderColor);
                sb.Draw(px, new Rectangle(rect.Right - cutSz, rect.Bottom - cutSz, cutSz, cutSz),
                    new Rectangle(0, 0, 1, 1), CyberwareTheme.BgPanel * alpha);
                CyberwareTheme.DrawLine(sb, px, new Vector2(rect.Right - cutSz, rect.Bottom),
                    new Vector2(rect.Right, rect.Bottom - cutSz), 1f, borderColor * 0.6f);

                //选中/悬停外发光
                if ((isSelected || hover > 0.3f) && slotGlow != null) {
                    float glowStr = isSelected ? 0.12f : hover * 0.06f;
                    Color sgColor = CyberwareTheme.Accent * (alpha * glowStr);
                    sgColor.A = 0;
                    sb.Draw(slotGlow, new Vector2(rect.Center.X, rect.Center.Y), null, sgColor, 0, slotGlow.Size() / 2,
                        new Vector2(rect.Width / 40f, rect.Height / 30f), SpriteEffects.None, 0);
                }

                //侧边强调条
                if (isSelected || hover > 0.01f) {
                    float barAlpha = isSelected ? 0.9f : hover * 0.5f;
                    Color barColor = CyberwareTheme.Accent * (alpha * barAlpha);
                    if (def.IsLeft) {
                        sb.Draw(px, new Rectangle(rect.X, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), barColor);
                    }
                    else {
                        sb.Draw(px, new Rectangle(rect.Right - 3, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), barColor);
                    }
                }

                //槽位标签
                Color labelColor = isSelected ? CyberwareTheme.Accent :
                    isHovered ? Color.Lerp(CyberwareTheme.TextDim, CyberwareTheme.TextBright, hover) :
                    CyberwareTheme.TextDim;
                labelColor *= alpha;
                float textX = rect.X + CyberwareTheme.SlotPadding + (def.IsLeft ? 4 : 0);
                string label = i < slotLabels.Length ? slotLabels[i] : "";
                Utils.DrawBorderString(sb, label, new Vector2(textX, rect.Y + 4), labelColor, 0.58f * CyberwareTheme.FontScale);

                //检查是否有装备的义体
                Item equippedItem = cyberPlayer?.EquippedCyberwares[i];
                bool hasEquipped = equippedItem != null && !equippedItem.IsAir;

                if (hasEquipped) {
                    //绘制已装备义体的图标
                    Texture2D itemTex = TextureAssets.Item[equippedItem.type]?.Value;
                    if (itemTex != null) {
                        float maxDim = Math.Max(itemTex.Width, itemTex.Height);
                        float iconScale = maxDim > 24 ? 24f / maxDim : 1f;
                        Vector2 iconPos = new(rect.X + rect.Width - 22, rect.Y + rect.Height / 2f);
                        sb.Draw(itemTex, iconPos, null, Color.White * alpha, 0f,
                            itemTex.Size() / 2f, iconScale, SpriteEffects.None, 0f);
                    }

                    //状态文字：显示义体名称
                    string itemName = equippedItem.Name ?? "???";
                    if (itemName.Length > 14) itemName = itemName[..13] + "…";
                    Color nameColor = CyberwareTheme.AccentGold * (alpha * 0.7f);
                    Utils.DrawBorderString(sb, itemName, new Vector2(textX, rect.Y + 26), nameColor, 0.50f * CyberwareTheme.FontScale);
                }
                else {
                    //状态文字
                    string statusStr = isSelected ? selectedText : emptyText;
                    Color statusColor = isSelected ? CyberwareTheme.AccentGold : CyberwareTheme.TextDim;
                    statusColor *= alpha * 0.6f;
                    Utils.DrawBorderString(sb, statusStr, new Vector2(textX, rect.Y + 26), statusColor, 0.52f * CyberwareTheme.FontScale);
                }
            }
        }

        /// <summary>
        ///绘制各槽位到人体节点之间的折线连接和流动光点
        /// </summary>
        public void DrawConnectors(SpriteBatch sb, float alpha, Rectangle panelRect,
            CyberBodyRenderer body, Vector2 bodyOrigin, float dataStreamPhase) {
            Texture2D px = CWRAsset.Placeholder_White?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (px == null) return;

            float focusVisualStrength = body.FocusVisualStrength;
            float passiveHideStrength = MathHelper.Clamp((focusVisualStrength - 0.08f) / 0.22f, 0f, 1f);

            for (int i = 0; i < Definitions.Length; i++) {
                var def = Definitions[i];
                Rectangle slotRect = GetSlotRect(i, panelRect);

                //连线起点（槽位靠内侧边缘）
                Vector2 slotEdge = def.IsLeft
                    ? new Vector2(slotRect.Right, slotRect.Center.Y)
                    : new Vector2(slotRect.Left, slotRect.Center.Y);

                //连线终点（人体节点位置）
                Vector2 nodePos = body.GetNodeWorldPosition(def.NodeIndex, bodyOrigin);
                Vector2 nodeApproach = nodePos + ConnectorNodeOffsets[i];

                bool isActive = i == hoveredSlot || i == selectedSlot;
                float hover = slotHoverAnim[i];
                float passiveAlpha = MathHelper.Lerp(0.11f, 0f, passiveHideStrength);
                float lineAlpha = isActive ? 0.72f : passiveAlpha;
                if (!isActive && lineAlpha <= 0.002f) {
                    continue;
                }

                Color lineColor = isActive
                    ? Color.Lerp(CyberwareTheme.Accent, CyberwareTheme.AccentGold, 0.14f)
                    : Color.Lerp(CyberwareTheme.Connector, CyberwareTheme.Accent, 0.18f);
                lineColor *= alpha * lineAlpha;

                //分道折线路径：水平出线→独立竖向通道→节点前汇入
                float laneFactor = ConnectorLaneFactors[i];
                float midX = MathHelper.Lerp(slotEdge.X, nodeApproach.X, laneFactor);
                float laneSpread = (i % 6 - 2.5f) * (def.IsLeft ? -2.6f : 2.6f);
                midX += laneSpread;
                float midY = MathHelper.Lerp(slotEdge.Y, nodeApproach.Y, 0.52f) + (i % 2 == 0 ? -4f : 4f);

                Vector2 p1 = slotEdge;
                Vector2 p2 = new(midX, slotEdge.Y);
                Vector2 p3 = new(midX, midY);
                Vector2 p4 = nodeApproach;
                Vector2 p5 = nodePos;

                Color sheathColor = Color.Lerp(CyberwareTheme.BodyInner, lineColor, isActive ? 0.38f : 0.22f) * (isActive ? 0.34f : 0.18f);
                Color coreColor = Color.Lerp(lineColor, CyberwareTheme.AccentCyan * alpha, isActive ? 0.24f : 0.08f);
                Color tracerColor = Color.Lerp(lineColor, CyberwareTheme.AccentGold * alpha, isActive ? 0.22f : 0.06f);

                DrawConnectorSegment(sb, px, p1, p2, sheathColor, coreColor, tracerColor, isActive ? 1.9f : 1.3f);
                DrawConnectorSegment(sb, px, p2, p3, sheathColor, coreColor, tracerColor, isActive ? 1.9f : 1.3f);
                DrawConnectorSegment(sb, px, p3, p4, sheathColor, coreColor, tracerColor, isActive ? 1.9f : 1.3f);
                DrawConnectorSegment(sb, px, p4, p5, sheathColor * 0.9f, coreColor, tracerColor, isActive ? 1.7f : 1.15f);

                float sideOffset = def.IsLeft ? 3f : -3f;
                Color guideColor = Color.Lerp(CyberwareTheme.Connector, CyberwareTheme.AccentGold, 0.08f) * (alpha * (isActive ? 0.16f : 0.03f));
                DrawOffsetGuide(sb, px, p1, p2, new Vector2(0f, sideOffset * 0.35f), guideColor);
                DrawOffsetGuide(sb, px, p2, p3, new Vector2(sideOffset * 0.2f, 0f), guideColor * 0.9f);
                DrawOffsetGuide(sb, px, p3, p4, new Vector2(0f, -sideOffset * 0.3f), guideColor * 0.8f);
                DrawOffsetGuide(sb, px, p4, p5, new Vector2(sideOffset * 0.18f, 0f), guideColor * 0.65f);

                //沿连接线绘制多颗数据脉冲
                int packetCount = isActive ? 3 : passiveHideStrength < 0.35f ? 1 : 0;
                for (int packet = 0; packet < packetCount; packet++) {
                    float packetPhase = (dataStreamPhase / MathHelper.TwoPi + packet / (float)packetCount + i * 0.037f) % 1f;
                    Vector2 flowPos = EvaluateConnectorPath(packetPhase, p1, p2, p3, p4, p5);
                    Vector2 trailPos = EvaluateConnectorPath((packetPhase + 0.9f) % 1f, p1, p2, p3, p4, p5);
                    Color packetColor = Color.Lerp(CyberwareTheme.Accent, CyberwareTheme.AccentGold, 0.18f) * (alpha * (isActive ? 0.58f : 0.12f));
                    packetColor.A = 0;
                    CyberwareTheme.DrawLine(sb, px, trailPos, flowPos, isActive ? 1.5f : 0.9f, packetColor * (isActive ? 0.42f : 0.26f));
                    sb.Draw(px, flowPos, new Rectangle(0, 0, 1, 1), packetColor,
                        MathHelper.PiOver4, new Vector2(0.5f), isActive ? 3.2f : 1.8f, SpriteEffects.None, 0f);

                    if (glow != null) {
                        sb.Draw(glow, flowPos, null, packetColor, 0f, glow.Size() / 2,
                            isActive ? 0.032f : 0.012f, SpriteEffects.None, 0f);
                    }
                }

                //折线拐角的中继节点
                Color relayColor = Color.Lerp(lineColor, CyberwareTheme.AccentGold * alpha, isActive ? 0.22f : 0.08f);
                DrawRelayJunction(sb, px, glow, p2, relayColor, alpha, isActive, dataStreamPhase + i * 0.11f);
                DrawRelayJunction(sb, px, glow, p3, relayColor, alpha, isActive, dataStreamPhase + i * 0.16f);
                DrawRelayJunction(sb, px, glow, p4, relayColor * 0.9f, alpha, isActive, dataStreamPhase + i * 0.2f);

                //槽位端与人体端的瞄准锁定标记
                DrawReticleEndpoint(sb, px, glow, p1, def.IsLeft ? 1f : -1f, lineColor, alpha, isActive, hover, dataStreamPhase + i * 0.13f, true);
                DrawReticleEndpoint(sb, px, glow, p5, def.IsLeft ? -1f : 1f, lineColor, alpha, isActive, hover, dataStreamPhase + i * 0.17f, false);
            }
        }

        private static Vector2 EvaluateConnectorPath(float t, Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, Vector2 p5) {
            float d12 = Vector2.Distance(p1, p2);
            float d23 = Vector2.Distance(p2, p3);
            float d34 = Vector2.Distance(p3, p4);
            float d45 = Vector2.Distance(p4, p5);
            float total = d12 + d23 + d34 + d45;
            if (total <= 0.001f) {
                return p1;
            }

            float dist = t * total;
            if (dist <= d12) {
                return Vector2.Lerp(p1, p2, d12 <= 0.001f ? 0f : dist / d12);
            }

            dist -= d12;
            if (dist <= d23) {
                return Vector2.Lerp(p2, p3, d23 <= 0.001f ? 0f : dist / d23);
            }

            dist -= d23;
            if (dist <= d34) {
                return Vector2.Lerp(p3, p4, d34 <= 0.001f ? 0f : dist / d34);
            }

            dist -= d34;
            return Vector2.Lerp(p4, p5, d45 <= 0.001f ? 0f : Math.Clamp(dist / d45, 0f, 1f));
        }

        private static void DrawConnectorSegment(SpriteBatch sb, Texture2D px, Vector2 start, Vector2 end,
            Color sheathColor, Color coreColor, Color tracerColor, float thickness) {
            sheathColor.A = 0;
            CyberwareTheme.DrawLine(sb, px, start, end, thickness + 1.6f, sheathColor);
            CyberwareTheme.DrawLine(sb, px, start, end, thickness, coreColor);
            CyberwareTheme.DrawLine(sb, px, start, end, Math.Max(0.7f, thickness * 0.42f), tracerColor);
        }

        private static void DrawOffsetGuide(SpriteBatch sb, Texture2D px, Vector2 start, Vector2 end, Vector2 offset, Color color) {
            CyberwareTheme.DrawLine(sb, px, start + offset, end + offset, 0.7f, color);
        }

        private static void DrawRelayJunction(SpriteBatch sb, Texture2D px, Texture2D glow, Vector2 position,
            Color color, float alpha, bool isActive, float phase) {
            Color junctionColor = color * (isActive ? 0.82f : 0.42f);
            sb.Draw(px, position, new Rectangle(0, 0, 1, 1), junctionColor,
                MathHelper.PiOver4, new Vector2(0.5f), isActive ? 3f : 2f, SpriteEffects.None, 0f);

            float tickOffset = MathF.Sin(phase * 5.5f) * (isActive ? 4f : 2.5f);
            CyberwareTheme.DrawLine(sb, px,
                position + new Vector2(-2f, tickOffset * 0.2f),
                position + new Vector2(2f, tickOffset * 0.2f),
                0.8f, junctionColor * 0.7f);

            if (glow != null) {
                Color glowColor = junctionColor * (isActive ? 0.18f : 0.08f);
                glowColor.A = 0;
                sb.Draw(glow, position, null, glowColor, 0f, glow.Size() / 2,
                    isActive ? 0.018f : 0.012f, SpriteEffects.None, 0f);
            }
        }

        private static void DrawReticleEndpoint(SpriteBatch sb, Texture2D px, Texture2D glow, Vector2 center,
            float horizontalDirection, Color baseColor, float alpha, bool isActive, float hover, float phase, bool isSlotSide) {
            float stateStrength = isActive ? 1f : Math.Max(0.18f, hover * 0.55f + 0.2f);
            Color reticleColor = Color.Lerp(baseColor, CyberwareTheme.AccentGold * alpha, isSlotSide ? 0.08f : 0.18f) * stateStrength;
            float radius = isSlotSide ? 8f : 9f;
            float sweep = phase / MathHelper.TwoPi % 1f;
            float bracketLen = isSlotSide ? 3.5f : 4.5f;
            float bracketInset = isSlotSide ? 2.5f : 3f;

            DrawReticleBracket(sb, px, center + new Vector2(horizontalDirection * radius, -radius * 0.7f), horizontalDirection, -1f, bracketLen, reticleColor * 0.72f);
            DrawReticleBracket(sb, px, center + new Vector2(horizontalDirection * radius, radius * 0.7f), horizontalDirection, 1f, bracketLen, reticleColor * 0.72f);
            DrawReticleBracket(sb, px, center + new Vector2(-horizontalDirection * bracketInset, -radius), -horizontalDirection, -1f, bracketLen * 0.85f, reticleColor * 0.48f);
            DrawReticleBracket(sb, px, center + new Vector2(-horizontalDirection * bracketInset, radius), -horizontalDirection, 1f, bracketLen * 0.85f, reticleColor * 0.48f);

            Color coreColor = Color.Lerp(reticleColor, CyberwareTheme.AccentCyan * alpha, isSlotSide ? 0.08f : 0.16f);
            sb.Draw(px, center, new Rectangle(0, 0, 1, 1), coreColor,
                MathHelper.PiOver4, new Vector2(0.5f), isSlotSide ? 2.6f : 3.2f, SpriteEffects.None, 0f);

            Vector2 sweepStart = center + new Vector2(horizontalDirection * (radius - 1f), -radius + sweep * radius * 2f);
            CyberwareTheme.DrawLine(sb, px, sweepStart + new Vector2(0f, -2f), sweepStart + new Vector2(0f, 2f), 0.8f, reticleColor * 0.82f);

            if (!isSlotSide) {
                CyberwareTheme.DrawLine(sb, px,
                    center + new Vector2(-2.6f, 0f),
                    center + new Vector2(2.6f, 0f),
                    0.85f, reticleColor * 0.4f);
            }

            if (glow != null) {
                Color glowColor = reticleColor * (isActive ? 0.18f : 0.08f);
                glowColor.A = 0;
                sb.Draw(glow, center, null, glowColor, 0f, glow.Size() / 2,
                    isSlotSide ? 0.018f : 0.024f, SpriteEffects.None, 0f);
            }
        }

        private static void DrawReticleBracket(SpriteBatch sb, Texture2D px, Vector2 corner,
            float xDir, float yDir, float length, Color color) {
            CyberwareTheme.DrawLine(sb, px, corner, corner + new Vector2(-xDir * length, 0f), 0.85f, color);
            CyberwareTheme.DrawLine(sb, px, corner, corner + new Vector2(0f, -yDir * length), 0.85f, color);
        }

        /// <summary>
        ///获取指定槽位的屏幕矩形区域
        /// </summary>
        public Rectangle GetSlotRect(int index, Rectangle panelRect) {
            var def = Definitions[index];
            float slotW = CyberwareTheme.PanelWidth * 0.20f;
            int x = panelRect.X + (int)(def.XRatio * panelRect.Width);
            int y = panelRect.Y + (int)(def.YRatio * panelRect.Height);
            return new Rectangle(x, y, (int)slotW, (int)CyberwareTheme.SlotSize);
        }

        #endregion
    }
}
