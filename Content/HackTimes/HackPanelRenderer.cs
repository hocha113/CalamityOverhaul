using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes.Protocols;
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
        //悬停检测用，已按视口裁剪，滚出去的部分不吃鼠标
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
        //页脚计数用，Show 时快照一次
        private int ownedCount;
        //滚动位移，正值表示列表上移
        private float scrollOffset;
        private float scrollTarget;
        private float maxScroll;

        #endregion

        #region 排版常量

        private const float RowWidth = 340f;
        //基准行高与间距，纵向空间不足时按比例压缩
        private const float RowHeightBase = 48f;
        private const float RowGapBase = 5f;
        //分组标题额外占高
        private const float GroupGapBase = 24f;
        //行高压缩下限，再低装不下名称加徽章两行
        private const float RowHeightMin = 40f;
        //压缩只用来吸收一两行的溢出，再多交给滚动；压到 0.62 那种挤法已被滚动取代
        private const float MinLayoutScale = 0.85f;
        //视口上下缘的软化带宽，跨界行按此淡出，替代 scissor
        private const float ClipFeather = 16f;
        //滚轮一格走的像素，按行高折算
        private const float ScrollStepRows = 0.9f;
        //滚动条轨道相对行右缘的偏移与宽度
        private const float ScrollBarOffsetX = 12f;
        private const float ScrollBarWidth = 3f;
        //旗标左端斜切宽（尖端指向屏幕中心）
        private const float TaperWidth = 12f;
        //成本大格宽
        private const float CostCellWidth = 46f;
        //电路树主干左偏移
        private const float TrunkOffsetX = 44f;
        //行尾顶线与出头刻度的最大悬挑，右缘预留这段才不越过边距
        private const float EdgeOverhang = 6f;
        //末行到页脚基线
        private const float FooterTopGap = 14f;
        //首条目前延迟（秒）
        private const float BaseEntryDelay = 0.2f;
        //条目飞入间隔（秒）
        private const float EntryStagger = 0.06f;
        //MouseText 中文不低于 0.5，否则糊
        private static float FontName => 0.80f;
        private static float FontCost => 0.88f;
        private static float FontDesc => 0.72f;
        private static float FontTime => 0.56f;
        private static float FontMicro => 0.50f;
        private static float FontGroup => 0.58f;
        private static float FontMeta => 0.56f;
        private static float FontHint => 0.72f;

        //解码乱码字符池
        private const string ScrambleChars = "0123456789ABCDEF#$%&";

        #endregion

        #region 每帧解算的排版

        private float rowHeight = RowHeightBase;
        private float rowGap = RowGapBase;
        private float groupGap = GroupGapBase;
        private float listStartY;
        private float listHeight;
        //可见窗口，列表装不下时小于 listHeight，差额即滚动行程
        private float viewportTop;
        private float viewportHeight;
        //只含无悬停态，见 MeasureStatusFooterHeight
        private float footerHeight;

        #endregion

        #region 生命周期

        public void Show(HackTargetKind targetKind = HackTargetKind.Npc) {
            //先取过滤集，再按类别分组排序
            List<int> filtered = [];
            QuickHackDef.GetFilteredIndices(targetKind, filtered);

            //未持有的协议不进列表：既不给名也不给描述的行只是扫读噪声
            Player local = Main.LocalPlayer;
            displayIndices.Clear();
            foreach (QuickHackCategory cat in Enum.GetValues<QuickHackCategory>()) {
                for (int i = 0; i < filtered.Count; i++) {
                    var hack = QuickHackDef.GetByIndex(filtered[i]);
                    if (hack == null || hack.Category != cat) continue;
                    if (!HackProtocolOwned.Owns(local, hack)) continue;
                    displayIndices.Add(filtered[i]);
                }
            }
            displayCount = displayIndices.Count;

            //一条都没有时仍然 visible：整列凭空消失读起来像 bug，改画空态卡
            if (slotFlyIn == null || slotFlyIn.Length != displayCount) {
                slotFlyIn = new float[displayCount];
                slotHoverAnim = new float[displayCount];
                slotGlitchSeed = new float[displayCount];
                slotRects = new Rectangle[displayCount];
                slotYOffset = new float[displayCount];
                slotGroupHead = new bool[displayCount];
            }

            //标记分组首行，纵偏由 RecomputeLayout 按当前 groupGap 累加
            QuickHackCategory? lastCat = null;
            for (int i = 0; i < displayCount; i++) {
                var hack = QuickHackDef.GetByIndex(displayIndices[i]);
                slotGroupHead[i] = lastCat == null || hack.Category != lastCat.Value;
                lastCat = hack.Category;
            }
            ownedCount = HackProtocolOwned.CountOwned(local);

            visible = true;
            hoveredSlot = -1;
            HoveredCostPreview = 0;
            revealTime = 0f;
            glitchBandY = -100f;
            glitchBandCooldown = 0.5f;
            scrollOffset = 0f;
            scrollTarget = 0f;
            Array.Clear(slotFlyIn);
            Array.Clear(slotHoverAnim);
            for (int i = 0; i < slotGlitchSeed.Length; i++)
                slotGlitchSeed[i] = Main.rand.NextFloat() * 100f;

            //Show 发生在本帧 Update 之后，这里先解一次算，Draw 才不会读到上一目标的排版
            RecomputeLayout();
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

            RecomputeLayout();

            //故障带下移
            glitchBandCooldown -= 0.016f;
            if (glitchBandCooldown <= 0f) {
                glitchBandY += 600f * 0.016f;
                if (glitchBandY > viewportTop + viewportHeight + 60f) {
                    glitchBandY = viewportTop - 50f;
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
                    int globalIdx = GetGlobalIndex(i);
                    var hack = QuickHackDef.GetByIndex(globalIdx);
                    //禁用槽位不响应悬停
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

            if (!HackTimeNetSync.TryRequestQueue(hack, target,
                out uint sessionId, out uint requestId)) return;
            Queue.Enqueue(hack, globalIdx, target, actualCost, sessionId, requestId);
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

        //列左缘。右缘预留 EdgeOverhang，行尾悬挑才不越过边距
        private static float GetBaseX()
            => HackTheme.UIScreenW - HackTheme.SideMargin - EdgeOverhang - RowWidth;

        //本列横向占位，含电路树主干与背景噪波
        private static void GetColumnBand(out float x0, out float x1) {
            float baseX = GetBaseX();
            x0 = baseX - TrunkOffsetX - 20f;
            x1 = baseX + RowWidth + EdgeOverhang;
        }

        //RAM 弧在本列横向带内的最低点；弧够不着时退回顶部安全线
        private static float ResolveArcClearY() {
            GetColumnBand(out float x0, out float x1);
            float arcBottom = HackRamArcLayout.BottomInBand(RamSystem.MaxRam, x0, x1);
            return arcBottom > float.MinValue
                ? arcBottom + HackRamArcLayout.ClearGap
                : HackTheme.TopSafe;
        }

        //给定行度量下的列表总高（含分组标题）
        private float MeasureListHeight(float rowH, float gap, float groupH) {
            if (displayCount <= 0) return 0f;
            int groups = 0;
            for (int i = 0; i < displayCount; i++) {
                if (slotGroupHead[i]) groups++;
            }
            return displayCount * (rowH + gap) - gap + groups * groupH;
        }

        /// <summary>
        /// 每帧解算排版：先按 RAM 弧的实际占位定顶线，再决定要不要压缩纵向节奏
        /// <br/>字号一律不参与压缩，最小的 FontMicro 已贴着中文可读下限
        /// </summary>
        private void RecomputeLayout() {
            if (displayCount <= 0 || slotGroupHead == null) {
                listHeight = 0f;
                footerHeight = 0f;
                viewportHeight = 0f;
                maxScroll = 0f;
                return;
            }

            float screenH = HackTheme.UIScreenH;
            footerHeight = MeasureStatusFooterHeight();

            float minY = ResolveArcClearY();
            float avail = screenH - HackTheme.BottomSafe - minY - footerHeight;
            float needAtFull = MeasureListHeight(RowHeightBase, RowGapBase, GroupGapBase);

            float scale = 1f;
            if (needAtFull > 0f) {
                //余量为负时直接压到下限，宁可挤也别整块顶出屏外
                scale = avail > 0f
                    ? MathHelper.Clamp(avail / needAtFull, MinLayoutScale, 1f)
                    : MinLayoutScale;
            }
            rowHeight = MathF.Max(RowHeightBase * scale, RowHeightMin);
            rowGap = RowGapBase * scale;
            groupGap = GroupGapBase * scale;

            //分组标题纵偏随 groupGap 变，跟着重算
            float acc = 0f;
            for (int i = 0; i < displayCount; i++) {
                if (slotGroupHead[i]) acc += groupGap;
                slotYOffset[i] = acc;
            }

            listHeight = MeasureListHeight(rowHeight, rowGap, groupGap);

            //视口取列表高与可用高的较小者，差额就是滚动行程
            viewportHeight = avail > 0f ? MathF.Min(listHeight, avail) : listHeight;
            maxScroll = MathF.Max(listHeight - viewportHeight, 0f);

            //含页脚整块居中，再夹进 [避让线, 屏底安全线]
            float centered = (screenH - (viewportHeight + footerHeight)) * 0.5f;
            float maxStart = screenH - HackTheme.BottomSafe - viewportHeight - footerHeight;
            //可用高为负时（小屏 + 高 UI 缩放 + 大 MaxRam），避让线退让到刚好装下
            //：宁可与弧的装饰环重叠，也不把可交互的行推出屏外
            float minStart = MathF.Min(minY, MathF.Max(maxStart, 0f));
            viewportTop = MathHelper.Clamp(centered, minStart, MathF.Max(minStart, maxStart));

            //目标先夹再缓动，行高变化导致行程缩水时不会卡在越界值上
            scrollTarget = MathHelper.Clamp(scrollTarget, 0f, maxScroll);
            scrollOffset = MathHelper.Lerp(scrollOffset, scrollTarget, 0.2f);
            if (MathF.Abs(scrollOffset - scrollTarget) < 0.3f) scrollOffset = scrollTarget;
            scrollOffset = MathHelper.Clamp(scrollOffset, 0f, maxScroll);

            listStartY = viewportTop - scrollOffset;
        }

        private float GetRowY(int i) {
            return listStartY + i * (rowHeight + rowGap) + slotYOffset[i];
        }

        //行在视口内的可见度，跨界处按 ClipFeather 淡出，替代 scissor
        private float GetClipFade(float rowY) {
            if (maxScroll <= 0.5f) return 1f;
            float enterTop = (rowY + rowHeight - viewportTop) / ClipFeather;
            float enterBottom = (viewportTop + viewportHeight - rowY) / ClipFeather;
            return MathHelper.Clamp(MathF.Min(enterTop, enterBottom), 0f, 1f);
        }

        //电路树干线的纵向跨度，夹在视口内，滚动时干线不会拖到屏外
        private void GetTrunkSpan(out float top, out float bottom) {
            float first = GetRowY(0) + rowHeight * 0.5f;
            float last = GetRowY(displayCount - 1) + rowHeight * 0.5f;
            float lo = viewportTop + 2f;
            float hi = viewportTop + MathF.Max(viewportHeight - 2f, 2f);
            top = MathHelper.Clamp(first, lo, hi);
            bottom = MathHelper.Clamp(last, lo, hi);
        }

        #endregion

        #region 滚动

        /// <summary>滚轮推进，delta 为原版 UI 滚轮增量</summary>
        public void HandleScroll(int delta) {
            if (!visible || delta == 0 || maxScroll <= 0.5f) return;
            scrollTarget = MathHelper.Clamp(
                scrollTarget - Math.Sign(delta) * (rowHeight + rowGap) * ScrollStepRows,
                0f, maxScroll);
        }

        /// <summary>列表是否溢出视口，UI 层据此决定要不要吃掉滚轮</summary>
        public bool CanScroll => visible && maxScroll > 0.5f;

        /// <summary>
        /// 鼠标是否落在列表视口内。比逐行命中宽松：行与行之间那几像素空隙也算，
        /// 否则光标停在缝里滚轮就失灵
        /// </summary>
        public bool ViewportContainsMouse() {
            if (!visible || viewportHeight <= 1f) return false;
            GetColumnBand(out float x0, out float x1);
            return Main.mouseX >= x0 && Main.mouseX <= x1
                && Main.mouseY >= viewportTop && Main.mouseY <= viewportTop + viewportHeight;
        }

        #endregion

        #region 主绘制入口

        public void Draw(SpriteBatch sb) {
            HackTargetFrame.Draw(sb, timer);

            Texture2D px = HackTheme.Pixel;
            if (px == null) return;
            float alpha = HackTime.Intensity;
            if (alpha < 0.01f) return;
            if (displayCount == 0) {
                if (visible) DrawEmptyState(sb, px, alpha);
                return;
            }
            if (slotFlyIn == null) return;

            float baseX = GetBaseX();

            DrawAmbientNoise(sb, px, alpha, baseX);
            DrawConnectorTree(sb, px, alpha, baseX);

            //行几何与状态快照
            if (rowStates == null || rowStates.Length != displayCount)
                rowStates = new RowState[displayCount];
            RowState[] rows = rowStates;
            BuildRowStates(rows, baseX);

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
            DrawGroupHeaders(sb, alpha, baseX);

            DrawGlitchBand(sb, px, alpha, baseX);
            DrawScrollBar(sb, px, alpha, baseX);
            //页脚锚在视口底，不随滚动上下跑
            DrawFooter(sb, px, alpha, baseX, viewportTop + viewportHeight + FooterTopGap);
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
            //视口裁剪可见度，跨上下缘时 <1
            public float Clip;
            public Color AccentColor;
        }

        //行状态快照池
        private RowState[] rowStates;

        private void BuildRowStates(RowState[] rows, float baseX) {
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

                float y = GetRowY(i);
                rs.Clip = GetClipFade(y);
                if (rs.Clip <= 0.004f) {
                    slotRects[i] = Rectangle.Empty;
                    rs.Skip = true;
                    continue;
                }

                rs.Fly = fly;
                rs.Hover = slotHoverAnim[i];
                rs.QueueState = Queue?.GetSlotState(globalIdx, HackTime.CurrentScanTarget) ?? QueueSlotState.None;
                rs.QueueProgress = Queue?.GetSlotProgress(globalIdx, HackTime.CurrentScanTarget) ?? 0f;
                rs.Disabled = !RamSystem.CanAfford(rs.Hack.RamCost)
                    || rs.QueueState is QueueSlotState.Uploading or QueueSlotState.Queued;
                if (rs.Disabled && rs.QueueState == QueueSlotState.None) rs.Hover = 0f;

                //飞入偏移（弹性过冲）+ 故障抖动
                float flyOffset = (1f - HackTheme.EaseOutBack(fly)) * 400f;
                rs.Glitch = 0f;
                if (fly < 0.85f) {
                    float seed = slotGlitchSeed[i] + timer * 25f;
                    rs.Glitch = (MathF.Sin(seed) + MathF.Sin(seed * 2.7f) * 0.5f) * (1f - fly);
                }

                //悬停向屏幕中心（左）扩展
                float hoverExpand = rs.Hover * 14f;
                float x = baseX + flyOffset + rs.Glitch * 16f - hoverExpand;
                Rectangle rect = new((int)x, (int)y, (int)(RowWidth + hoverExpand), (int)rowHeight);
                rs.Rect = rect;
                //命中框按视口纵向裁剪，滚出去的半行不吃鼠标
                slotRects[i] = ClipHitRect(rect);

                //整行主色由状态决定
                rs.AccentColor = ResolveRowAccent(in rs, i);
            }
        }

        //只裁纵向：横向在飞入期会远超列带，按列带裁会把入场行的命中框抹掉
        private Rectangle ClipHitRect(Rectangle rect) {
            if (maxScroll <= 0.5f) return rect;
            int top = Math.Max(rect.Y, (int)viewportTop);
            int bottom = Math.Min(rect.Bottom, (int)(viewportTop + viewportHeight));
            return bottom > top
                ? new Rectangle(rect.X, top, rect.Width, bottom - top)
                : Rectangle.Empty;
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

        //整行不透明度：入场淡入 × 视口裁剪
        private static float RowAlpha(in RowState rs, float alpha)
            => alpha * Math.Min(rs.Fly * 2.5f, 1f) * rs.Clip;

        #region 行背景

        private void DrawRowBackgroundsShader(SpriteBatch sb, Texture2D px, Effect deck, Span<RowState> rows, float alpha) {
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, deck, Main.UIScaleMatrix);

            for (int i = 0; i < displayCount; i++) {
                ref RowState rs = ref rows[i];
                if (rs.Skip) continue;

                float rowAlpha = RowAlpha(in rs, alpha);
                deck.Parameters["uTime"]?.SetValue(timer + slotGlitchSeed[i]);
                deck.Parameters["uAlpha"]?.SetValue(rowAlpha);
                deck.Parameters["uResolution"]?.SetValue(new Vector2(rs.Rect.Width, rs.Rect.Height));
                deck.Parameters["uTaperLeft"]?.SetValue(TaperWidth);
                deck.Parameters["uTaperRight"]?.SetValue(0f);
                deck.Parameters["uAccent"]?.SetValue(rs.AccentColor.ToVector3());
                deck.Parameters["uHover"]?.SetValue(rs.Hover);
                deck.Parameters["uDisabled"]?.SetValue(
                    rs.Disabled && rs.QueueState == QueueSlotState.None ? 1f : 0f);
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
                float rowAlpha = RowAlpha(in rs, alpha);

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
            float rowAlpha = RowAlpha(in rs, alpha);
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
            //徽章贴底锚定，行高被压缩时才不溢出行外
            HackTheme.DrawBadge(sb, new Vector2(nameX, rect.Bottom - 22), badgeText, badgeColor, rowAlpha, 0.56f);

            //右区 耗时/类别
            if (rs.QueueState == QueueSlotState.None) {
                //折后值与权威侧同一个口径，面板读数才对得上实际上传
                float sec = PrivilegeEscalateState.ApplyUploadTime(
                    rs.Hack.UploadTime, Main.LocalPlayer) / 60f;
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

        private void DrawGroupHeaders(SpriteBatch sb, float alpha, float baseX) {
            for (int i = 0; i < displayCount; i++) {
                if (!slotGroupHead[i] || slotFlyIn[i] < 0.4f) continue;
                var hack = QuickHackDef.GetByIndex(GetGlobalIndex(i));
                if (hack == null) continue;

                float rowY = GetRowY(i);
                float y = rowY - 20f;
                //标题跟着自己那一行走：行滚没了就不画。
                //允许探出视口上缘一点，那上面是 RAM 弧的留白，越界 20px 不碰到东西
                if (maxScroll > 0.5f
                    && (GetClipFade(rowY) <= 0.25f || y > viewportTop + viewportHeight - 8f)) {
                    continue;
                }
                float headerAlpha = alpha * Math.Min(slotFlyIn[i] * 2f, 1f) * 0.9f;
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

        #region 滚动条

        //列表溢出视口时才出现，细轨 + 主色滑块，无贴图
        private void DrawScrollBar(SpriteBatch sb, Texture2D px, float alpha, float baseX) {
            if (maxScroll <= 0.5f || viewportHeight <= 1f) return;

            int trackX = (int)(baseX + RowWidth + ScrollBarOffsetX);
            int trackW = (int)ScrollBarWidth;
            int trackY = (int)viewportTop;
            int trackH = (int)viewportHeight;

            sb.Draw(px, new Rectangle(trackX, trackY, trackW, trackH),
                HackTheme.SrcPixel, HackTheme.Border * (alpha * 0.55f));

            float ratio = viewportHeight / MathF.Max(listHeight, 1f);
            int thumbH = (int)MathF.Max(trackH * ratio, 18f);
            float travel = MathF.Max(trackH - thumbH, 0f);
            int thumbY = trackY + (int)(travel * (scrollOffset / MathF.Max(maxScroll, 1f)));

            Color accent = HackTheme.Accent;
            sb.Draw(px, new Rectangle(trackX, thumbY, trackW, thumbH),
                HackTheme.SrcPixel, accent * (alpha * 0.75f));
            //两端小帽，读作机械滑块而不是一条色带
            sb.Draw(px, new Rectangle(trackX - 1, thumbY, trackW + 2, 1),
                HackTheme.SrcPixel, accent * (alpha * 0.9f));
            sb.Draw(px, new Rectangle(trackX - 1, thumbY + thumbH - 1, trackW + 2, 1),
                HackTheme.SrcPixel, accent * (alpha * 0.9f));
        }

        #endregion

        #region 背景噪波与故障带

        //背景水平噪波
        private void DrawAmbientNoise(SpriteBatch sb, Texture2D px, float alpha, float baseX) {
            bool anyVisible = false;
            for (int i = 0; i < slotFlyIn.Length; i++) {
                if (slotFlyIn[i] > 0.3f) { anyVisible = true; break; }
            }
            if (!anyVisible) return;

            GetColumnBand(out float x0, out float x1);
            float regionH = viewportHeight + footerHeight + 20f;

            float noiseAlpha = alpha * 0.022f;
            for (int dy = 0; dy < (int)regionH; dy += 3) {
                float seed = dy * 0.73f + timer * 8f;
                float brightness = MathF.Sin(seed) * 0.5f + 0.5f;
                if (brightness < 0.3f) continue;
                sb.Draw(px, new Rectangle((int)x0, (int)(viewportTop - 10 + dy), (int)(x1 - x0), 1),
                    HackTheme.SrcPixel, HackTheme.Accent * (noiseAlpha * brightness));
            }
        }

        //故障色偏带
        private void DrawGlitchBand(SpriteBatch sb, Texture2D px, float alpha, float baseX) {
            if (glitchBandCooldown > 0f) return;

            float bandH = 4f + MathF.Sin(timer * 30f) * 2f;
            float bandAlpha = alpha * 0.15f;
            GetColumnBand(out float bandLeft, out float bandRight);
            float x0 = baseX - TrunkOffsetX - 10;
            //色偏两层各带 ±3 位移，宽度按位移收窄才不越过列右缘
            float bandW = bandRight - x0 - 3f;

            sb.Draw(px, new Rectangle((int)(x0 + 3), (int)glitchBandY, (int)bandW, (int)bandH),
                HackTheme.SrcPixel, HackTheme.Accent * bandAlpha);
            sb.Draw(px, new Rectangle((int)MathF.Max(x0 - 2, bandLeft), (int)(glitchBandY + 1), (int)bandW, (int)(bandH * 0.5f)),
                HackTheme.SrcPixel, new Color(200, 30, 60) * (bandAlpha * 0.4f));
        }

        #endregion

        #region 电路连接树

        private void DrawConnectorTree(SpriteBatch sb, Texture2D px, float alpha, float baseX) {
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
                GetTrunkSpan(out float firstCY, out float lastCY);
                float trunkTop = MathHelper.Lerp(screenCenter.Y, firstCY, trunkProg);
                float trunkBot = MathHelper.Lerp(screenCenter.Y, lastCY, trunkProg);
                //垂直干线保持实线（结构件）
                HackTheme.DrawLine(sb, new Vector2(trunkX, trunkTop), new Vector2(trunkX, trunkBot), 1.5f, wireColor * 0.8f);

                //待命虚线，悬停/上传实线
                for (int i = 0; i < displayCount; i++) {
                    float fly = slotFlyIn[i];
                    if (fly < 0.05f) continue;
                    float itemCY = GetRowY(i) + rowHeight * 0.5f;
                    float clip = GetClipFade(GetRowY(i));
                    if (clip <= 0.004f) continue;
                    float branchEndX = slotRects[i] != Rectangle.Empty ? slotRects[i].X - 2 : baseX - 4;

                    int gi = GetGlobalIndex(i);
                    var qs = Queue?.GetSlotState(gi, HackTime.CurrentScanTarget) ?? QueueSlotState.None;
                    bool lit = i == hoveredSlot || qs == QueueSlotState.Uploading;

                    Vector2 p0 = new(trunkX, itemCY);
                    Vector2 p1 = new(MathHelper.Lerp(trunkX, branchEndX, fly), itemCY);

                    if (lit) {
                        Color litColor = qs == QueueSlotState.Uploading
                            ? HackTheme.Uploading * (wireAlpha * 1.6f * clip)
                            : HackTheme.Accent * (wireAlpha * 1.8f * clip);
                        HackTheme.DrawLine(sb, p0, p1, 1.4f, litColor);
                        //末端菱形节点
                        HackTheme.DrawDiamond(sb, p1, 5f, litColor * 1.2f);
                        HackTheme.DrawDiamond(sb, p1, 2.4f, HackTheme.BgDarkest * (alpha * clip));
                    }
                    else {
                        Color idleColor = qs == QueueSlotState.Queued
                            ? HackTheme.Uploading * (wireAlpha * 0.6f * clip)
                            : wireColor * (0.45f * clip);
                        HackTheme.DrawDashedLine(sb, p0, p1, 1f, idleColor, 4f, 6f);
                    }

                    //分组首行的主干节点
                    if (slotGroupHead[i]) {
                        HackTheme.DrawDiamondOutline(sb, new Vector2(trunkX, itemCY), 4f, 1f, wireColor * (0.9f * clip));
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
                        GetTrunkSpan(out float tTop, out float tBot);
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
                    if (GetClipFade(GetRowY(i)) <= 0.004f) continue;
                    float itemCY = GetRowY(i) + rowHeight * 0.5f;
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

        #region 页脚测量

        //描述换行；测高与绘制必须同参，否则测出来的高度对不上画出来的行
        private static string[] WrapDescription(QuickHackDef hack, int maxLines) {
            return VaultUtils.WrapTextArray(hack.Description.Value, FontAssets.MouseText.Value,
                RowWidth - 16f, FontDesc, maxLines, true);
        }

        private static float DescLineHeight()
            => FontAssets.MouseText.Value.MeasureString("汉").Y * FontDesc;

        private static float MetaLineHeight()
            => FontAssets.MouseText.Value.MeasureString("0").Y * FontMeta;

        /// <summary>
        /// 页脚参与布局的高度。只按无悬停态测，鼠标划过各行时整列才不会上下跳
        /// </summary>
        private static float MeasureStatusFooterHeight() {
            float hintH = FontAssets.MouseText.Value.MeasureString(HackTime.RightClickHint.Value).Y * FontHint;
            return FooterTopGap + 24f + hintH + 6f;
        }

        //悬停详情向下生长，行数按屏底余量收敛，装不下才用省略号
        private static int ResolveDetailMaxLines(float footerY) {
            float room = HackTheme.UIScreenH - HackTheme.BottomSafe
                - (footerY + 20f) - MetaLineHeight() - 8f;
            int lines = (int)(room / MathF.Max(DescLineHeight(), 1f));
            return Math.Clamp(lines, 1, 6);
        }

        #endregion

        //悬停协议详情
        private void DrawFooterDetail(SpriteBatch sb, Texture2D px, float alpha, float baseX, float footerY, QuickHackDef hack) {
            Color catColor = HackTheme.CategoryColor(hack.Category);

            //类别竖刻 + 协议名微标题
            sb.Draw(px, new Rectangle((int)baseX, (int)footerY + 2, 2, 14), HackTheme.SrcPixel, catColor * (alpha * 0.9f));
            Utils.DrawBorderString(sb, hack.DisplayName.Value, new Vector2((int)(baseX + 8), (int)(footerY - 2)),
                HackTheme.TextBright * alpha, 0.66f);

            string[] descLines = WrapDescription(hack, ResolveDetailMaxLines(footerY));
            float lineH = DescLineHeight();
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
                Color.Lerp(HackTheme.Accent, Color.White, 0.2f) * alpha, FontMeta);
            string upStr = HackTime.FooterUpload.Format(
                $"{PrivilegeEscalateState.ApplyUploadTime(hack.UploadTime, Main.LocalPlayer) / 60f:F1}");
            float costW = FontAssets.MouseText.Value.MeasureString(costStr).X * FontMeta;
            Utils.DrawBorderString(sb, upStr, new Vector2((int)(baseX + 8 + costW + 16), (int)metaY),
                HackTheme.TextBright * (alpha * 0.8f), FontMeta);

            //提权徽章：窗口内成本/耗时读数都被打折，标出来由是什么在生效
            int privLeft = PrivilegeEscalateState.RemainingSeconds(Main.myPlayer);
            if (privLeft > 0) {
                float upW = FontAssets.MouseText.Value.MeasureString(upStr).X * FontMeta;
                HackTheme.DrawBadge(sb,
                    new Vector2((int)(baseX + 8 + costW + 16 + upW + 14), (int)metaY),
                    $"ROOT {privLeft}s", new Color(140, 255, 170), alpha);
            }
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

            //伪十六进制 + 协议计数，无描边；按实测右对齐，本地化变长也不会压到状态文字
            var font = FontAssets.MouseText.Value;
            string tag = $"NET::0x{(int)(timer * 100) % 0xFFFF:X4}";
            string countStr = ResolveCountText(out bool partialOwned);
            //未持全时这行是玩家唯一能看出"库外还有协议"的地方，比常态计数略大略亮
            float countScale = partialOwned ? FontMeta : FontMicro;
            Color countColor = partialOwned
                ? Color.Lerp(HackTheme.TextNormal, HackTheme.Accent, 0.3f) * (alpha * 0.8f)
                : HackTheme.TextNormal * (alpha * 0.55f);
            float tagW = font.MeasureString(tag).X * FontMicro;
            float countW = font.MeasureString(countStr).X * countScale;
            float microRight = baseX + RowWidth;
            HackTheme.DrawRawText(sb, tag, new Vector2(microRight - tagW, footerY + 1),
                HackTheme.Accent * (alpha * 0.5f), FontMicro);
            HackTheme.DrawRawText(sb, countStr, new Vector2(microRight - countW, footerY + 18),
                countColor, countScale);

            //右键取消提示
            if (HackTime.CurrentScanTarget != null) {
                float hintPulse = MathF.Sin(timer * 1.8f) * 0.12f + 0.88f;
                Utils.DrawBorderString(sb, HackTime.RightClickHint.Value, new Vector2((int)baseX, (int)(footerY + 24f)),
                    HackTheme.TextBright * (alpha * hintPulse * 0.9f), FontHint);
            }
        }

        /// <summary>
        /// 协议计数文案。未持全时报"已解锁/总数"，未持有的协议不再占行，
        /// 这一行是玩家唯一能看出库外还有协议的地方
        /// </summary>
        private string ResolveCountText(out bool partialOwned) {
            partialOwned = ownedCount < QuickHackDef.Count;
            return partialOwned
                ? HackTime.ProtocolsOwned.Format(ownedCount, QuickHackDef.Count)
                : HackTime.Protocols.Format(displayCount);
        }

        #endregion

        #region 空态

        /// <summary>
        /// 库里没有能作用于本目标的协议：整列凭空消失会被读成 bug，
        /// 改画一张最小卡说清是"库里没有"，并保留协议计数
        /// </summary>
        private void DrawEmptyState(SpriteBatch sb, Texture2D px, float alpha) {
            float appear = Math.Clamp((revealTime - BaseEntryDelay) * 4f, 0f, 1f);
            if (appear < 0.01f) return;
            float a = alpha * appear;

            var font = FontAssets.MouseText.Value;
            float baseX = GetBaseX();
            string[] lines = VaultUtils.WrapTextArray(HackTime.NoProtocolHint.Value, font,
                RowWidth - 16f, FontDesc, 3, true);
            float lineH = DescLineHeight();
            float cardH = 24f + lines.Length * lineH + 6f + MetaLineHeight();

            //夹在 RAM 弧避让线与屏底安全线之间，纵向居中
            float maxY = MathF.Max(HackTheme.UIScreenH - HackTheme.BottomSafe - cardH, 0f);
            float minY = MathF.Min(ResolveArcClearY(), maxY);
            float y = MathHelper.Clamp((HackTheme.UIScreenH - cardH) * 0.5f, minY, maxY);

            //页眉沿用页脚那条分隔虚线与竖刻，读作同一张面板的一部分
            HackTheme.DrawDashedLine(sb, new Vector2(baseX, y - 6), new Vector2(baseX + RowWidth, y - 6),
                1f, HackTheme.Border * (a * 0.5f), 5f, 4f);
            sb.Draw(px, new Rectangle((int)baseX, (int)y + 2, 2, 14), HackTheme.SrcPixel,
                HackTheme.Border * (a * 0.9f));
            Utils.DrawBorderString(sb, HackTime.NoProtocolTitle.Value,
                new Vector2((int)(baseX + 8), (int)(y - 2)), HackTheme.TextNormal * a, 0.66f);

            float curY = y + 20f;
            for (int li = 0; li < lines.Length; li++) {
                if (string.IsNullOrEmpty(lines[li])) continue;
                Utils.DrawBorderString(sb, lines[li].TrimEnd('-', ' '),
                    new Vector2((int)(baseX + 8), (int)(curY + li * lineH)),
                    HackTheme.TextBright * (a * 0.8f), FontDesc);
            }

            string countStr = ResolveCountText(out bool partialOwned);
            float countW = font.MeasureString(countStr).X * FontMeta;
            Color countColor = partialOwned
                ? Color.Lerp(HackTheme.TextNormal, HackTheme.Accent, 0.3f) * (a * 0.8f)
                : HackTheme.TextNormal * (a * 0.55f);
            HackTheme.DrawRawText(sb, countStr,
                new Vector2(baseX + RowWidth - countW, curY + lines.Length * lineH + 4f),
                countColor, FontMeta);
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
