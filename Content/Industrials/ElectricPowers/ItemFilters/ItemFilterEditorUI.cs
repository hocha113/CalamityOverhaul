using CalamityOverhaul.Common;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.ItemFilters
{
    /// <summary>
    /// 通用过滤编辑面板(<see cref="IItemFilterHost"/>)<br/>
    /// 持物点格=加；空手点格=删；滚轮翻行；拖标题；右键/ESC关；宿主失效或过远自动关
    /// </summary>
    internal class ItemFilterEditorUI : UIHandle, ILocalizedModType
    {
        public static ItemFilterEditorUI Instance => UIHandleLoader.GetUIHandleOfType<ItemFilterEditorUI>();
        public string LocalizationCategory => "UI";

        public override bool CloseOnEscape => true;
        public override SoundStyle? OpenSound => CWRSound.ButtonZero with { Pitch = 0.2f, Volume = 0.6f };
        public override SoundStyle? CloseSound => CWRSound.ButtonZero with { Pitch = -0.15f, Volume = 0.5f };
        //命中与绘制统一走UI空间坐标
        public override Vector2 MousePosition => ItemFilterTheme.UIMouse;

        #region 本地化
        public static LocalizedText ModeWhitelistText { get; private set; }
        public static LocalizedText ModeBlacklistText { get; private set; }
        public static LocalizedText ClearText { get; private set; }
        public static LocalizedText UninstallText { get; private set; }
        public static LocalizedText CountFormat { get; private set; }
        public static LocalizedText EmptyHintText { get; private set; }
        public static LocalizedText OperateHintText { get; private set; }
        public static LocalizedText InstalledText { get; private set; }
        public static LocalizedText UninstalledText { get; private set; }

        public override void SetStaticDefaults() {
            ModeWhitelistText = this.GetLocalization(nameof(ModeWhitelistText), () => "白名单");
            ModeBlacklistText = this.GetLocalization(nameof(ModeBlacklistText), () => "黑名单");
            ClearText = this.GetLocalization(nameof(ClearText), () => "清空");
            UninstallText = this.GetLocalization(nameof(UninstallText), () => "卸载过滤");
            CountFormat = this.GetLocalization(nameof(CountFormat), () => "{0} 项");
            EmptyHintText = this.GetLocalization(nameof(EmptyHintText), () => "手持物品点击此处即可收录");
            OperateHintText = this.GetLocalization(nameof(OperateHintText), () => "持物点击=添加 · 点击条目=移除 · [右键/ESC]关闭");
            InstalledText = this.GetLocalization(nameof(InstalledText), () => "名单已安装");
            UninstalledText = this.GetLocalization(nameof(UninstalledText), () => "过滤器已卸载");
        }
        #endregion

        #region 状态
        internal IItemFilterHost Host { get; private set; }

        private float eased;
        private Rectangle panelRect;
        private Rectangle headerRect;
        private Rectangle gridViewport;
        private Rectangle scrollTrack;
        private Rectangle modeChipRect;
        private Rectangle clearButtonRect;
        private Rectangle uninstallButtonRect;

        //平滑滚动(像素)
        private float scrollPx;
        private float scrollTarget;

        private int hoverCellIndex = -1;
        private bool hoverGrid;
        private bool hoverMode;
        private bool hoverClear;
        private bool hoverUninstall;
        private readonly Dictionary<int, float> cellHoverEase = [];

        private bool dragging;
        private Vector2 dragOffset;

        //出场缓动:负值=按索引错开的延迟
        private readonly Dictionary<int, float> appearEase = [];
        private readonly Dictionary<int, float> duplicateFlash = [];

        //移除残影(纯视觉，数据在点击瞬间已删)
        private struct GhostEntry
        {
            public int ItemType;
            public Vector2 PanelOffset;
            public float Fade;
        }
        private readonly List<GhostEntry> ghosts = [];

        private const int RowStep = ItemFilterTheme.CellSize + ItemFilterTheme.CellGap;
        #endregion

        #region 开关

        /// <summary>打开；已开则换绑并保持面板位置</summary>
        public void OpenFor(IItemFilterHost host) {
            bool rebindOnly = IsOpen;
            Host = host;
            scrollPx = scrollTarget = 0f;
            ghosts.Clear();
            duplicateFlash.Clear();
            cellHoverEase.Clear();
            SeedAppearStagger();

            if (!rebindOnly) {
                Vector2 mouse = MousePosition;
                DrawPosition = new Vector2(
                    mouse.X - ItemFilterTheme.PanelWidth * 0.5f,
                    mouse.Y - ItemFilterTheme.PanelHeight * 0.5f);
                ClampPanelPosition();
                Open();
            }
        }

        /// <summary>同宿主再触发则关，否则打开</summary>
        public void ToggleFor(IItemFilterHost host) {
            if (IsOpen && ReferenceEquals(Host, host)) {
                Close();
            }
            else {
                OpenFor(host);
            }
        }

        protected override void OnClose() {
            dragging = false;
            hoverCellIndex = -1;
        }

        private void SeedAppearStagger() {
            appearEase.Clear();
            if (Host == null) {
                return;
            }
            IReadOnlyList<int> items = Host.Filter.OrderedItems;
            for (int i = 0; i < items.Count; i++) {
                appearEase[items[i]] = Math.Max(-1.2f, -i * 0.05f);
            }
        }

        private bool HostStillValid() {
            if (Host == null || !Host.FilterHostAlive) {
                return false;
            }
            if (Host.FilterHostWorldCenter is Vector2 worldCenter
                && player.Center.Distance(worldCenter) > ItemFilterTheme.KeepDistance + 200f) {
                return false;
            }
            return true;
        }

        #endregion

        #region 更新

        public override void Update() {
            if (!IsOpen && OpenProgress.Current <= 0.001f) {
                hoverInMainPage = false;
                return;
            }

            eased = VaultUtils.EaseOutCubic(MathHelper.Clamp(OpenProgress.Current, 0f, 1f));

            if (IsOpen && !HostStillValid()) {
                Close();
            }

            ClampPanelPosition();
            LayoutRects();

            hoverInMainPage = panelRect.Contains(MousePoint);
            if (hoverInMainPage && IsOpen) {
                player.mouseInterface = true;
                UIInputGuard.SuppressWeaponSwitch();
            }

            hoverCellIndex = -1;
            hoverGrid = hoverMode = hoverClear = hoverUninstall = false;

            bool interactive = IsOpen && eased > 0.85f && Host != null;
            if (interactive) {
                HandleDrag();
                if (!dragging) {
                    HandleWheel();
                    HandleHovers();
                    HandleClicks();

                    //右键面板空白处关
                    if (hoverInMainPage && keyRightPressState == KeyPressState.Pressed) {
                        Close();
                    }
                }
            }
            else {
                dragging = false;
            }

            UpdateScrollAndAnims();
        }

        private void ClampPanelPosition() {
            DrawPosition = new Vector2(
                MathHelper.Clamp(DrawPosition.X, 6f, Math.Max(6f, ItemFilterTheme.UIScreenW - ItemFilterTheme.PanelWidth - 6f)),
                MathHelper.Clamp(DrawPosition.Y, 6f, Math.Max(6f, ItemFilterTheme.UIScreenH - ItemFilterTheme.PanelHeight - 6f)));
        }

        private void LayoutRects() {
            panelRect = new Rectangle((int)DrawPosition.X, (int)DrawPosition.Y
                , ItemFilterTheme.PanelWidth, ItemFilterTheme.PanelHeight);
            UIHitBox = panelRect;

            headerRect = new Rectangle(panelRect.X, panelRect.Y, panelRect.Width, ItemFilterTheme.HeaderHeight);
            gridViewport = new Rectangle(panelRect.X + ItemFilterTheme.Padding
                , panelRect.Y + ItemFilterTheme.HeaderHeight
                , ItemFilterTheme.GridWidth, ItemFilterTheme.GridHeight);
            scrollTrack = new Rectangle(gridViewport.Right + 5, gridViewport.Y + 2, 4, gridViewport.Height - 4);

            int buttonY = gridViewport.Bottom + 8;
            modeChipRect = new Rectangle(gridViewport.X, buttonY, 112, 26);
            clearButtonRect = new Rectangle(modeChipRect.Right + 10, buttonY, 76, 26);
            uninstallButtonRect = new Rectangle(clearButtonRect.Right + 10, buttonY, 96, 26);
        }

        private void HandleDrag() {
            Vector2 mouse = MousePosition;
            if (!dragging && headerRect.Contains(mouse.ToPoint()) && keyLeftPressState == KeyPressState.Pressed) {
                dragging = true;
                dragOffset = DrawPosition - mouse;
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.3f });
            }

            if (dragging) {
                DrawPosition = mouse + dragOffset;
                if (keyLeftPressState is KeyPressState.Released or KeyPressState.None) {
                    dragging = false;
                }
            }
        }

        private void HandleWheel() {
            if (!hoverInMainPage) {
                return;
            }
            PlayerInput.LockVanillaMouseScroll("CalamityOverhaul/ItemFilterEditor");
            float maxScroll = MaxScroll();
            if (maxScroll <= 0f) {
                return;
            }
            int delta = MouseScrollDelta;
            if (delta != 0) {
                scrollTarget = MathHelper.Clamp(scrollTarget - Math.Sign(delta) * RowStep, 0f, maxScroll);
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.22f, Pitch = -0.2f });
            }
        }

        private void HandleHovers() {
            Point mouse = MousePoint;
            hoverGrid = gridViewport.Contains(mouse);
            hoverMode = modeChipRect.Contains(mouse);
            hoverClear = clearButtonRect.Contains(mouse) && !Host.Filter.IsEmpty;
            hoverUninstall = Host.CanUninstallFilter && uninstallButtonRect.Contains(mouse);

            if (hoverGrid && Main.mouseItem.IsAir) {
                int index = CellIndexAt(mouse);
                if (index >= 0 && index < Host.Filter.Count) {
                    hoverCellIndex = index;
                }
            }
        }

        private void HandleClicks() {
            if (keyLeftPressState != KeyPressState.Pressed) {
                return;
            }

            if (hoverMode) {
                Host.Filter.ToggleMode();
                Host.OnFilterChanged();
                SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = 0.2f });
                return;
            }

            if (hoverClear) {
                Host.Filter.Clear();
                Host.OnFilterChanged();
                ghosts.Clear();
                SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.7f, Pitch = -0.2f });
                return;
            }

            if (hoverUninstall) {
                Host.UninstallFilter();
                SoundEngine.PlaySound(CWRSound.Select with { Pitch = 0.2f });
                CombatText.NewText(player.Hitbox, ItemFilterTheme.Gold, UninstalledText.Value);
                Close();
                return;
            }

            if (!hoverGrid) {
                return;
            }

            //持物收录
            if (!Main.mouseItem.IsAir) {
                TryAddItem(Main.mouseItem.type);
                return;
            }

            //空手移除
            if (hoverCellIndex >= 0) {
                RemoveItemAt(hoverCellIndex);
            }
        }

        private void TryAddItem(int itemType) {
            if (Host.Filter.Add(itemType)) {
                Host.OnFilterChanged();
                appearEase[itemType] = 0f;
                scrollTarget = MaxScroll();
                SoundEngine.PlaySound(SoundID.Grab with { Volume = 0.8f, Pitch = 0.1f });
            }
            else if (Host.Filter.Contains(itemType)) {
                //重复收录闪一下
                duplicateFlash[itemType] = 1f;
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.5f, Pitch = -0.35f });
            }
            else {
                SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.6f, Pitch = -0.3f });
            }
        }

        private void RemoveItemAt(int index) {
            IReadOnlyList<int> items = Host.Filter.OrderedItems;
            if (index < 0 || index >= items.Count) {
                return;
            }
            int itemType = items[index];

            //先删数据，残影仅视觉
            Rectangle cell = CellRect(index);
            ghosts.Add(new GhostEntry {
                ItemType = itemType,
                PanelOffset = new Vector2(cell.X - panelRect.X, cell.Y - panelRect.Y),
                Fade = 1f
            });

            Host.Filter.Remove(itemType);
            Host.OnFilterChanged();
            appearEase.Remove(itemType);
            SoundEngine.PlaySound(SoundID.Grab with { Volume = 0.9f, Pitch = -0.15f });
        }

        private void UpdateScrollAndAnims() {
            scrollTarget = MathHelper.Clamp(scrollTarget, 0f, MaxScroll());
            scrollPx = MathHelper.Lerp(scrollPx, scrollTarget, 0.25f);

            //出场
            if (Host != null) {
                IReadOnlyList<int> items = Host.Filter.OrderedItems;
                for (int i = 0; i < items.Count; i++) {
                    int type = items[i];
                    if (!appearEase.TryGetValue(type, out float ease)) {
                        ease = Math.Max(-0.6f, -i * 0.02f);
                    }
                    appearEase[type] = Math.Min(1f, ease + 0.09f);
                }
            }

            //悬停
            if (Host != null) {
                IReadOnlyList<int> items = Host.Filter.OrderedItems;
                for (int i = 0; i < items.Count; i++) {
                    int type = items[i];
                    float target = i == hoverCellIndex ? 1f : 0f;
                    cellHoverEase.TryGetValue(type, out float cur);
                    float next = MathHelper.Lerp(cur, target, 0.28f);
                    if (next < 0.005f && target == 0f) {
                        cellHoverEase.Remove(type);
                    }
                    else {
                        cellHoverEase[type] = next;
                    }
                }
            }

            //闪烁衰减
            if (duplicateFlash.Count > 0) {
                foreach (int key in new List<int>(duplicateFlash.Keys)) {
                    float value = duplicateFlash[key] - 0.06f;
                    if (value <= 0f) {
                        duplicateFlash.Remove(key);
                    }
                    else {
                        duplicateFlash[key] = value;
                    }
                }
            }

            //残影衰减
            for (int i = ghosts.Count - 1; i >= 0; i--) {
                GhostEntry ghost = ghosts[i];
                ghost.Fade -= 0.09f;
                if (ghost.Fade <= 0f) {
                    ghosts.RemoveAt(i);
                }
                else {
                    ghosts[i] = ghost;
                }
            }
        }

        #endregion

        #region 布局工具

        private int ContentRows => Host == null ? 0
            : (Host.Filter.Count + ItemFilterTheme.GridCols - 1) / ItemFilterTheme.GridCols;

        private float MaxScroll()
            => Math.Max(0f, ContentRows * RowStep - ItemFilterTheme.CellGap - gridViewport.Height);

        private Rectangle CellRect(int index) {
            int row = index / ItemFilterTheme.GridCols;
            int col = index % ItemFilterTheme.GridCols;
            return new Rectangle(
                gridViewport.X + col * RowStep,
                gridViewport.Y + row * RowStep - (int)scrollPx,
                ItemFilterTheme.CellSize, ItemFilterTheme.CellSize);
        }

        private int CellIndexAt(Point mouse) {
            int localX = mouse.X - gridViewport.X;
            int localY = mouse.Y - gridViewport.Y + (int)scrollPx;
            if (localX < 0 || localY < 0) {
                return -1;
            }
            int col = localX / RowStep;
            int row = localY / RowStep;
            if (col >= ItemFilterTheme.GridCols) {
                return -1;
            }
            //须落在格子内(排除格间空隙)
            if (localX % RowStep >= ItemFilterTheme.CellSize || localY % RowStep >= ItemFilterTheme.CellSize) {
                return -1;
            }
            return row * ItemFilterTheme.GridCols + col;
        }

        #endregion

        #region 绘制

        public override void Draw(SpriteBatch spriteBatch) {
            if (OpenProgress.Current <= 0.001f || Host == null) {
                return;
            }
            float alpha = eased;

            ItemFilterRenderer.DrawChrome(spriteBatch, panelRect, alpha, GlobalTimer);
            DrawHeader(spriteBatch, alpha);
            DrawGridClipped(spriteBatch, alpha);

            if (MaxScroll() > 0f) {
                ItemFilterRenderer.DrawScrollbar(spriteBatch, scrollTrack
                    , scrollPx / MaxScroll()
                    , gridViewport.Height / (float)Math.Max(1, ContentRows * RowStep)
                    , alpha);
            }

            DrawButtons(spriteBatch, alpha);
            DrawFooter(spriteBatch, alpha);
            ShowHoveredTooltip();
        }

        private void DrawHeader(SpriteBatch sb, float alpha) {
            string title = Host.FilterHostName;
            Vector2 titlePos = new(panelRect.X + ItemFilterTheme.Padding, panelRect.Y + 14);
            Color glow = ItemFilterTheme.EdgeBright * (alpha * 0.5f);
            for (int i = 0; i < 4; i++) {
                Vector2 offset = (MathHelper.TwoPi * i / 4f).ToRotationVector2() * 1.6f;
                Utils.DrawBorderString(sb, title, titlePos + offset, glow, 0.85f);
            }
            Utils.DrawBorderString(sb, title, titlePos, ItemFilterTheme.TextWarm * alpha, 0.85f);

            string count = CountFormat.Format(Host.Filter.Count);
            Vector2 countSize = FontAssets.MouseText.Value.MeasureString(count) * 0.68f;
            Utils.DrawBorderString(sb, count
                , new Vector2(panelRect.Right - ItemFilterTheme.Padding - countSize.X, panelRect.Y + 18)
                , ItemFilterTheme.Label * alpha, 0.68f);

            ItemFilterRenderer.DrawDivider(sb
                , new Vector2(panelRect.X + ItemFilterTheme.Padding, panelRect.Y + ItemFilterTheme.HeaderHeight - 10)
                , panelRect.Width - ItemFilterTheme.Padding * 2, alpha, GlobalTimer);
        }

        private void DrawGridClipped(SpriteBatch sb, float alpha) {
            IReadOnlyList<int> items = Host.Filter.OrderedItems;

            if (items.Count == 0 && ghosts.Count == 0) {
                ItemFilterRenderer.DrawEmptyHint(sb, gridViewport, EmptyHintText.Value, alpha);
                return;
            }

            //裁剪到网格视口(变换到后台缓冲坐标)
            sb.End();
            Vector2 clipPos = Vector2.Transform(new Vector2(gridViewport.X, gridViewport.Y), Main.UIScaleMatrix);
            Vector2 clipSize = Vector2.Transform(new Vector2(gridViewport.Width, gridViewport.Height + 2), Main.UIScaleMatrix)
                - Vector2.Transform(Vector2.Zero, Main.UIScaleMatrix);
            Rectangle scissor = new((int)clipPos.X, (int)clipPos.Y, (int)clipSize.X, (int)clipSize.Y);
            scissor = Rectangle.Intersect(scissor, sb.GraphicsDevice.Viewport.Bounds);
            Rectangle original = sb.GraphicsDevice.ScissorRectangle;
            RasterizerState rasterizer = new() { ScissorTestEnable = true };

            sb.GraphicsDevice.ScissorRectangle = scissor;
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, rasterizer, null, Main.UIScaleMatrix);

            Color modeAccent = ItemFilterTheme.ModeAccent(Host.Filter.Mode);
            for (int i = 0; i < items.Count; i++) {
                Rectangle cell = CellRect(i);
                if (cell.Bottom < gridViewport.Y || cell.Y > gridViewport.Bottom) {
                    continue;
                }
                int type = items[i];
                appearEase.TryGetValue(type, out float ease);
                cellHoverEase.TryGetValue(type, out float hover);
                duplicateFlash.TryGetValue(type, out float flash);
                ItemFilterRenderer.DrawCell(sb, cell, type, Math.Max(0f, ease), hover, flash, alpha, modeAccent);
            }

            foreach (GhostEntry ghost in ghosts) {
                Rectangle cell = new((int)(panelRect.X + ghost.PanelOffset.X), (int)(panelRect.Y + ghost.PanelOffset.Y)
                    , ItemFilterTheme.CellSize, ItemFilterTheme.CellSize);
                ItemFilterRenderer.DrawGhost(sb, cell, ghost.ItemType, ghost.Fade, alpha);
            }

            sb.End();
            sb.GraphicsDevice.ScissorRectangle = original;
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);
        }

        private void DrawButtons(SpriteBatch sb, float alpha) {
            ItemFilterMode mode = Host.Filter.Mode;
            string modeLabel = mode == ItemFilterMode.Whitelist ? ModeWhitelistText.Value : ModeBlacklistText.Value;
            ItemFilterRenderer.DrawModeChip(sb, modeChipRect, modeLabel, mode, hoverMode, alpha, GlobalTimer);

            bool clearEnabled = !Host.Filter.IsEmpty;
            ItemFilterRenderer.DrawButton(sb, clearButtonRect, ClearText.Value, hoverClear
                , alpha * (clearEnabled ? 1f : 0.45f), ItemFilterTheme.Danger);

            if (Host.CanUninstallFilter) {
                ItemFilterRenderer.DrawButton(sb, uninstallButtonRect, UninstallText.Value, hoverUninstall
                    , alpha, ItemFilterTheme.Gold);
            }
        }

        private void DrawFooter(SpriteBatch sb, float alpha) {
            Utils.DrawBorderString(sb, OperateHintText.Value
                , new Vector2(panelRect.X + ItemFilterTheme.Padding, panelRect.Bottom - ItemFilterTheme.FooterHeight - 4)
                , ItemFilterTheme.TextDim * (alpha * 0.9f), 0.56f);
        }

        private void ShowHoveredTooltip() {
            if (hoverCellIndex < 0 || Host == null || hoverCellIndex >= Host.Filter.Count) {
                return;
            }
            int type = Host.Filter.OrderedItems[hoverCellIndex];
            if (ContentSamples.ItemsByType.TryGetValue(type, out Item sample)) {
                Main.HoverItem = sample.Clone();
                Main.hoverItemName = Main.HoverItem.Name;
            }
        }

        #endregion
    }
}
