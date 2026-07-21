using CalamityOverhaul.Common;
using CalamityOverhaul.Content.RAMSystems;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.HackTimes
{
    /// <summary>右侧协议旗标列，尖端指向屏幕中心目标</summary>
    internal class HackPanelRenderer
    {
        #region 状态字段

        private float[] slotFlyIn;//0..1
        private float[] slotHoverAnim;
        private float[] slotGlitchSeed;
        //悬停检测用
        private Rectangle[] slotRects;
        //分组标题造成的纵偏
        private float[] slotYOffset;
        private bool[] slotGroupHead;
        private int hoveredSlot = -1;
        public bool HasHoveredSlot => hoveredSlot >= 0;
        /// <summary>悬停协议实际 RAM，无悬停为 0，弧预扣用</summary>
        public int HoveredCostPreview { get; private set; }
        private float timer;
        private bool visible;
        internal HackQueueRenderer Queue;
        //Show 时重置
        private float revealTime;
        private float glitchBandY;
        private float glitchBandCooldown;
        //类别分组后显示序 → 全局协议索引
        private readonly List<int> displayIndices = [];
        private int displayCount;

        #endregion

        #region 排版常量

        private const float RowWidth = 340f;
        private const float RowHeight = 48f;
        private const float RowGap = 5f;
        //分组标题额外占高
        private const float GroupGap = 24f;
        private const float RightMargin = 36f;
        //旗标左端斜切宽（尖端指向屏幕中心）
        private const float TaperWidth = 12f;
        //成本大格宽
        private const float CostCellWidth = 46f;
        //电路树主干左偏移
        private const float TrunkOffsetX = 44f;
        //首条目前延迟（秒）
        private const float BaseEntryDelay = 0.2f;
        //条目飞入间隔（秒）
        private const float EntryStagger = 0.06f;
        //列表下移，避让 RAM HUD
        private const float TopPadding = 60f;
        //详情页脚高度
        private const float FooterHeight = 92f;
        //MouseText 中文不低于 0.5，否则糊
        private static float FontName => 0.80f;
        private static float FontCost => 0.88f;
        private static float FontDesc => 0.72f;
        private static float FontTime => 0.56f;
        private static float FontMicro => 0.50f;
        private static float FontGroup => 0.58f;

        //解码乱码字符池
        private const string ScrambleChars = "0123456789ABCDEF#$%&";

        #endregion

        #region 生命周期

        public void Show(HackTargetKind targetKind = HackTargetKind.Npc) {
            //先取过滤集，再按类别分组排序
            List<int> filtered = [];
            QuickHackDef.GetFilteredIndices(targetKind, filtered);

            displayIndices.Clear();
            foreach (QuickHackCategory cat in Enum.GetValues<QuickHackCategory>()) {
                for (int i = 0; i < filtered.Count; i++) {
                    var hack = QuickHackDef.GetByIndex(filtered[i]);
                    if (hack != null && hack.Category == cat)
                        displayIndices.Add(filtered[i]);
                }
            }
            displayCount = displayIndices.Count;
            if (displayCount == 0) {
                Hide();
                return;
            }

            if (slotFlyIn == null || slotFlyIn.Length != displayCount) {
                slotFlyIn = new float[displayCount];
                slotHoverAnim = new float[displayCount];
                slotGlitchSeed = new float[displayCount];
                slotRects = new Rectangle[displayCount];
                slotYOffset = new float[displayCount];
                slotGroupHead = new bool[displayCount];
            }

            //预计算分组标题偏移
            float acc = 0f;
            QuickHackCategory? lastCat = null;
            for (int i = 0; i < displayCount; i++) {
                var hack = QuickHackDef.GetByIndex(displayIndices[i]);
                bool newGroup = lastCat == null || hack.Category != lastCat.Value;
                slotGroupHead[i] = newGroup;
                if (newGroup) acc += GroupGap;
                slotYOffset[i] = acc;
                lastCat = hack.Category;
            }

            visible = true;
            hoveredSlot = -1;
            HoveredCostPreview = 0;
            revealTime = 0f;
            glitchBandY = -100f;
            glitchBandCooldown = 0.5f;
            Array.Clear(slotFlyIn);
            Array.Clear(slotHoverAnim);
            for (int i = 0; i < slotGlitchSeed.Length; i++)
                slotGlitchSeed[i] = Main.rand.NextFloat() * 100f;
        }

        public void Hide() {
            visible = false;
            hoveredSlot = -1;
            HoveredCostPreview = 0;
            //队列生命周期独立于面板，CWRWorld 全局驱动
        }

        public void CancelUpload() {
            Queue?.Clear();
        }

        #endregion

        #region 更新

        public void Update() {
            timer += 0.016f;

            if (!visible) {
                HoveredCostPreview = 0;
                if (slotFlyIn == null) return;
                for (int i = 0; i < slotFlyIn.Length; i++)
                    slotFlyIn[i] = MathHelper.Lerp(slotFlyIn[i], 0f, 0.15f);
                revealTime = Math.Max(revealTime - 0.032f, 0f);
                return;
            }

            revealTime += 0.016f;

            //各条目依次飞入
            for (int i = 0; i < slotFlyIn.Length; i++) {
                float delay = BaseEntryDelay + i * EntryStagger;
                float elapsed = revealTime - delay;
                if (elapsed <= 0f) continue;
                float speed = 0.1f + elapsed * 0.25f;
                slotFlyIn[i] = MathHelper.Lerp(slotFlyIn[i], 1f, Math.Min(speed, 0.22f));
                if (slotFlyIn[i] > 0.995f) slotFlyIn[i] = 1f;
            }

            //故障带下移
            glitchBandCooldown -= 0.016f;
            if (glitchBandCooldown <= 0f) {
                glitchBandY += 600f * 0.016f;
                float totalH = GetListHeight();
                float startY = GetListStartY(totalH);
                if (glitchBandY > startY + totalH + 60f) {
                    glitchBandY = startY - 50f;
                    glitchBandCooldown = 2f + Main.rand.NextFloat() * 3f;
                }
            }

            UpdateHover();
        }

        private void UpdateHover() {
            hoveredSlot = -1;
            HoveredCostPreview = 0;
            int mx = Main.mouseX;
            int my = Main.mouseY;
            for (int i = 0; i < slotRects.Length; i++) {
                if (slotFlyIn[i] < 0.8f) continue;
                if (slotRects[i].Contains(mx, my)) {
                    //禁用槽位不响应悬停
                    int globalIdx = GetGlobalIndex(i);
                    var hack = QuickHackDef.GetByIndex(globalIdx);
                    var qs = Queue?.GetSlotState(globalIdx, HackTime.CurrentScanTarget) ?? QueueSlotState.None;
                    bool disabled = hack != null && !RamSystem.CanAfford(hack.RamCost)
                        || qs != QueueSlotState.None;
                    if (!disabled) {
                        hoveredSlot = i;
                        if (hack != null)
                            HoveredCostPreview = HackCostEvaluator.GetActualCost(hack, HackTime.CurrentScanTarget);
                    }
                    break;
                }
            }

            for (int i = 0; i < slotHoverAnim.Length; i++) {
                float target = 0f;
                int gi = GetGlobalIndex(i);
                if (i == hoveredSlot) target = 1f;
                else if (Queue != null && Queue.GetSlotState(gi, HackTime.CurrentScanTarget) == QueueSlotState.Uploading) target = 0.5f;
                slotHoverAnim[i] = MathHelper.Lerp(slotHoverAnim[i], target, 0.2f);
            }
        }

        //槽位索引到协议全局索引
        private int GetGlobalIndex(int displaySlot) {
            if (displaySlot >= 0 && displaySlot < displayIndices.Count)
                return displayIndices[displaySlot];
            return -1;
        }

        public void HandleClick() {
            if (!visible) return;
            if (hoveredSlot < 0 || Queue == null) return;

            int globalIdx = GetGlobalIndex(hoveredSlot);
            var hack = QuickHackDef.GetByIndex(globalIdx);
            if (hack == null) return;

            IHackTarget target = HackTime.CurrentScanTarget;
            if (target == null) return;
            if ((hack.SupportedTargets & target.TargetType.Kind) == 0) return;

            int actualCost = HackCostEvaluator.GetActualCost(hack, target);
            if (!RamSystem.CanAfford(actualCost)) return;

            bool enqueued = Queue.Enqueue(hack, globalIdx, target, actualCost);
            if (enqueued) {
                RamSystem.TryConsume(actualCost);
            }
        }

        public bool ContainsMouse() {
            if (!visible || slotRects == null || slotFlyIn == null) return false;
            int mx = Main.mouseX;
            int my = Main.mouseY;
            for (int i = 0; i < slotRects.Length; i++) {
                if (slotFlyIn[i] < 0.5f) continue;
                if (slotRects[i].Contains(mx, my)) return true;
            }
            return false;
        }

        #endregion

        #region 布局计算

        //列表总高（含分组标题）
        private float GetListHeight() {
            if (displayCount <= 0) return 0f;
            return displayCount * (RowHeight + RowGap) - RowGap + slotYOffset[displayCount - 1];
        }

        private float GetListStartY(float totalH) {
            return (Main.screenHeight - totalH - FooterHeight) * 0.5f + TopPadding;
        }

        private float GetBaseX() => Main.screenWidth - RightMargin - RowWidth;

        private float GetRowY(float startY, int i) {
            return startY + i * (RowHeight + RowGap) + slotYOffset[i];
        }

        #endregion

        #region 主绘制入口

        public void Draw(SpriteBatch sb) {
            HackTargetFrame.Draw(sb, timer);

            Texture2D px = HackTheme.Pixel;
            if (px == null) return;
            float alpha = HackTime.Intensity;
            if (alpha < 0.01f) return;
            if (slotFlyIn == null || displayCount == 0) return;

            float totalH = GetListHeight();
            float startY = GetListStartY(totalH);
            float baseX = GetBaseX();

            DrawAmbientNoise(sb, px, alpha, baseX, startY, totalH);
            DrawConnectorTree(sb, px, alpha, baseX, startY, totalH);

            //行几何与状态快照
            if (rowStates == null || rowStates.Length != displayCount)
                rowStates = new RowState[displayCount];
            RowState[] rows = rowStates;
            BuildRowStates(rows, baseX, startY);

            //行背景，着色器优先，缺则 CPU 旗标
            Effect deck = EffectLoader.HackDeckPanel?.Value;
            if (deck != null) {
                DrawRowBackgroundsShader(sb, px, deck, rows, alpha);
            }
            else {
                DrawRowBackgroundsCPU(sb, rows, alpha);
            }

            //行前景
            for (int i = 0; i < displayCount; i++) {
                if (rows[i].Skip) continue;
                DrawRowForeground(sb, px, alpha, i, in rows[i]);
            }

            //分组微标题
            DrawGroupHeaders(sb, alpha, baseX, startY);

            DrawGlitchBand(sb, px, alpha, baseX);
            DrawFooter(sb, px, alpha, baseX, startY + totalH + 14f);
        }

        #endregion

        #region 行状态快照

        private struct RowState
        {
            public Rectangle Rect;
            public QuickHackDef Hack;
            public float Fly;
            public float Hover;
            public float Glitch;
            public QueueSlotState QueueState;
            public float QueueProgress;
            public bool Disabled;
            public bool Skip;
            public Color AccentColor;
        }

        //行状态快照池
        private RowState[] rowStates;

        private void BuildRowStates(RowState[] rows, float baseX, float startY) {
            for (int i = 0; i < displayCount; i++) {
                ref RowState rs = ref rows[i];
                rs = default;
                float fly = slotFlyIn[i];
                if (fly < 0.01f) {
                    slotRects[i] = Rectangle.Empty;
                    rs.Skip = true;
                    continue;
                }

                int globalIdx = GetGlobalIndex(i);
                rs.Hack = QuickHackDef.GetByIndex(globalIdx);
                if (rs.Hack == null) {
                    slotRects[i] = Rectangle.Empty;
                    rs.Skip = true;
                    continue;
                }

                rs.Fly = fly;
                rs.Hover = slotHoverAnim[i];
                rs.QueueState = Queue?.GetSlotState(globalIdx, HackTime.CurrentScanTarget) ?? QueueSlotState.None;
                rs.QueueProgress = Queue?.GetSlotProgress(globalIdx, HackTime.CurrentScanTarget) ?? 0f;
                rs.Disabled = !RamSystem.CanAfford(rs.Hack.RamCost) || rs.QueueState is QueueSlotState.Uploading or QueueSlotState.Queued;
                if (rs.Disabled && rs.QueueState == QueueSlotState.None) rs.Hover = 0f;

                //飞入偏移（弹性过冲）+ 故障抖动
                float flyOffset = (1f - HackTheme.EaseOutBack(fly)) * 400f;
                rs.Glitch = 0f;
                if (fly < 0.85f) {
                    float seed = slotGlitchSeed[i] + timer * 25f;
                    rs.Glitch = (MathF.Sin(seed) + MathF.Sin(seed * 2.7f) * 0.5f) * (1f - fly);
                }

                float y = GetRowY(startY, i);
                //悬停向屏幕中心（左）扩展
                float hoverExpand = rs.Hover * 14f;
                float x = baseX + flyOffset + rs.Glitch * 16f - hoverExpand;
                Rectangle rect = new((int)x, (int)y, (int)(RowWidth + hoverExpand), (int)RowHeight);
                slotRects[i] = rect;
                rs.Rect = rect;

                //整行主色由状态决定
                rs.AccentColor = ResolveRowAccent(in rs, i);
            }
        }

        //红不可用、琥珀队列/上传、主题色可用
        private Color ResolveRowAccent(in RowState rs, int index) {
            Color accent;
            if (rs.QueueState == QueueSlotState.Uploading) accent = HackTheme.Uploading;
            else if (rs.QueueState == QueueSlotState.Queued) accent = Color.Lerp(HackTheme.Uploading, HackTheme.BgSlotHover, 0.35f);
            else if (rs.QueueState == QueueSlotState.Completed) accent = HackTheme.Accent;
            else if (rs.Disabled) accent = HackTheme.Danger;
            else accent = HackTheme.Accent;

            //无限骇入全红闪
            if (HackTime.InfiniteHack) {
                float rFlicker = MathF.Sin(timer * 15f + slotGlitchSeed[index] * 3f) * 0.35f
                    + MathF.Sin(timer * 23f + slotGlitchSeed[index] * 7f) * 0.15f + 0.5f;
                accent = Color.Lerp(accent, HackTheme.Danger, 0.55f + rFlicker * 0.25f);
            }
            return accent;
        }

        #endregion

        #region 行背景

        private void DrawRowBackgroundsShader(SpriteBatch sb, Texture2D px, Effect deck, Span<RowState> rows, float alpha) {
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, deck, Main.UIScaleMatrix);

            for (int i = 0; i < displayCount; i++) {
                ref RowState rs = ref rows[i];
                if (rs.Skip) continue;

                float rowAlpha = alpha * Math.Min(rs.Fly * 2.5f, 1f);
                deck.Parameters["uTime"]?.SetValue(timer + slotGlitchSeed[i]);
                deck.Parameters["uAlpha"]?.SetValue(rowAlpha);
                deck.Parameters["uResolution"]?.SetValue(new Vector2(rs.Rect.Width, rs.Rect.Height));
                deck.Parameters["uTaperLeft"]?.SetValue(TaperWidth);
                deck.Parameters["uTaperRight"]?.SetValue(0f);
                deck.Parameters["uAccent"]?.SetValue(rs.AccentColor.ToVector3());
                deck.Parameters["uHover"]?.SetValue(rs.Hover);
                deck.Parameters["uDisabled"]?.SetValue(rs.Disabled && rs.QueueState == QueueSlotState.None ? 1f : 0f);
                deck.Parameters["uProgress"]?.SetValue(rs.QueueState == QueueSlotState.Uploading ? rs.QueueProgress : 0f);
                deck.Parameters["uGlitch"]?.SetValue(Math.Abs(rs.Glitch));
                deck.CurrentTechnique.Passes[0].Apply();
                sb.Draw(px, rs.Rect, Color.White);
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);
        }

        private void DrawRowBackgroundsCPU(SpriteBatch sb, Span<RowState> rows, float alpha) {
            for (int i = 0; i < displayCount; i++) {
                ref RowState rs = ref rows[i];
                if (rs.Skip) continue;
                float rowAlpha = alpha * Math.Min(rs.Fly * 2.5f, 1f);

                Color bg = Color.Lerp(HackTheme.BgSlot, HackTheme.BgSlotHover, rs.Hover * 0.6f);
                if (rs.Disabled && rs.QueueState == QueueSlotState.None)
                    bg = Color.Lerp(HackTheme.BgDarkest, new Color(45, 8, 8), 0.4f);
                else if (rs.QueueState == QueueSlotState.Uploading)
                    bg = Color.Lerp(bg, HackTheme.Uploading, 0.08f);

                HackTheme.DrawPennantFill(sb, rs.Rect, TaperWidth, 0f, bg * (rowAlpha * 0.92f));
                HackTheme.DrawCRTOverlay(sb, rs.Rect, rowAlpha * 0.05f);

                //上传进度填充
                if (rs.QueueState == QueueSlotState.Uploading && rs.QueueProgress > 0.01f) {
                    Rectangle fill = rs.Rect;
                    fill.Width = (int)(rs.Rect.Width * rs.QueueProgress);
                    HackTheme.DrawPennantFill(sb, fill, TaperWidth, 0f, rs.AccentColor * (rowAlpha * 0.12f));
                }
            }
        }

        #endregion

        #region 行前景

        private void DrawRowForeground(SpriteBatch sb, Texture2D px, float alpha, int index, in RowState rs) {
            Rectangle rect = rs.Rect;
            float rowAlpha = alpha * Math.Min(rs.Fly * 2.5f, 1f);
            Color accent = rs.AccentColor;
            bool idleDisabled = rs.Disabled && rs.QueueState == QueueSlotState.None;

            //---- 成本大格（左端，指向目标） ----
            Rectangle costCell = new(rect.X + (int)TaperWidth, rect.Y, (int)CostCellWidth, rect.Height);
            //悬停反色
            if (rs.Hover > 0.35f) {
                sb.Draw(px, costCell, HackTheme.SrcPixel, accent * (rowAlpha * 0.85f * rs.Hover));
            }
            else {
                //格子右侧分隔细线
                sb.Draw(px, new Rectangle(costCell.Right, rect.Y + 4, 1, rect.Height - 8),
                    HackTheme.SrcPixel, accent * (rowAlpha * 0.30f));
            }

            int actualCost = HackCostEvaluator.GetActualCost(rs.Hack, HackTime.CurrentScanTarget);
            string costStr = $"{actualCost}";
            Vector2 costSize = FontAssets.MouseText.Value.MeasureString(costStr) * FontCost;
            //悬停数字暗底，常态主色
            Color costColor = rs.Hover > 0.35f
                ? HackTheme.BgDarkest * rowAlpha
                : Color.Lerp(accent, Color.White, 0.25f) * rowAlpha;
            if (idleDisabled) {
                float pulse = MathF.Sin(timer * 5f + index) * 0.15f + 0.85f;
                costColor *= pulse;
            }
            Utils.DrawBorderString(sb, costStr,
                new Vector2((int)(costCell.Center.X - costSize.X * 0.5f), rect.Y + 4), costColor, FontCost);
            //RAM 微标注，无描边淡字
            Vector2 ramCapSize = FontAssets.MouseText.Value.MeasureString("RAM") * 0.5f;
            Color ramCapColor = rs.Hover > 0.35f
                ? HackTheme.BgDarkest * (rowAlpha * 0.9f)
                : Color.Lerp(accent, Color.White, 0.15f) * (rowAlpha * 0.62f);
            HackTheme.DrawRawText(sb, "RAM",
                new Vector2(costCell.Center.X - ramCapSize.X * 0.5f, rect.Bottom - 17), ramCapColor, 0.5f);
            //类别刻痕（格子左上角短斜线）
            Color catColor = HackTheme.CategoryColor(rs.Hack.Category);
            HackTheme.DrawLine(sb,
                new Vector2(costCell.X + 1, rect.Y + 6),
                new Vector2(costCell.X + 7, rect.Y + 1),
                1.4f, catColor * (rowAlpha * 0.9f));

            //---- 协议名（解码乱码入场） ----
            float nameX = costCell.Right + 12;
            float nameY = rect.Y + 5;
            string displayName = GetDecodedName(rs.Hack.DisplayName.Value, rs.Fly, index);
            Color nameColor;
            if (idleDisabled) nameColor = Color.Lerp(HackTheme.TextNormal, HackTheme.Danger, 0.55f) * 0.6f;
            else if (rs.QueueState == QueueSlotState.Completed) nameColor = HackTheme.Accent;
            else nameColor = Color.Lerp(HackTheme.TextBright, Color.White, rs.Hover * 0.4f);

            //色散残影（悬停）
            if (rs.Hover > 0.2f) {
                float aberration = rs.Hover * 1.6f;
                Utils.DrawBorderString(sb, displayName, new Vector2(nameX - aberration, nameY),
                    new Color(220, 40, 40) * (rowAlpha * rs.Hover * 0.22f), FontName);
                Utils.DrawBorderString(sb, displayName, new Vector2(nameX + aberration, nameY + 0.5f),
                    new Color(40, 80, 220) * (rowAlpha * rs.Hover * 0.22f), FontName);
            }
            Utils.DrawBorderString(sb, displayName, new Vector2(nameX, nameY), nameColor * rowAlpha, FontName);

            //---- 状态徽章（名称下） ----
            string badgeText;
            Color badgeColor;
            switch (rs.QueueState) {
                case QueueSlotState.Uploading:
                    badgeText = HackTime.UploadingPct.Format((int)(rs.QueueProgress * 100));
                    badgeColor = HackTheme.Uploading;
                    break;
                case QueueSlotState.Queued:
                    badgeText = HackTime.Queued.Value;
                    badgeColor = HackTheme.Uploading;
                    break;
                case QueueSlotState.Completed:
                    badgeText = HackTime.Done.Value;
                    badgeColor = HackTheme.Accent;
                    break;
                default:
                    badgeText = idleDisabled ? HackTime.StatusNoRam.Value : HackTime.StatusReady.Value;
                    badgeColor = idleDisabled ? HackTheme.Danger : HackTheme.AccentAlt;
                    break;
            }
            HackTheme.DrawBadge(sb, new Vector2(nameX, rect.Y + 26), badgeText, badgeColor, rowAlpha, 0.56f);

            //右区 耗时/类别
            if (rs.QueueState == QueueSlotState.None) {
                float sec = rs.Hack.UploadTime / 60f;
                string timeStr = $"{sec:F1}s";
                Vector2 ts = FontAssets.MouseText.Value.MeasureString(timeStr) * FontTime;
                Color timeColor = idleDisabled ? HackTheme.Danger * 0.75f : HackTheme.TextBright;
                Utils.DrawBorderString(sb, timeStr, new Vector2((int)(rect.Right - ts.X - 10), rect.Y + 5),
                    timeColor * (rowAlpha * 0.85f), FontTime);
            }
            else if (rs.QueueState == QueueSlotState.Uploading) {
                //大号百分比读数
                string pct = $"{(int)(rs.QueueProgress * 100)}";
                Vector2 ps = FontAssets.MouseText.Value.MeasureString(pct) * 0.72f;
                Utils.DrawBorderString(sb, pct, new Vector2((int)(rect.Right - ps.X - 20), rect.Y + 5),
                    HackTheme.Uploading * rowAlpha, 0.72f);
                Utils.DrawBorderString(sb, "%", new Vector2(rect.Right - 16, rect.Y + 11),
                    HackTheme.Uploading * (rowAlpha * 0.8f), 0.5f);
            }
            //类别符号+微标签（右下），实色亮化避免黑边吞噬
            string catSymbol = HackTheme.CategorySymbol(rs.Hack.Category);
            string catLabel = HackTheme.CategoryLabel(rs.Hack.Category);
            Color catTextColor = Color.Lerp(catColor, Color.White, 0.2f) * (rowAlpha * 0.8f);
            Vector2 cls = FontAssets.MouseText.Value.MeasureString(catLabel) * FontMicro;
            Utils.DrawBorderString(sb, catLabel, new Vector2((int)(rect.Right - cls.X - 10), (int)(rect.Bottom - cls.Y - 4)),
                catTextColor, FontMicro);
            Vector2 syms = FontAssets.MouseText.Value.MeasureString(catSymbol) * 0.48f;
            Utils.DrawBorderString(sb, catSymbol, new Vector2((int)(rect.Right - cls.X - syms.X - 15), (int)(rect.Bottom - cls.Y - 4)),
                catTextColor, 0.48f);

            //---- 禁用斜线剖面纹 ----
            if (idleDisabled) {
                Rectangle hatchArea = new(costCell.Right + 2, rect.Y + 2, rect.Width - costCell.Width - (int)TaperWidth - 4, rect.Height - 4);
                HackTheme.DrawHatch(sb, hatchArea, 11f, HackTheme.Danger * (rowAlpha * 0.10f));
            }

            //开放描边
            Color edge = accent * (rowAlpha * (0.35f + rs.Hover * 0.4f));
            //顶线超出行宽的悬挑
            sb.Draw(px, new Rectangle(rect.X + (int)TaperWidth, rect.Y, rect.Width - (int)TaperWidth + 6, 1),
                HackTheme.SrcPixel, edge);
            //底线
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1),
                HackTheme.SrcPixel, edge * 0.5f);
            //右端2px端帽 + 出头小刻度
            sb.Draw(px, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height),
                HackTheme.SrcPixel, accent * (rowAlpha * (0.5f + rs.Hover * 0.5f)));
            sb.Draw(px, new Rectangle(rect.Right, rect.Y + rect.Height / 2 - 1, 4, 2),
                HackTheme.SrcPixel, accent * (rowAlpha * 0.35f));
            //左斜切边线
            HackTheme.DrawLine(sb,
                new Vector2(rect.X + TaperWidth, rect.Y),
                new Vector2(rect.X, rect.Bottom),
                1f, edge * 0.9f);

            //---- 扫描线 ----
            if (rs.Hover > 0.1f || rs.QueueState == QueueSlotState.Uploading || HackTime.InfiniteHack) {
                float scanSpeed = HackTime.InfiniteHack ? 3.5f : rs.QueueState == QueueSlotState.Uploading ? 2.5f : 1.8f;
                float scanAlpha = HackTime.InfiniteHack ? 0.3f : rs.QueueState == QueueSlotState.Uploading ? 0.28f : rs.Hover * 0.2f;
                float scanPos = (timer * scanSpeed + index * 0.4f) % 1.4f - 0.2f;
                DrawScanLine(sb, px, rect, scanPos, rowAlpha * scanAlpha, accent);
            }

            //---- 悬停角标与辉光 ----
            if (rs.Hover > 0.15f) {
                Color bracket = accent * (rowAlpha * rs.Hover * 0.7f);
                HackTheme.DrawCornerBracket(sb, new Vector2(rect.Right - 1, rect.Y), -1, 1, 7, 1f, bracket);
                HackTheme.DrawCornerBracket(sb, new Vector2(rect.Right - 1, rect.Bottom - 1), -1, -1, 7, 1f, bracket);
                Texture2D glow = CWRAsset.SoftGlow?.Value;
                if (glow != null) {
                    Color slotGlow = accent * (rowAlpha * rs.Hover * 0.07f);
                    slotGlow.A = 0;
                    sb.Draw(glow, rect.Center.ToVector2(), null, slotGlow, 0,
                        glow.Size() / 2, new Vector2(rect.Width / 30f, rect.Height / 26f),
                        SpriteEffects.None, 0);
                }
            }

            //---- 完成白闪 ----
            if (rs.QueueState == QueueSlotState.Completed) {
                float flash = MathF.Sin(timer * 10f) * 0.5f + 0.5f;
                sb.Draw(px, rect, HackTheme.SrcPixel, HackTheme.Accent * (rowAlpha * 0.10f * flash));
            }
        }

        //入场未完时名称掺十六进制噪声
        private string GetDecodedName(string name, float fly, int index) {
            if (fly >= 0.92f || string.IsNullOrEmpty(name)) return name;
            float decodeProgress = Math.Clamp((fly - 0.3f) / 0.62f, 0f, 1f);
            int decoded = (int)(name.Length * decodeProgress);
            int frameSeed = (int)(timer * 24f) * 31 + index * 977;
            Span<char> buf = stackalloc char[name.Length];
            for (int c = 0; c < name.Length; c++) {
                if (c < decoded) {
                    buf[c] = name[c];
                }
                else {
                    int h = frameSeed + c * 131;
                    h = h * 1103515245 + 12345;
                    buf[c] = ScrambleChars[Math.Abs(h) % ScrambleChars.Length];
                }
            }
            return new string(buf);
        }

        #endregion

        #region 分组标题

        private void DrawGroupHeaders(SpriteBatch sb, float alpha, float baseX, float startY) {
            for (int i = 0; i < displayCount; i++) {
                if (!slotGroupHead[i] || slotFlyIn[i] < 0.4f) continue;
                var hack = QuickHackDef.GetByIndex(GetGlobalIndex(i));
                if (hack == null) continue;

                float headerAlpha = alpha * Math.Min(slotFlyIn[i] * 2f, 1f) * 0.9f;
                float y = GetRowY(startY, i) - 20f;
                Color catColor = HackTheme.CategoryColor(hack.Category);

                string label = HackTheme.CategoryLabel(hack.Category);
                Utils.DrawBorderString(sb, label, new Vector2((int)(baseX + TaperWidth + 2), (int)(y - 2)),
                    Color.Lerp(catColor, Color.White, 0.2f) * headerAlpha, FontGroup);
                //标题右侧引出刻度线
                float labelW = FontAssets.MouseText.Value.MeasureString(label).X * FontGroup;
                HackTheme.DrawDashedLine(sb,
                    new Vector2(baseX + TaperWidth + labelW + 10, y + 7),
                    new Vector2(baseX + RowWidth, y + 7),
                    1f, catColor * (headerAlpha * 0.4f), 3f, 5f);
            }
        }

        #endregion

        #region 背景噪波与故障带

        //背景水平噪波
        private void DrawAmbientNoise(SpriteBatch sb, Texture2D px, float alpha, float baseX, float startY, float totalH) {
            bool anyVisible = false;
            for (int i = 0; i < slotFlyIn.Length; i++) {
                if (slotFlyIn[i] > 0.3f) { anyVisible = true; break; }
            }
            if (!anyVisible) return;

            float x0 = baseX - TrunkOffsetX - 20;
            float x1 = Main.screenWidth - RightMargin + 10;
            float regionH = totalH + FooterHeight + 20f;

            float noiseAlpha = alpha * 0.022f;
            for (int dy = 0; dy < (int)regionH; dy += 3) {
                float seed = dy * 0.73f + timer * 8f;
                float brightness = MathF.Sin(seed) * 0.5f + 0.5f;
                if (brightness < 0.3f) continue;
                sb.Draw(px, new Rectangle((int)x0, (int)(startY - 10 + dy), (int)(x1 - x0), 1),
                    HackTheme.SrcPixel, HackTheme.Accent * (noiseAlpha * brightness));
            }
        }

        //故障色偏带
        private void DrawGlitchBand(SpriteBatch sb, Texture2D px, float alpha, float baseX) {
            if (glitchBandCooldown > 0f) return;

            float bandH = 4f + MathF.Sin(timer * 30f) * 2f;
            float bandAlpha = alpha * 0.15f;
            float x0 = baseX - TrunkOffsetX - 10;
            float x1 = Main.screenWidth - RightMargin + 5;

            sb.Draw(px, new Rectangle((int)(x0 + 3), (int)glitchBandY, (int)(x1 - x0), (int)bandH),
                HackTheme.SrcPixel, HackTheme.Accent * bandAlpha);
            sb.Draw(px, new Rectangle((int)(x0 - 2), (int)(glitchBandY + 1), (int)(x1 - x0), (int)(bandH * 0.5f)),
                HackTheme.SrcPixel, new Color(200, 30, 60) * (bandAlpha * 0.4f));
        }

        #endregion

        #region 电路连接树

        private void DrawConnectorTree(SpriteBatch sb, Texture2D px, float alpha, float baseX, float listStartY, float totalH) {
            if (HackTime.CurrentScanTarget == null) return;

            float trunkX = baseX - TrunkOffsetX;
            Vector2 screenCenter = new(Main.screenWidth * 0.5f, Main.screenHeight * 0.5f);

            float wireProgress = Math.Clamp(revealTime * 3f, 0f, 1f);
            float wireAlpha = alpha * wireProgress * 0.40f;
            Color wireColor = HackTheme.Accent * wireAlpha;

            //主干水平线（中心→主干），虚化暗示
            float hLineEnd = MathHelper.Lerp(screenCenter.X, trunkX, HackTheme.EaseOutCubic(wireProgress));
            HackTheme.DrawDashedLine(sb, screenCenter, new Vector2(hLineEnd, screenCenter.Y),
                1.2f, wireColor * 0.7f, 7f, 9f);

            if (wireProgress > 0.3f) {
                float trunkProg = Math.Clamp((wireProgress - 0.3f) / 0.7f, 0f, 1f);
                float firstCY = GetRowY(listStartY, 0) + RowHeight * 0.5f;
                float lastCY = GetRowY(listStartY, displayCount - 1) + RowHeight * 0.5f;
                float trunkTop = MathHelper.Lerp(screenCenter.Y, firstCY, trunkProg);
                float trunkBot = MathHelper.Lerp(screenCenter.Y, lastCY, trunkProg);
                //垂直干线保持实线（结构件）
                HackTheme.DrawLine(sb, new Vector2(trunkX, trunkTop), new Vector2(trunkX, trunkBot), 1.5f, wireColor * 0.8f);

                //待命虚线，悬停/上传实线
                for (int i = 0; i < displayCount; i++) {
                    float fly = slotFlyIn[i];
                    if (fly < 0.05f) continue;
                    float itemCY = GetRowY(listStartY, i) + RowHeight * 0.5f;
                    float branchEndX = slotRects[i] != Rectangle.Empty ? slotRects[i].X - 2 : baseX - 4;

                    int gi = GetGlobalIndex(i);
                    var qs = Queue?.GetSlotState(gi, HackTime.CurrentScanTarget) ?? QueueSlotState.None;
                    bool lit = i == hoveredSlot || qs == QueueSlotState.Uploading;

                    Vector2 p0 = new(trunkX, itemCY);
                    Vector2 p1 = new(MathHelper.Lerp(trunkX, branchEndX, fly), itemCY);

                    if (lit) {
                        Color litColor = qs == QueueSlotState.Uploading
                            ? HackTheme.Uploading * (wireAlpha * 1.6f)
                            : HackTheme.Accent * (wireAlpha * 1.8f);
                        HackTheme.DrawLine(sb, p0, p1, 1.4f, litColor);
                        //末端菱形节点
                        HackTheme.DrawDiamond(sb, p1, 5f, litColor * 1.2f);
                        HackTheme.DrawDiamond(sb, p1, 2.4f, HackTheme.BgDarkest * alpha);
                    }
                    else {
                        Color idleColor = qs == QueueSlotState.Queued
                            ? HackTheme.Uploading * (wireAlpha * 0.6f)
                            : wireColor * 0.45f;
                        HackTheme.DrawDashedLine(sb, p0, p1, 1f, idleColor, 4f, 6f);
                    }

                    //分组首行的主干节点
                    if (slotGroupHead[i]) {
                        HackTheme.DrawDiamondOutline(sb, new Vector2(trunkX, itemCY), 4f, 1f, wireColor * 0.9f);
                    }
                }
            }

            //流动数据光点（主干路径两个）
            Texture2D glowTex = CWRAsset.SoftGlow?.Value;
            if (glowTex != null) {
                for (int d = 0; d < 2; d++) {
                    float flowT = (timer * 0.55f + d * 0.5f) % 1f;
                    Vector2 flowPos;
                    if (flowT < 0.5f) {
                        flowPos = Vector2.Lerp(screenCenter, new Vector2(trunkX, screenCenter.Y), flowT / 0.5f);
                    }
                    else {
                        float t = (flowT - 0.5f) / 0.5f;
                        float tTop = GetRowY(listStartY, 0) + RowHeight * 0.5f;
                        float tBot = GetRowY(listStartY, displayCount - 1) + RowHeight * 0.5f;
                        flowPos = new Vector2(trunkX, MathHelper.Lerp(tTop, tBot, t));
                    }
                    Color dotGlow = HackTheme.Accent * (alpha * 0.28f * (1f - d * 0.3f));
                    dotGlow.A = 0;
                    sb.Draw(glowTex, flowPos, null, dotGlow, 0, glowTex.Size() / 2, 0.07f, SpriteEffects.None, 0);
                }

                //上传分支脉冲
                for (int i = 0; i < displayCount; i++) {
                    int gi = GetGlobalIndex(i);
                    if (Queue == null || Queue.GetSlotState(gi, HackTime.CurrentScanTarget) != QueueSlotState.Uploading) continue;
                    float itemCY = GetRowY(listStartY, i) + RowHeight * 0.5f;
                    float branchEndX = slotRects[i] != Rectangle.Empty ? slotRects[i].X - 2 : baseX - 4;
                    float pulseT = timer * 2f % 1f;
                    float pulseX = MathHelper.Lerp(trunkX, branchEndX, pulseT);
                    Color pulseCol = HackTheme.Uploading * (alpha * 0.4f * (1f - pulseT));
                    pulseCol.A = 0;
                    sb.Draw(glowTex, new Vector2(pulseX, itemCY), null, pulseCol, 0, glowTex.Size() / 2, 0.06f, SpriteEffects.None, 0);
                }
            }
        }

        #endregion

        #region 详情页脚

        private void DrawFooter(SpriteBatch sb, Texture2D px, float alpha, float baseX, float footerY) {
            bool anyVisible = false;
            for (int i = 0; i < slotFlyIn.Length; i++) {
                if (slotFlyIn[i] > 0.5f) { anyVisible = true; break; }
            }
            if (!anyVisible) return;

            //分隔虚线
            HackTheme.DrawDashedLine(sb, new Vector2(baseX, footerY - 6),
                new Vector2(baseX + RowWidth, footerY - 6), 1f, HackTheme.Border * (alpha * 0.5f), 5f, 4f);

            if (hoveredSlot >= 0) {
                var hack = QuickHackDef.GetByIndex(GetGlobalIndex(hoveredSlot));
                if (hack != null) {
                    DrawFooterDetail(sb, px, alpha, baseX, footerY, hack);
                    return;
                }
            }
            DrawFooterStatus(sb, px, alpha, baseX, footerY);
        }

        //悬停协议详情
        private void DrawFooterDetail(SpriteBatch sb, Texture2D px, float alpha, float baseX, float footerY, QuickHackDef hack) {
            Color catColor = HackTheme.CategoryColor(hack.Category);

            //类别竖刻 + 协议名微标题
            sb.Draw(px, new Rectangle((int)baseX, (int)footerY + 2, 2, 14), HackTheme.SrcPixel, catColor * (alpha * 0.9f));
            Utils.DrawBorderString(sb, hack.DisplayName.Value, new Vector2((int)(baseX + 8), (int)(footerY - 2)),
                HackTheme.TextBright * alpha, 0.66f);

            //描述换行（最多3行）
            var descFont = FontAssets.MouseText.Value;
            int wrapPx = Math.Max(32, (int)(RowWidth / FontDesc) - 6);
            string[] descLines = VaultUtils.WrapTextArray(hack.Description.Value, descFont, wrapPx, 3, out _);
            float lineH = descFont.MeasureString("汉").Y * FontDesc;
            float curY = footerY + 20f;
            for (int li = 0; li < descLines.Length; li++) {
                if (string.IsNullOrEmpty(descLines[li])) continue;
                Utils.DrawBorderString(sb, descLines[li].TrimEnd('-', ' '),
                    new Vector2((int)(baseX + 8), (int)(curY + li * lineH)),
                    HackTheme.TextBright * (alpha * 0.85f), FontDesc);
            }

            //成本与耗时行
            float metaY = curY + descLines.Length * lineH + 4f;
            int actualCost = HackCostEvaluator.GetActualCost(hack, HackTime.CurrentScanTarget);
            string costStr = HackTime.FooterCost.Format(actualCost);
            if (actualCost != hack.RamCost)
                costStr += $" ×{(float)actualCost / hack.RamCost:F1}";
            Utils.DrawBorderString(sb, costStr, new Vector2((int)(baseX + 8), (int)metaY),
                Color.Lerp(HackTheme.Accent, Color.White, 0.2f) * alpha, 0.56f);
            string upStr = HackTime.FooterUpload.Format($"{hack.UploadTime / 60f:F1}");
            float costW = FontAssets.MouseText.Value.MeasureString(costStr).X * 0.56f;
            Utils.DrawBorderString(sb, upStr, new Vector2((int)(baseX + 8 + costW + 16), (int)metaY),
                HackTheme.TextBright * (alpha * 0.8f), 0.56f);
        }

        //无悬停时的系统状态
        private void DrawFooterStatus(SpriteBatch sb, Texture2D px, float alpha, float baseX, float footerY) {
            float pulse = (MathF.Sin(timer * 3.5f) + 1f) * 0.5f;
            bool hasActive = Queue != null && !Queue.IsEmpty;
            Color dotColor = hasActive
                ? Color.Lerp(HackTheme.Uploading * 0.4f, HackTheme.Uploading, pulse) * alpha
                : Color.Lerp(new Color(20, 100, 50), new Color(40, 200, 100), pulse) * alpha;
            HackTheme.DrawDiamond(sb, new Vector2(baseX + 5, footerY + 8), 7f, dotColor);

            string status = hasActive ? HackTime.UploadingText.Value : HackTime.BreachReady.Value;
            if (Queue != null && Queue.HasCompleted) status = HackTime.UploadComplete.Value;
            Utils.DrawBorderString(sb, status, new Vector2((int)(baseX + 16), (int)footerY),
                HackTheme.TextNormal * alpha, 0.62f);

            //伪十六进制 + 协议计数，无描边
            string tag = $"NET::0x{(int)(timer * 100) % 0xFFFF:X4}";
            HackTheme.DrawRawText(sb, tag, new Vector2(baseX + RowWidth - 110, footerY + 1),
                HackTheme.Accent * (alpha * 0.5f), FontMicro);
            string countStr = HackTime.Protocols.Format(displayCount);
            HackTheme.DrawRawText(sb, countStr, new Vector2(baseX + RowWidth - 110, footerY + 18),
                HackTheme.TextNormal * (alpha * 0.55f), FontMicro);

            //右键取消提示
            if (HackTime.CurrentScanTarget != null) {
                float hintPulse = MathF.Sin(timer * 1.8f) * 0.12f + 0.88f;
                Utils.DrawBorderString(sb, HackTime.RightClickHint.Value, new Vector2((int)baseX, (int)(footerY + 24f)),
                    HackTheme.TextBright * (alpha * hintPulse * 0.9f), 0.72f);
            }
        }

        #endregion

        #region 视觉辅助

        //竖扫描线
        private static void DrawScanLine(SpriteBatch sb, Texture2D px, Rectangle rect, float pos, float alpha, Color color) {
            int lineX = rect.X + (int)(rect.Width * pos);
            if (lineX < rect.X || lineX > rect.Right - 2) return;
            sb.Draw(px, new Rectangle(lineX, rect.Y + 1, 2, rect.Height - 2),
                HackTheme.SrcPixel, color * alpha);
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null) {
                Color gc = color * (alpha * 0.6f);
                gc.A = 0;
                sb.Draw(glow, new Vector2(lineX, rect.Center.Y), null, gc, 0,
                    glow.Size() / 2, new Vector2(0.1f, rect.Height / 40f), SpriteEffects.None, 0);
            }
        }

        #endregion
    }
}
