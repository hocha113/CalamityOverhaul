using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Narrative.Presentation.Skins.Draedon;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Draedon.PQCDs.DraedonShops
{
    /// <summary>右侧滑入交换终端</summary>
    internal class DraedonShopUI : UIHandle, ILocalizedModType
    {
        public static DraedonShopUI Instance => UIHandleLoader.GetUIHandleOfType<DraedonShopUI>();
        public string LocalizationCategory => "UI";

        public override bool CloseOnEscape => true;
        public override SoundStyle? OpenSound => CWRSound.ButtonZero with { Pitch = 0.25f };
        public override SoundStyle? CloseSound => CWRSound.ButtonZero with { Pitch = -0.1f };
        //命中判定走UI空间
        public override Vector2 MousePosition => DraedonShopTheme.UIMouse;

        #region 本地化
        public static LocalizedText TitleText { get; private set; }
        public static LocalizedText FundsLabelText { get; private set; }
        public static LocalizedText BuyActionText { get; private set; }
        public static LocalizedText HintText { get; private set; }
        public static LocalizedText EmptyText { get; private set; }

        public override void SetStaticDefaults() {
            TitleText = this.GetLocalization(nameof(TitleText), () => "DRAEDON.EXCHANGE");
            FundsLabelText = this.GetLocalization(nameof(FundsLabelText), () => "余额");
            BuyActionText = this.GetLocalization(nameof(BuyActionText), () => "购买");
            HintText = this.GetLocalization(nameof(HintText), () => "[左键] 购买 · 按住连续购买 · [ESC] 关闭");
            EmptyText = this.GetLocalization(nameof(EmptyText), () => "暂无可交换的货品");
        }
        #endregion

        #region 状态
        private readonly DraedonPanelState state = new() {
            TechSideMargin = DraedonShopTheme.SidePadding,
            DataSpawnInterval = 24,
            MaxDataParticles = 12,
            CircuitSpawnInterval = 34,
            MaxCircuitNodes = 6,
            ParticleInsetY = 50f
        };

        private readonly List<ShopItem> shopItems = [];

        private Rectangle panelRect;
        private float eased;

        //平滑滚动（像素）
        private float scrollPx;
        private float scrollTarget;

        //悬停 / 选中
        private int hoveredIndex = -1;
        private int selectedIndex = -1;
        private float hoverAnim;
        private int lastHoveredIndex = -1;

        //长按连续购买
        private int holdIndex = -1;
        private int holdTimer;
        private int purchaseCooldown = InitialCooldown;
        private int consecutiveCount;
        private const int HoldThreshold = 18;
        private const int InitialCooldown = 28;
        private const int MinCooldown = 2;

        //滚动条拖拽
        private bool scrollDragging;
        private float dragGrabOffset;
        private float scrollbarGlow;

        /// <summary>当前面板矩形，供呼叫面板贴靠</summary>
        public Rectangle PanelRect => panelRect;
        #endregion

        protected override void OnOpen() {
            scrollPx = scrollTarget = 0f;
            hoveredIndex = selectedIndex = -1;
            ResetHold();
        }

        protected override void OnClose() => ResetHold();

        public override void Update() {
            if (!IsOpen && OpenProgress.Current <= 0.001f) {
                return;
            }

            eased = VaultUtils.EaseOutCubic(MathHelper.Clamp(OpenProgress.Current, 0f, 1f));
            panelRect = new Rectangle(
                (int)(DraedonShopTheme.UIScreenW - DraedonShopTheme.PanelWidth + (1f - eased) * DraedonShopTheme.PanelWidth),
                (int)((DraedonShopTheme.UIScreenH - DraedonShopTheme.PanelHeight) / 2f),
                DraedonShopTheme.PanelWidth, DraedonShopTheme.PanelHeight);

            InitializeShop();
            state.Update(panelRect, IsOpen);

            scrollTarget = MathHelper.Clamp(scrollTarget, 0f, MaxScroll());
            scrollPx = MathHelper.Lerp(scrollPx, scrollTarget, 0.25f);

            bool interactive = IsOpen && eased > 0.85f;
            if (interactive) {
                UpdateInteraction();
            }
            else {
                hoveredIndex = -1;
                scrollDragging = false;
            }

            //换行重置hoverAnim
            if (hoveredIndex != lastHoveredIndex) {
                hoverAnim = 0f;
                lastHoveredIndex = hoveredIndex;
            }
            hoverAnim = MathHelper.Clamp(hoverAnim + (hoveredIndex >= 0 ? 0.2f : -0.3f), 0f, 1f);
            scrollbarGlow = MathHelper.Lerp(scrollbarGlow, scrollDragging ? 1f : 0f, 0.2f);

            UpdateHold();
        }

        private void UpdateInteraction() {
            hoverInMainPage = panelRect.Contains(MousePosition.ToPoint());
            bool pressed = keyLeftPressState == KeyPressState.Pressed;

            if (hoverInMainPage) {
                player.mouseInterface = true;
                UIInputGuard.SuppressWeaponSwitch();
            }
            else if (pressed && !DraedonCallUI.Instance.hoverInMainPage && !player.mouseInterface) {
                //点外部关闭
                Close();
                return;
            }

            HandleScrollbar();
            if (!scrollDragging) {
                HandleWheel();
                HandleRows();
            }
            else {
                hoveredIndex = -1;
            }
        }

        private void HandleWheel() {
            if (!hoverInMainPage || MaxScroll() <= 0f) {
                return;
            }
            int delta = MouseScrollDelta;
            if (delta != 0) {
                scrollTarget = MathHelper.Clamp(scrollTarget - Math.Sign(delta) * DraedonShopTheme.RowHeight * 0.9f, 0f, MaxScroll());
                PlayerInput.LockVanillaMouseScroll("CalamityOverhaul/DraedonShop");
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.22f, Pitch = -0.2f });
            }
        }

        private void HandleScrollbar() {
            float maxScroll = MaxScroll();
            if (maxScroll <= 0f) {
                scrollDragging = false;
                return;
            }

            Rectangle track = ScrollTrack();
            int indH = IndicatorHeight();
            float progress = scrollTarget / maxScroll;
            int indY = track.Y + (int)(progress * (track.Height - indH));
            Rectangle indicator = new(track.X - 4, indY, track.Width + 8, indH);

            Vector2 mouse = MousePosition;
            bool overIndicator = indicator.Contains(mouse.ToPoint());
            bool overTrack = track.Contains(mouse.ToPoint());

            if (!scrollDragging && keyLeftPressState == KeyPressState.Pressed && (overIndicator || overTrack)) {
                scrollDragging = true;
                dragGrabOffset = overIndicator ? mouse.Y - indY : indH / 2f;
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.3f, Pitch = 0.5f });
            }

            if (scrollDragging) {
                if (Main.mouseLeft) {
                    float available = track.Height - indH;
                    float newProgress = available > 0f ? (mouse.Y - track.Y - dragGrabOffset) / available : 0f;
                    scrollTarget = MathHelper.Clamp(newProgress, 0f, 1f) * maxScroll;
                }
                else {
                    scrollDragging = false;
                }
            }
        }

        private void HandleRows() {
            hoveredIndex = -1;
            Rectangle viewport = ListViewport();
            Vector2 mouse = MousePosition;
            if (!viewport.Contains(mouse.ToPoint())) {
                ResetHold();
                return;
            }

            for (int i = 0; i < shopItems.Count; i++) {
                Rectangle row = RowRect(i);
                if (row.Y > viewport.Bottom || row.Bottom < viewport.Top) {
                    continue;
                }
                if (row.Contains(mouse.ToPoint())) {
                    hoveredIndex = i;
                    if (lastHoveredIndex != i) {
                        SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.2f, Pitch = 0.4f });
                    }
                    HandlePurchaseInput(i);
                    return;
                }
            }
            ResetHold();
        }

        private void HandlePurchaseInput(int index) {
            if (!Main.mouseLeft) {
                if (holdIndex == index) {
                    ResetHold();
                }
                return;
            }

            if (Main.mouseLeftRelease) {
                selectedIndex = index;
                holdIndex = index;
                holdTimer = 0;
                consecutiveCount = 0;
                purchaseCooldown = InitialCooldown;
                TryBuy(index);
                return;
            }

            if (holdIndex != index) {
                holdIndex = index;
                holdTimer = 0;
                consecutiveCount = 0;
                purchaseCooldown = InitialCooldown;
                return;
            }

            holdTimer++;
            if (holdTimer >= HoldThreshold && (holdTimer - HoldThreshold) % purchaseCooldown == 0) {
                TryBuy(index);
                consecutiveCount++;
                if (consecutiveCount % 5 == 0) {
                    purchaseCooldown = Math.Max(MinCooldown, (int)(purchaseCooldown * 0.8f));
                }
            }
        }

        private void TryBuy(int index) {
            if (index < 0 || index >= shopItems.Count) {
                return;
            }
            ShopItem si = shopItems[index];
            if (player.BuyItem(si.price)) {
                player.GiveItem(player.GetSource_OpenItem(si.itemType), si.itemType, si.stack);
                SoundEngine.PlaySound(SoundID.Coins);
                SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.6f, Pitch = 0.3f });
            }
            else {
                SoundEngine.PlaySound(SoundID.MenuClose with { Pitch = -0.5f, Volume = 0.8f });
            }
        }

        private void UpdateHold() {
            if (!Main.mouseLeft && holdIndex != -1) {
                ResetHold();
            }
        }

        private void ResetHold() {
            holdIndex = -1;
            holdTimer = 0;
            consecutiveCount = 0;
            purchaseCooldown = InitialCooldown;
        }

        #region 布局
        private Rectangle ListViewport() => new(
            panelRect.X, panelRect.Y + DraedonShopTheme.HeaderHeight,
            DraedonShopTheme.PanelWidth, DraedonShopTheme.PanelHeight - DraedonShopTheme.HeaderHeight - DraedonShopTheme.FooterHeight);

        private Rectangle RowRect(int index) {
            int listTop = panelRect.Y + DraedonShopTheme.HeaderHeight;
            int rowX = panelRect.X + DraedonShopTheme.SidePadding;
            int rowW = DraedonShopTheme.PanelWidth - DraedonShopTheme.SidePadding * 2 - 12;
            int y = (int)(listTop - scrollPx + index * DraedonShopTheme.RowHeight) + 4;
            return new Rectangle(rowX, y, rowW, DraedonShopTheme.RowHeight - 8);
        }

        private Rectangle ScrollTrack() {
            Rectangle viewport = ListViewport();
            return new Rectangle(panelRect.Right - 13, viewport.Top + 4, 4, viewport.Height - 8);
        }

        private int IndicatorHeight() {
            float viewportH = ListViewport().Height;
            float contentH = shopItems.Count * DraedonShopTheme.RowHeight;
            float track = ScrollTrack().Height;
            return (int)Math.Max(28f, contentH > 0f ? track * viewportH / contentH : track);
        }

        private float MaxScroll() {
            float viewportH = ListViewport().Height;
            float contentH = shopItems.Count * DraedonShopTheme.RowHeight;
            return Math.Max(0f, contentH - viewportH);
        }
        #endregion

        public override void Draw(SpriteBatch spriteBatch) {
            if (OpenProgress.Current <= 0.001f) {
                return;
            }
            float alpha = eased;

            DraedonShopRenderer.DrawChrome(spriteBatch, panelRect, alpha, state);

            DrawRowsClipped(spriteBatch, alpha);
            ShowHoveredTooltip();

            long funds = DraedonShopStyle.CountCoins(player);
            DraedonShopRenderer.DrawHeader(spriteBatch, panelRect, alpha, state, TitleText.Value, FundsLabelText.Value, funds);
            DraedonShopRenderer.DrawFooter(spriteBatch, panelRect, alpha, state, HintText.Value, PageText());

            if (MaxScroll() > 0f) {
                DraedonShopRenderer.DrawScrollbar(spriteBatch, ScrollTrack(), alpha, scrollTarget, MaxScroll(),
                    IndicatorHeight(), scrollbarGlow, state);
            }
        }

        private void DrawRowsClipped(SpriteBatch spriteBatch, float alpha) {
            Rectangle viewport = ListViewport();

            if (shopItems.Count == 0) {
                DraedonShopRenderer.DrawEmpty(spriteBatch, viewport, alpha, EmptyText.Value);
                return;
            }

            spriteBatch.End();
            Vector2 clipPos = Vector2.Transform(new Vector2(viewport.X, viewport.Y), Main.UIScaleMatrix);
            Vector2 clipSize = Vector2.Transform(new Vector2(viewport.Width, viewport.Height), Main.UIScaleMatrix)
                - Vector2.Transform(Vector2.Zero, Main.UIScaleMatrix);
            Rectangle scissor = new((int)clipPos.X, (int)clipPos.Y, (int)clipSize.X, (int)clipSize.Y);
            scissor = Rectangle.Intersect(scissor, spriteBatch.GraphicsDevice.Viewport.Bounds);
            Rectangle original = spriteBatch.GraphicsDevice.ScissorRectangle;
            RasterizerState rasterizer = new() { ScissorTestEnable = true };

            spriteBatch.GraphicsDevice.ScissorRectangle = scissor;
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, rasterizer, null, Main.UIScaleMatrix);

            float holdProgress = holdTimer < HoldThreshold ? holdTimer / (float)HoldThreshold : 0f;
            for (int i = 0; i < shopItems.Count; i++) {
                Rectangle row = RowRect(i);
                if (row.Y > viewport.Bottom || row.Bottom < viewport.Top) {
                    continue;
                }
                ShopItem si = shopItems[i];
                float hover = i == hoveredIndex ? hoverAnim : 0f;
                var visual = new DraedonShopRenderer.RecordVisual(
                    i + 1, si.itemType, Lang.GetItemNameValue(si.itemType), si.price,
                    hover, i == selectedIndex, player.CanAfford(si.price),
                    i == holdIndex ? holdProgress : 0f, i == holdIndex ? consecutiveCount : 0);
                DraedonShopRenderer.DrawRecord(spriteBatch, row, alpha, state, visual, BuyActionText.Value);
            }

            spriteBatch.End();
            spriteBatch.GraphicsDevice.ScissorRectangle = original;
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);
        }

        /// <summary>悬停tooltip,同ChargingStationUI</summary>
        private void ShowHoveredTooltip() {
            if (hoveredIndex < 0 || hoveredIndex >= shopItems.Count) {
                return;
            }
            ShopItem si = shopItems[hoveredIndex];
            if (ContentSamples.ItemsByType.TryGetValue(si.itemType, out Item sample)) {
                Item clone = sample.Clone();
                clone.stack = Math.Max(1, si.stack);
                Main.HoverItem = clone;
                Main.hoverItemName = clone.Name;
            }
        }

        private string PageText() {
            if (shopItems.Count == 0) {
                return string.Empty;
            }
            int rowsPerView = Math.Max(1, ListViewport().Height / DraedonShopTheme.RowHeight);
            int first = (int)(scrollPx / DraedonShopTheme.RowHeight) + 1;
            int last = Math.Min(shopItems.Count, first + rowsPerView - 1);
            return $"{first:00}-{last:00} / {shopItems.Count:00}";
        }

        /// <summary>懒加载货品,仅一次</summary>
        public void InitializeShop() {
            if (shopItems.Count > 0) {
                return;
            }
            ShopHandle.Handle(shopItems);
        }
    }
}
