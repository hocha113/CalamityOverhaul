using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions;
using CalamityOverhaul.Content.TimeFreezes;
using CalamityOverhaul.Content.UIs;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI
{
    /// <summary>
    /// 铭谱：改铭台旁那本线装册子摊开的样子。<br/>
    /// 左页名录（按槽位分卷、暗刻未得、朱点标现铭），右页详情（大字形按笔序凿现 + 全文或线索）。<br/>
    /// 只读，凿刻仍只在改铭台做，此处不动刀。<br/>
    /// 与改铭台是"取书/放书"的关系而非姊妹屏：开册静默收台，合册静默回台
    /// </summary>
    internal sealed class OniMeiCodexUI : UIHandle, ILocalizedModType, IFullScreenUIHandle
    {
        public string LocalizationCategory => "Legend.OnikiriText";

        FullScreenUIDomain IFullScreenUIHandle.FullScreenDomain => FullScreenUIDomain.Onikiri;
        public static OniMeiCodexUI Instance => UIHandleLoader.GetUIHandleOfType<OniMeiCodexUI>();

        private const string FreezeReason = "OniMeiCodex";
        /// <summary>选中换页后大字形重凿一遍的帧数</summary>
        private const float DetailRevealFrames = 26f;
        /// <summary>全部/茎铭/樋位/雕位</summary>
        private const int TabCount = 4;

        #region 本地化
        public static LocalizedText TitleText { get; private set; }
        public static LocalizedText CloseTagText { get; private set; }
        public static LocalizedText CloseHintFormat { get; private set; }
        public static LocalizedText TabAll { get; private set; }
        public static LocalizedText TallyFormat { get; private set; }
        public static LocalizedText TallySlotFormat { get; private set; }
        public static LocalizedText PageFormat { get; private set; }
        public static LocalizedText EmptyPage { get; private set; }
        public static LocalizedText LockedTitle { get; private set; }
        public static LocalizedText SectionAcquire { get; private set; }
        public static LocalizedText SectionProgress { get; private set; }
        public static LocalizedText SectionSource { get; private set; }
        public static LocalizedText HiddenBody { get; private set; }
        public static LocalizedText EngravedMark { get; private set; }
        public static LocalizedText SourceFactory { get; private set; }
        public static LocalizedText SourceGiftFormat { get; private set; }
        public static LocalizedText SourceGiftUnknown { get; private set; }
        public static LocalizedText SourceGiftJoin { get; private set; }
        public static LocalizedText SourceDeed { get; private set; }
        public static LocalizedText SourceUnknown { get; private set; }
        public static LocalizedText ProgressSettled { get; private set; }
        public static LocalizedText ProgressWaiting { get; private set; }

        public override void SetStaticDefaults() {
            TitleText = this.GetLocalization(nameof(TitleText), () => "铭 谱");
            CloseTagText = this.GetLocalization(nameof(CloseTagText), () => "合 卷");
            CloseHintFormat = this.GetLocalization(nameof(CloseHintFormat), () => "ESC · {0} · 点击册外 归台");
            TabAll = this.GetLocalization(nameof(TabAll), () => "全册");
            TallyFormat = this.GetLocalization(nameof(TallyFormat), () => "已得 {0} / {1}");
            TallySlotFormat = this.GetLocalization(nameof(TallySlotFormat), () => "{0} {1}/{2}");
            PageFormat = this.GetLocalization(nameof(PageFormat), () => "{0} / {1}");
            EmptyPage = this.GetLocalization(nameof(EmptyPage), () => "此卷无铭");
            LockedTitle = this.GetLocalization(nameof(LockedTitle), () => "未 凿");
            SectionAcquire = this.GetLocalization(nameof(SectionAcquire), () => "所寻");
            SectionProgress = this.GetLocalization(nameof(SectionProgress), () => "縁分");
            SectionSource = this.GetLocalization(nameof(SectionSource), () => "来路");
            HiddenBody = this.GetLocalization(nameof(HiddenBody),
                () => "未凿之铭，赋效与代价皆不载于谱。先得其铭，再读其文");
            EngravedMark = this.GetLocalization(nameof(EngravedMark), () => "此刻在刀");
            SourceFactory = this.GetLocalization(nameof(SourceFactory), () => "自鸟居下拔出时便在刀上");
            SourceGiftFormat = this.GetLocalization(nameof(SourceGiftFormat), () => "斩落「{0}」之后，绯真夜会把拓本递来");
            SourceGiftUnknown = this.GetLocalization(nameof(SourceGiftUnknown), () => "绯真夜会在某一夜把拓本递来");
            SourceGiftJoin = this.GetLocalization(nameof(SourceGiftJoin), () => "」或「");
            SourceDeed = this.GetLocalization(nameof(SourceDeed), () => "无人相赠。此铭须持刀自证");
            SourceUnknown = this.GetLocalization(nameof(SourceUnknown), () => "来历不详，谱上无载");
            ProgressSettled = this.GetLocalization(nameof(ProgressSettled), () => "縁分已结");
            ProgressWaiting = this.GetLocalization(nameof(ProgressWaiting), () => "尚待其时");
        }
        #endregion

        public override bool CloseOnEscape => true;
        /// <summary>压在改铭台之上（改铭台为 2f）</summary>
        public override float RenderPriority => 2.1f;
        public override SoundStyle? OpenSound => CWRSound.ButtonZero with { Pitch = -0.25f, Volume = 0.45f };
        /// <summary>合卷不出声，交接的唯一一声留给改铭台的开台音</summary>
        public override SoundStyle? CloseSound => null;
        public override Vector2 MousePosition => OnikiriUITheme.UIMouse;

        //====状态====
        /// <summary>合册后要不要把台重新摆开</summary>
        private bool returnToMei;
        /// <summary>-1=全册，否则为槽序</summary>
        private int filterSlot = -1;
        private int page;
        private int selected;
        private int hoverCell = -1;
        private int hoverTab = -1;
        private int hoverArrow;
        private float closeTagHover;
        private float shaderTime;
        /// <summary>&lt;0 完成态；0~1 按笔序凿现</summary>
        private float detailReveal = -1f;
        private string revealedKey = "";
        /// <summary>右页正文滚动量</summary>
        private float detailScroll;
        private float detailMaxScroll;

        private readonly List<OniMeiCodexRow> rows = [];
        private readonly float[] cellEase = new float[OnikiriUITheme.CodexPageCells];
        private readonly float[] tabEase = new float[TabCount];

        //====布局====
        private Rectangle bookRect;
        private Rectangle leftPage;
        private Rectangle rightPage;
        private Rectangle closeTagRect;
        private Rectangle prevArrow;
        private Rectangle nextArrow;
        private readonly Rectangle[] tabRects = new Rectangle[TabCount];
        private readonly Rectangle[] cellRects = new Rectangle[OnikiriUITheme.CodexPageCells];

        internal float ShaderTime => shaderTime;
        internal IReadOnlyList<OniMeiCodexRow> Rows => rows;

        private int PageCount => Math.Max(1,
            (rows.Count + OnikiriUITheme.CodexPageCells - 1) / OnikiriUITheme.CodexPageCells);
        private int PageStart => page * OnikiriUITheme.CodexPageCells;

        /// <summary>自改铭台取书：开册并记得合册后要摆回台</summary>
        internal static void OpenFromStand() {
            OniMeiCodexUI codex = Instance;
            if (codex == null || codex.IsOpen) {
                return;
            }
            codex.returnToMei = true;
            codex.Open();
        }

        public override void OnEnterWorld() {
            if (IsOpen) {
                returnToMei = false;
                Close();
            }
            SnapOpenProgress();
        }

        protected override void OnOpen() {
            filterSlot = -1;
            page = 0;
            selected = 0;
            hoverCell = -1;
            hoverTab = -1;
            hoverArrow = 0;
            closeTagHover = 0f;
            detailReveal = 0f;
            revealedKey = "";
            detailScroll = 0f;
            detailMaxScroll = 0f;
            Array.Clear(cellEase, 0, cellEase.Length);
            Array.Clear(tabEase, 0, tabEase.Length);
            //取书即收台：静默收，切换只响开册这一声
            if (OniMeiUI.Instance?.IsOpen ?? false) {
                OniMeiUI.Instance.SilentSwap = true;
                OniMeiUI.Instance.Close();
                OniMeiUI.Instance.SilentSwap = false;
            }
            Rebuild();
            LayoutCompute();
            if (VaultUtils.isSinglePlayer) {
                WorldFreezeSystem.Activate(FreezeReason);
            }
        }

        protected override void OnClose() {
            rows.Clear();
            detailScroll = 0f;
            detailMaxScroll = 0f;
            if (VaultUtils.isSinglePlayer) {
                WorldFreezeSystem.Deactivate(FreezeReason);
            }
            if (!returnToMei) {
                return;
            }
            returnToMei = false;
            //放书归台；台自己的开台音就是这次交接的唯一一声
            if (player != null && player.active && !player.dead) {
                OniMeiUI.Instance?.Open();
                return;
            }
            SoundEngine.PlaySound(SoundID.MenuClose with { Pitch = -0.35f, Volume = 0.4f });
        }

        /// <summary>按当前分卷重取行；选中项尽量跟着原来那一枚走</summary>
        private void Rebuild() {
            string keep = selected >= 0 && selected < rows.Count ? rows[selected].Key : "";
            OniMeiSlotKind? slot = filterSlot < 0 ? null : (OniMeiSlotKind)filterSlot;
            OniMeiCodexData.Build(player, slot, rows);
            selected = 0;
            if (keep.Length > 0) {
                for (int i = 0; i < rows.Count; i++) {
                    if (rows[i].Key == keep) {
                        selected = i;
                        break;
                    }
                }
            }
            page = Math.Clamp(selected / Math.Max(1, OnikiriUITheme.CodexPageCells), 0, PageCount - 1);
        }

        public override void Update() {
            if (IsOpen) {
                player.mouseInterface = true;
            }
        }

        public override void LogicUpdate() {
            if (IsOpen) {
                player.mouseInterface = true;
                UIInputGuard.SuppressWeaponSwitch();
                if (!player.active || player.dead) {
                    returnToMei = false;
                    Close();
                }
            }

            float a = OpenProgress;
            if (a < 0.01f) {
                return;
            }
            shaderTime += 1f / 60f;
            LayoutCompute();

            //名录每帧重取:刀縁进度与现铭都会在开册期间变
            if (IsOpen) {
                RefreshRows();
            }
            TickReveal();
            UpdateDetailScrollMetrics();
            UpdateInteraction(a);
        }

        /// <summary>按当前右页测正文高度，钳制滚动</summary>
        private void UpdateDetailScrollMetrics() {
            if (selected < 0 || selected >= rows.Count) {
                detailMaxScroll = 0f;
                detailScroll = 0f;
                return;
            }
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            Rectangle body = OniMeiCodexRenderer.DetailBodyRect(font, rightPage, rows[selected]);
            float wrapW = Math.Max(32f, body.Width - 8f);
            float contentH = OniMeiCodexRenderer.MeasureDetailBody(font, wrapW, rows[selected]);
            detailMaxScroll = Math.Max(0f, contentH - body.Height);
            detailScroll = Math.Clamp(detailScroll, 0f, detailMaxScroll);
        }

        /// <summary>就地刷新行的读数，不重排也不动选中</summary>
        private void RefreshRows() {
            OniMeiSlotKind? slot = filterSlot < 0 ? null : (OniMeiSlotKind)filterSlot;
            OniMeiCodexData.Build(player, slot, rows);
            if (selected >= rows.Count) {
                selected = Math.Max(0, rows.Count - 1);
            }
        }

        /// <summary>换了一枚就重凿一遍字形</summary>
        private void TickReveal() {
            string key = selected >= 0 && selected < rows.Count ? rows[selected].Key : "";
            if (key != revealedKey) {
                revealedKey = key;
                detailReveal = key.Length > 0 ? 0f : -1f;
                detailScroll = 0f;
            }
            if (detailReveal >= 0f) {
                detailReveal += 1f / DetailRevealFrames;
                if (detailReveal >= 1f) {
                    detailReveal = -1f;
                }
            }
        }

        private void LayoutCompute() {
            float sw = OnikiriUITheme.UIScreenW;
            float sh = OnikiriUITheme.UIScreenH;
            float bookW = Math.Min(OnikiriUITheme.CodexBookMaxW, sw * OnikiriUITheme.CodexBookWRatio);
            float bookH = Math.Min(OnikiriUITheme.CodexBookMaxH, sh * OnikiriUITheme.CodexBookHRatio);
            //开册时自下抬起一线，落定即停
            float rise = (1f - VaultUtils.EaseOutCubic(MathHelper.Clamp(OpenProgress, 0f, 1f))) * 26f;
            bookRect = new Rectangle(
                (int)((sw - bookW) * 0.5f),
                (int)((sh - bookH) * 0.46f + rise),
                (int)bookW, (int)bookH);

            float pad = OnikiriUITheme.CodexPagePad;
            int halfW = (int)(bookW * 0.5f - pad * 1.5f);
            leftPage = new Rectangle(bookRect.X + (int)pad, bookRect.Y + (int)pad, halfW,
                bookRect.Height - (int)(pad * 2f));
            rightPage = new Rectangle(bookRect.Right - (int)pad - halfW, leftPage.Y, halfW, leftPage.Height);

            //页签骑在左页顶缘之上
            float tabW = leftPage.Width / (float)TabCount;
            for (int i = 0; i < TabCount; i++) {
                tabRects[i] = new Rectangle(
                    leftPage.X + (int)(tabW * i) + 2,
                    leftPage.Y - (int)OnikiriUITheme.CodexTabH + 2,
                    (int)tabW - 4, (int)OnikiriUITheme.CodexTabH);
            }

            //名录格：CodexGridCols × CodexGridRows，收在页签之下、翻页角标之上
            int gridTop = leftPage.Y + (int)OnikiriUITheme.CodexTallyH;
            int gridBottom = leftPage.Bottom - (int)OnikiriUITheme.CodexArrowH;
            float cellW = leftPage.Width / (float)OnikiriUITheme.CodexGridCols;
            float cellH = (gridBottom - gridTop) / (float)OnikiriUITheme.CodexGridRows;
            for (int i = 0; i < cellRects.Length; i++) {
                int col = i % OnikiriUITheme.CodexGridCols;
                int row = i / OnikiriUITheme.CodexGridCols;
                cellRects[i] = new Rectangle(
                    leftPage.X + (int)(cellW * col),
                    gridTop + (int)(cellH * row),
                    (int)cellW, (int)cellH);
            }

            int arrowW = 46;
            prevArrow = new Rectangle(leftPage.X, gridBottom, arrowW, (int)OnikiriUITheme.CodexArrowH);
            nextArrow = new Rectangle(leftPage.Right - arrowW, gridBottom, arrowW,
                (int)OnikiriUITheme.CodexArrowH);

            closeTagRect = new Rectangle(bookRect.Right - 96, bookRect.Bottom - 6, 88, 34);
        }

        private void UpdateInteraction(float a) {
            bool live = IsOpen && a > 0.9f;
            Vector2 mp = MousePosition;

            //教程焦点:页眉收集度那一行,展读时的落眼处
            if (Tutorial.OnikiriTutorialLead.IsActive) {
                Tutorial.OnikiriTutorialTargets.Publish(Tutorial.OnikiriTutorialTargets.Tag_CodexTally,
                    new Rectangle(leftPage.X, leftPage.Y - (int)OnikiriUITheme.CodexTabH,
                        leftPage.Width, (int)(OnikiriUITheme.CodexTabH + OnikiriUITheme.CodexTallyH)));
            }

            //页签
            hoverTab = -1;
            for (int i = 0; i < TabCount; i++) {
                bool hover = live && tabRects[i].Contains(mp.ToPoint());
                if (hover) {
                    hoverTab = i;
                }
                tabEase[i] = MathHelper.Lerp(tabEase[i], hover ? 1f : 0f, 0.22f);
            }

            //名录格
            hoverCell = -1;
            for (int i = 0; i < cellRects.Length; i++) {
                int index = PageStart + i;
                bool hover = live && index < rows.Count && cellRects[i].Contains(mp.ToPoint());
                if (hover) {
                    hoverCell = i;
                }
                cellEase[i] = MathHelper.Lerp(cellEase[i], hover ? 1f : 0f, 0.24f);
            }

            //翻页角标
            hoverArrow = 0;
            if (live && PageCount > 1) {
                if (page > 0 && prevArrow.Contains(mp.ToPoint())) {
                    hoverArrow = -1;
                }
                else if (page < PageCount - 1 && nextArrow.Contains(mp.ToPoint())) {
                    hoverArrow = 1;
                }
            }

            bool closeHover = live && closeTagRect.Contains(mp.ToPoint());
            closeTagHover = MathHelper.Lerp(closeTagHover, closeHover ? 1f : 0f, 0.2f);

            //滚轮：悬停右页且正文溢出 → 滚详情；否则左页翻页
            if (live) {
                int delta = PlayerInput.ScrollWheelDeltaForUI;
                if (delta != 0) {
                    bool hoverDetail = rightPage.Contains(mp.ToPoint());
                    if (hoverDetail && detailMaxScroll > 0.5f) {
                        detailScroll = Math.Clamp(detailScroll - delta * 0.3f, 0f, detailMaxScroll);
                        PlayerInput.LockVanillaMouseScroll("CalamityOverhaul/OniMeiCodex");
                    }
                    else if (PageCount > 1 && !hoverDetail) {
                        TurnPage(delta > 0 ? -1 : 1);
                    }
                }
            }

            if (!live || keyLeftPressState != KeyPressState.Pressed) {
                return;
            }

            if (closeHover) {
                Close();
                return;
            }
            if (hoverArrow != 0) {
                TurnPage(hoverArrow);
                return;
            }
            if (hoverTab >= 0) {
                SelectTab(hoverTab);
                return;
            }
            if (hoverCell >= 0) {
                int index = PageStart + hoverCell;
                if (index != selected && index < rows.Count) {
                    selected = index;
                    SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = 0.25f, Volume = 0.35f });
                }
                return;
            }
            //点册外归台
            if (!bookRect.Contains(mp.ToPoint())) {
                Close();
            }
        }

        private void TurnPage(int step) {
            int next = Math.Clamp(page + step, 0, PageCount - 1);
            if (next == page) {
                return;
            }
            page = next;
            Array.Clear(cellEase, 0, cellEase.Length);
            SoundEngine.PlaySound(SoundID.Item55 with { Pitch = 0.45f, Volume = 0.30f });
        }

        private void SelectTab(int tab) {
            int slot = tab == 0 ? -1 : tab - 1;
            if (slot == filterSlot) {
                return;
            }
            filterSlot = slot;
            page = 0;
            detailScroll = 0f;
            Array.Clear(cellEase, 0, cellEase.Length);
            Rebuild();
            SoundEngine.PlaySound(SoundID.Item55 with { Pitch = 0.2f, Volume = 0.32f });
        }

        /// <summary>页签题字：全册 + 三槽（槽名与改铭台共用一份）</summary>
        internal static string TabLabel(int tab) => tab switch {
            0 => TabAll?.Value ?? "",
            1 => OniMeiUI.SlotNakago?.Value ?? "",
            2 => OniMeiUI.SlotHi?.Value ?? "",
            _ => OniMeiUI.SlotHorimono?.Value ?? "",
        };

        public override void Draw(SpriteBatch spriteBatch) {
            float a = OpenProgress;
            if (a < 0.01f) {
                return;
            }
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);

            Rectangle full = new(0, 0, (int)OnikiriUITheme.UIScreenW + 2, (int)OnikiriUITheme.UIScreenH + 2);
            spriteBatch.Draw(pixel, full, src, Color.Black * (a * 0.74f));

            float contentA = MathHelper.Clamp((a - 0.35f) / 0.65f, 0f, 1f);
            OniMeiCodexRenderer.DrawBook(spriteBatch, bookRect, leftPage, rightPage, a, shaderTime);
            if (contentA <= 0.01f) {
                return;
            }

            //页眉：全册收集度 + 三槽分计
            OniMeiCodexRenderer.DrawTally(spriteBatch, font, leftPage, player, contentA);

            //页签
            for (int i = 0; i < TabCount; i++) {
                OniMeiCodexRenderer.DrawTab(spriteBatch, font, tabRects[i], i, TabLabel(i),
                    filterSlot == (i == 0 ? -1 : i - 1), tabEase[i], contentA);
            }

            //名录格
            if (rows.Count == 0) {
                OniMeiCodexRenderer.DrawPaperInk(spriteBatch, font, EmptyPage.Value,
                    new Vector2(leftPage.Center.X, leftPage.Center.Y),
                    OniMeiCodexRenderer.PaperAsh, 0.95f, contentA, 0.5f, 0.5f);
            }
            for (int i = 0; i < cellRects.Length; i++) {
                int index = PageStart + i;
                if (index >= rows.Count) {
                    break;
                }
                OniMeiCodexRenderer.DrawCell(spriteBatch, font, cellRects[i], rows[index],
                    index == selected, cellEase[i], contentA, shaderTime);
            }

            //翻页
            if (PageCount > 1) {
                OniMeiCodexRenderer.DrawPager(spriteBatch, font, prevArrow, nextArrow,
                    PageFormat.Format(page + 1, PageCount), page > 0, page < PageCount - 1,
                    hoverArrow, contentA);
            }

            //右页详情（Scissor 正文 + 滚轮偏移）
            if (selected >= 0 && selected < rows.Count) {
                float contentH = OniMeiCodexRenderer.DrawDetail(spriteBatch, font, rightPage, rows[selected],
                    detailReveal, detailScroll, contentA, shaderTime);
                Rectangle body = OniMeiCodexRenderer.DetailBodyRect(font, rightPage, rows[selected]);
                detailMaxScroll = Math.Max(0f, contentH - body.Height);
                detailScroll = Math.Clamp(detailScroll, 0f, detailMaxScroll);
            }

            //合卷牌与提示
            OniMeiCodexRenderer.DrawCloseTag(spriteBatch, font, closeTagRect, CloseTagText.Value,
                closeTagHover, contentA);
            string hint = CloseHintFormat.Format(CloseTagText.Value.Replace(" ", ""));
            OniMeiCodexRenderer.DrawPaperInk(spriteBatch, font, hint,
                new Vector2(bookRect.Center.X, bookRect.Bottom + 26f),
                OnikiriUITheme.Paper, 0.86f, contentA * 0.9f, 0.5f, 0.5f);
        }
    }
}
