using CalamityOverhaul.Content.Cyberwares.UIs;
using CalamityOverhaul.Content.NPCs.CommonUIs;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.UI;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.TBUGs.UIs
{
    /// <summary>
    /// 黑客商店（义体家族皮肤）：购买/出售双页签。
    /// 购买页读世界库存（黎明补货、每日特惠），单击即买、挂起门防连发；
    /// 出售页列背包可售物品，左键卖一件、右键卖整叠，钱货两清全在本机
    /// </summary>
    internal class TBUGShopUI : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "UI";

        public static TBUGShopUI Instance => UIHandleLoader.GetUIHandleOfType<TBUGShopUI>();

        public override bool CloseOnEscape => true;
        public override SoundStyle? OpenSound => SoundID.MenuOpen;
        public override SoundStyle? CloseSound => SoundID.MenuClose;

        #region 本地化

        public static LocalizedText TitleText { get; private set; }
        public static LocalizedText BuyTabText { get; private set; }
        public static LocalizedText SellTabText { get; private set; }
        public static LocalizedText BalanceText { get; private set; }
        public static LocalizedText PriceText { get; private set; }
        public static LocalizedText SellPriceText { get; private set; }
        public static LocalizedText TagAffordText { get; private set; }
        public static LocalizedText TagNoFundsText { get; private set; }
        public static LocalizedText TagSoldOutText { get; private set; }
        public static LocalizedText StockText { get; private set; }
        public static LocalizedText SpecialTagText { get; private set; }
        public static LocalizedText SpecialTipText { get; private set; }
        public static LocalizedText RestockText { get; private set; }
        public static LocalizedText SellHintText { get; private set; }
        public static LocalizedText SellReceiptText { get; private set; }
        public static LocalizedText SellEmptyText { get; private set; }
        public static LocalizedText StatusPendingText { get; private set; }
        public static LocalizedText ResultSuccessText { get; private set; }
        public static LocalizedText ResultInvalidText { get; private set; }
        public static LocalizedText ResultOutOfRangeText { get; private set; }
        public static LocalizedText ResultNoFundsText { get; private set; }
        public static LocalizedText ResultNoSpaceText { get; private set; }
        public static LocalizedText ResultOutOfStockText { get; private set; }
        public static LocalizedText ResultBusyText { get; private set; }
        public static LocalizedText ResultTimeoutText { get; private set; }

        public override void SetStaticDefaults() {
            TitleText = this.GetLocalization(nameof(TitleText), () => "TBUG BUG MARKET");
            BuyTabText = this.GetLocalization(nameof(BuyTabText), () => "BUY");
            SellTabText = this.GetLocalization(nameof(SellTabText), () => "SELL");
            BalanceText = this.GetLocalization(nameof(BalanceText), () => "BALANCE");
            PriceText = this.GetLocalization(nameof(PriceText), () => "PRICE");
            SellPriceText = this.GetLocalization(nameof(SellPriceText), () => "OFFER");
            TagAffordText = this.GetLocalization(nameof(TagAffordText), () => "IN STOCK");
            TagNoFundsText = this.GetLocalization(nameof(TagNoFundsText), () => "NO FUNDS");
            TagSoldOutText = this.GetLocalization(nameof(TagSoldOutText), () => "SOLD OUT");
            StockText = this.GetLocalization(nameof(StockText), () => "STOCK x{0}");
            SpecialTagText = this.GetLocalization(nameof(SpecialTagText), () => "-25%");
            SpecialTipText = this.GetLocalization(nameof(SpecialTipText), () => "daily special, 25% off");
            RestockText = this.GetLocalization(nameof(RestockText), () => "restock at dawn, {0} left");
            SellHintText = this.GetLocalization(nameof(SellHintText), () => "left click sells one, right click sells the stack");
            SellReceiptText = this.GetLocalization(nameof(SellReceiptText), () => "sold {0} x{1}");
            SellEmptyText = this.GetLocalization(nameof(SellEmptyText), () => "nothing sellable in your bag");
            StatusPendingText = this.GetLocalization(nameof(StatusPendingText), () => "awaiting authority...");
            ResultSuccessText = this.GetLocalization(nameof(ResultSuccessText), () => "exit 0, transaction complete");
            ResultInvalidText = this.GetLocalization(nameof(ResultInvalidText), () => "ERR: bad request");
            ResultOutOfRangeText = this.GetLocalization(nameof(ResultOutOfRangeText), () => "ERR: too far from vendor");
            ResultNoFundsText = this.GetLocalization(nameof(ResultNoFundsText), () => "ERR: insufficient funds");
            ResultNoSpaceText = this.GetLocalization(nameof(ResultNoSpaceText), () => "ERR: inventory full");
            ResultOutOfStockText = this.GetLocalization(nameof(ResultOutOfStockText), () => "ERR: sold out, restock at dawn");
            ResultBusyText = this.GetLocalization(nameof(ResultBusyText), () => "ERR: too many requests, slow down");
            ResultTimeoutText = this.GetLocalization(nameof(ResultTimeoutText), () => "ERR: request timed out");
        }

        private static LocalizedText ResultTextOf(TBUGShopResult code) => code switch {
            TBUGShopResult.Success => ResultSuccessText,
            TBUGShopResult.OutOfRange => ResultOutOfRangeText,
            TBUGShopResult.InsufficientFunds => ResultNoFundsText,
            TBUGShopResult.InventoryFull => ResultNoSpaceText,
            TBUGShopResult.OutOfStock => ResultOutOfStockText,
            TBUGShopResult.Busy => ResultBusyText,
            TBUGShopResult.Timeout => ResultTimeoutText,
            _ => ResultInvalidText,
        };

        #endregion

        private enum ShopTab : byte
        {
            Buy,
            Sell,
        }

        #region 布局常量

        private const int CellSize = 84;
        private const int CellGap = 10;
        private const int GridColumns = 4;
        //窗高按货物行数长出来；超过这个行数才开始滚动，免得空窗吊着一排货
        private const int MaxVisibleRows = 5;
        private const int TitleBlock = 34;
        private const int TabBlock = 32;
        private const int GridPadY = 8;
        private const int FooterBlock = 30;
        private const int PriceStripH = 24;

        private static int CellStride => CellSize + CellGap;

        #endregion

        #region 状态

        private ShopTab tab;
        private Rectangle panelRect;
        private Rectangle buyTabRect;
        private Rectangle sellTabRect;
        private Rectangle gridRect;
        private Rectangle closeRect;
        private bool closeHover;
        private float buyTabHover;
        private float sellTabHover;

        private float scrollOffset;
        private int oldWheel;
        private int hoverIndex = -1;
        private float[] cellHover = [];

        private bool purchasePending;
        private uint purchaseSerial;
        private string feedback;
        private bool feedbackError;
        private int feedbackFrames;

        //出售页可售背包槽位，逐帧重建（50 格遍历，便宜）
        private readonly List<int> sellSlots = [];

        //悬停介绍框的描述缓存，避免每帧重建 tooltip 列表
        private int cachedTipType = -1;
        private List<string> cachedTipLines = [];

        private readonly CyberPanelRenderer panelRenderer = new();

        private int ItemCount => tab == ShopTab.Buy ? TBUGCatalog.Entries.Count : sellSlots.Count;
        private int RowCount => (ItemCount + GridColumns - 1) / GridColumns;

        #endregion

        protected override void OnOpen() {
            tab = ShopTab.Buy;
            scrollOffset = 0f;
            hoverIndex = -1;
            purchasePending = false;
            feedback = null;
            feedbackFrames = 0;
            cachedTipType = -1;
            buyTabHover = sellTabHover = 0f;
            oldWheel = Mouse.GetState().ScrollWheelValue;
            cellHover = new float[Math.Max(1, ItemCount)];
        }

        protected override void OnClose() => TBUGSession.MaybeEndSession();

        private float MaxScroll() => MathF.Max(0f, RowCount * CellStride - CellGap - gridRect.Height);

        private void Layout() {
            float screenW = NPCUIStyle.UIScreenW;
            float screenH = NPCUIStyle.UIScreenH;

            //宽度按列数定死，网格才不会在窗里飘
            int gridW = GridColumns * CellStride - CellGap;
            int panelW = gridW + 56;

            int visibleRows = Math.Clamp(RowCount, 1, MaxVisibleRows);
            int gridH = visibleRows * CellStride - CellGap;
            //视口再让屏高兜一次底，小分辨率下不许顶出屏幕
            int maxGridH = (int)screenH - 140 - TitleBlock - TabBlock - FooterBlock;
            gridH = Math.Max(CellStride - CellGap, Math.Min(gridH, maxGridH));
            int panelH = TitleBlock + TabBlock + GridPadY * 2 + gridH + FooterBlock;

            float ease = VaultUtils.EaseOutCubic(MathHelper.Clamp(OpenProgress.Current, 0f, 1f));
            int x = (int)(screenW - panelW) / 2;
            int y = (int)(screenH - panelH) / 2 + (int)((1f - ease) * 44f);
            panelRect = new Rectangle(x, y, panelW, panelH);

            //页签行：左双页签，右余额
            int tabY = panelRect.Y + TitleBlock;
            const int tabW = 92;
            buyTabRect = new Rectangle(panelRect.X + 16, tabY, tabW, TabBlock - 6);
            sellTabRect = new Rectangle(buyTabRect.Right + 6, tabY, tabW, TabBlock - 6);

            int gridTop = panelRect.Y + TitleBlock + TabBlock + GridPadY;
            gridRect = new Rectangle(panelRect.X + 28, gridTop, gridW, gridH);

            closeRect = CyberPanelRenderer.GetCloseButtonRect(panelRect);
        }

        private Rectangle CellRect(int index) {
            int col = index % GridColumns;
            int row = index / GridColumns;
            return new Rectangle(
                gridRect.X + col * CellStride,
                gridRect.Y + row * CellStride - (int)scrollOffset,
                CellSize, CellSize);
        }

        public override void Update() {
            if (!IsOpen && OpenProgress.Current <= 0.001f) {
                return;
            }
            if (tab == ShopTab.Sell) {
                RebuildSellSlots();
            }
            Layout();
            panelRenderer.Update();
            if (!IsOpen) {
                return;
            }

            //绑定的 TBUG 没了（被杀/消失）就收窗；挂起的回执由超时清账
            if (!TBUGSession.IsBoundNPCAlive()) {
                Close();
                return;
            }

            if (feedbackFrames > 0) {
                feedbackFrames--;
            }
            if (cellHover.Length != Math.Max(1, ItemCount)) {
                cellHover = new float[Math.Max(1, ItemCount)];
            }

            bool overPanel = panelRect.Contains(MousePoint);
            if (overPanel) {
                player.mouseInterface = true;
                //两把锁都要，且都必须每帧常驻（UIHandle.Update 跑在绘制阶段，
                //滚轮增量帧首已被 Player.Update 吃掉，等检测到 delta 再锁就晚一帧）：
                //SuppressWeaponSwitch 是 tick 倒计时，拦 CanSwitchWeapon，管换武器；
                //LockVanillaMouseScroll 是单帧标志，管背包开启时的配方栏滚动
                UIInputGuard.SuppressWeaponSwitch();
                Terraria.GameInput.PlayerInput.LockVanillaMouseScroll("CalamityOverhaul/TBUGShop");
            }

            //滚轮：指针在窗内即接管
            int wheel = Mouse.GetState().ScrollWheelValue;
            int delta = wheel - oldWheel;
            oldWheel = wheel;
            if (delta != 0 && overPanel) {
                scrollOffset = Math.Clamp(scrollOffset - delta * 0.4f, 0f, MaxScroll());
            }
            scrollOffset = Math.Clamp(scrollOffset, 0f, MaxScroll());

            //页签悬停
            UpdateTabHover(buyTabRect, ref buyTabHover);
            UpdateTabHover(sellTabRect, ref sellTabHover);

            //悬停格：命中要同时落在格内与网格视口内，免得滚出视口的半格还能点
            int newHover = -1;
            if (gridRect.Contains(MousePoint)) {
                for (int i = 0; i < ItemCount; i++) {
                    if (CellRect(i).Contains(MousePoint)) {
                        newHover = i;
                        break;
                    }
                }
            }
            if (newHover != hoverIndex && newHover >= 0) {
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.35f });
            }
            hoverIndex = newHover;
            for (int i = 0; i < cellHover.Length; i++) {
                cellHover[i] = MathHelper.Clamp(cellHover[i] + (i == hoverIndex ? 0.22f : -0.22f), 0f, 1f);
            }

            closeHover = closeRect.Contains(MousePoint);
            if (keyLeftPressState == KeyPressState.Pressed) {
                if (closeHover) {
                    Close();
                    return;
                }
                if (buyTabRect.Contains(MousePoint)) {
                    SwitchTab(ShopTab.Buy);
                    return;
                }
                if (sellTabRect.Contains(MousePoint)) {
                    SwitchTab(ShopTab.Sell);
                    return;
                }
                if (hoverIndex >= 0 && hoverIndex < ItemCount) {
                    if (tab == ShopTab.Buy) {
                        TryBuy(TBUGCatalog.Entries[hoverIndex]);
                    }
                    else {
                        DoSell(sellSlots[hoverIndex], wholeStack: false);
                    }
                }
            }

            //出售页右键整叠
            if (keyRightPressState == KeyPressState.Pressed && tab == ShopTab.Sell
                && hoverIndex >= 0 && hoverIndex < sellSlots.Count) {
                DoSell(sellSlots[hoverIndex], wholeStack: true);
            }
        }

        private void UpdateTabHover(Rectangle rect, ref float t) {
            bool now = rect.Contains(MousePoint);
            t = MathHelper.Clamp(t + (now ? 0.2f : -0.2f), 0f, 1f);
        }

        private void SwitchTab(ShopTab target) {
            if (tab == target) {
                return;
            }
            tab = target;
            scrollOffset = 0f;
            hoverIndex = -1;
            cachedTipType = -1;
            if (target == ShopTab.Sell) {
                RebuildSellSlots();
            }
            cellHover = new float[Math.Max(1, ItemCount)];
            SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = 0.2f });
        }

        #region 出售

        private void RebuildSellSlots() {
            sellSlots.Clear();
            Player p = Main.LocalPlayer;
            for (int i = 0; i < 50; i++) {
                Item item = p.inventory[i];
                if (item == null || item.IsAir || item.favorited || item.IsACoin) {
                    continue;
                }
                if (UnitSellPrice(p, item) <= 0L) {
                    continue;
                }
                sellSlots.Add(i);
            }
        }

        /// <summary>单件收购价：原版语义 value/5，最低 1 铜；不可售返回 0</summary>
        private static long UnitSellPrice(Player p, Item item) {
            p.GetItemExpectedPrice(item, out long forSelling, out _);
            return forSelling <= 0L ? 0L : Math.Max(1L, forSelling / 5L);
        }

        /// <summary>
        /// 本机结算出售：SellItem 只把钱塞进背包（原版语义，含卖回补差），
        /// 扣叠数归这里；背包与钱包都归客户端所有，逐帧差分自动同步，不需要发包
        /// </summary>
        private void DoSell(int slot, bool wholeStack) {
            Player p = Main.LocalPlayer;
            if (slot < 0 || slot >= 50) {
                return;
            }
            Item item = p.inventory[slot];
            if (item == null || item.IsAir) {
                return;
            }
            int count = wholeStack ? item.stack : 1;
            string name = item.Name;
            if (!p.SellItem(item, count)) {
                //可售项已过滤，走到这里只会是钱币没处安放
                SoundEngine.PlaySound(SoundID.MenuClose with { Pitch = -0.5f });
                SetFeedback(ResultNoSpaceText.Value, error: true);
                return;
            }
            item.stack -= count;
            if (item.stack <= 0) {
                item.TurnToAir();
            }
            SoundEngine.PlaySound(SoundID.Coins);
            SetFeedback(SellReceiptText.Format(name, count), error: false);
            panelRenderer.TriggerGlitch(0.3f);
        }

        #endregion

        #region 购买

        private void TryBuy(TBUGCatalogEntry entry) {
            if (TBUGStock.GetStock(entry.ItemType) <= 0) {
                //缺货点击拒答：本地直接回话，不发包去打扰权威端
                SoundEngine.PlaySound(SoundID.MenuClose with { Pitch = -0.5f });
                SetFeedback(ResultOutOfStockText.Value, error: true);
                return;
            }
            DoBuy(entry.ItemType);
        }

        private void DoBuy(int itemType) {
            if (purchasePending) {
                SoundEngine.PlaySound(SoundID.MenuClose with { Pitch = -0.5f });
                return;
            }
            purchasePending = true;
            uint serial = ++purchaseSerial;
            bool sent = TBUGShopNet.SendPurchaseRequest(Main.LocalPlayer,
                TBUGSession.BoundWhoAmI, itemType, (code, _) => HandleResult(serial, code));
            if (!sent) {
                purchasePending = false;
                SoundEngine.PlaySound(SoundID.MenuClose with { Pitch = -0.5f });
            }
        }

        private void HandleResult(uint serial, TBUGShopResult code) {
            if (serial != purchaseSerial) {
                return;
            }
            purchasePending = false;
            bool error = code != TBUGShopResult.Success;
            SetFeedback(ResultTextOf(code).Value, error);
            if (error) {
                SoundEngine.PlaySound(SoundID.MenuClose with { Pitch = -0.5f });
            }
            else {
                SoundEngine.PlaySound(SoundID.Coins);
                SoundEngine.PlaySound(SoundID.Grab with { Pitch = 0.1f });
                panelRenderer.TriggerGlitch(0.45f);
            }
        }

        #endregion

        private void SetFeedback(string text, bool error) {
            feedback = text;
            feedbackError = error;
            feedbackFrames = 180;
        }

        /// <summary>距下一次黎明补货的现实时间，mm:ss</summary>
        private static string RestockCountdown() {
            double ticks = Main.dayTime
                ? Main.dayLength - Main.time + Main.nightLength
                : Main.nightLength - Main.time;
            int seconds = Math.Max(0, (int)(ticks / 60.0));
            return $"{seconds / 60}:{seconds % 60:00}";
        }

        private (string text, Color? color) ComposeStatus() {
            if (purchasePending) {
                return (StatusPendingText.Value, null);
            }
            if (feedbackFrames > 0 && feedback != null) {
                return (feedback, feedbackError ? CyberwareTheme.Accent : CyberwareTheme.AccentCyan);
            }
            if (tab == ShopTab.Sell) {
                return (SellHintText.Value, null);
            }
            return (RestockText.Format(RestockCountdown()), null);
        }

        #region 绘制

        public override void Draw(SpriteBatch spriteBatch) {
            if (!IsOpen && OpenProgress.Current <= 0.001f) {
                return;
            }
            Layout();
            float alpha = MathHelper.Clamp(OpenProgress.Current, 0f, 1f);
            long balance = NPCUIStyle.CountCoins(Main.LocalPlayer);

            CyberPanelRenderer.DrawShaderBackground(spriteBatch, alpha * 0.97f, panelRect, Vector2.Zero, 0f, mode: 0);
            CyberPanelRenderer.DrawFrameDecor(spriteBatch, alpha, panelRect, GlobalTimer);

            (string status, Color? statusColor) = ComposeStatus();
            panelRenderer.DrawTitleAndDecor(spriteBatch, alpha, panelRect, panelRect.Center.ToVector2(),
                GlobalTimer, TitleText.Value, status, statusColor);

            DrawTab(spriteBatch, buyTabRect, BuyTabText.Value, tab == ShopTab.Buy, buyTabHover, alpha);
            DrawTab(spriteBatch, sellTabRect, SellTabText.Value, tab == ShopTab.Sell, sellTabHover, alpha);
            DrawBalance(spriteBatch, alpha, balance);

            DrawGrid(spriteBatch, alpha, balance);

            panelRenderer.DrawGlitchEffect(spriteBatch, alpha, panelRect);
            panelRenderer.DrawCloseButton(spriteBatch, alpha, panelRect, closeHover);

            //介绍框最后画，压在一切之上
            DrawHoverTip(spriteBatch, alpha, balance);
        }

        private static void DrawTab(SpriteBatch sb, Rectangle r, string label, bool active, float hoverT, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            sb.Draw(px, r, (active ? CyberwareTheme.SlotInnerBg : CyberwareTheme.SectionBg)
                * (alpha * (active ? 0.95f : 0.55f + 0.25f * hoverT)));
            sb.Draw(px, new Rectangle(r.X, r.Y, r.Width, 1), CyberwareTheme.Border * (alpha * 0.6f));
            //底缘亮条：活动页签常亮，非活动随悬停
            float barT = active ? 1f : hoverT * 0.6f;
            sb.Draw(px, new Rectangle(r.X, r.Bottom - 2, r.Width, 2),
                CyberwareTheme.Accent * (alpha * (0.15f + 0.85f * barT)));

            float scale = 0.52f * CyberwareTheme.FontScale;
            Vector2 size = FontAssets.MouseText.Value.MeasureString(label) * scale;
            Color tc = active ? CyberwareTheme.TextBright
                : Color.Lerp(CyberwareTheme.TextDim, CyberwareTheme.TextNormal, hoverT);
            Utils.DrawBorderString(sb, label, new Vector2(r.Center.X - size.X / 2f, r.Center.Y - size.Y / 2f + 1f),
                tc * alpha, scale);
        }

        private void DrawBalance(SpriteBatch sb, float alpha, long balance) {
            float priceScale = 0.5f * CyberwareTheme.FontScale;
            float labelScale = 0.46f * CyberwareTheme.FontScale;
            float y = buyTabRect.Y + 5f;
            float priceW = NPCUIStyle.MeasurePrice(balance, priceScale);
            NPCUIStyle.DrawPrice(sb, new Vector2(panelRect.Right - 28f, y), balance, alpha,
                priceScale, rightAlign: true, numberColor: CyberwareTheme.AccentGold);
            string label = BalanceText.Value;
            Vector2 labelSize = FontAssets.MouseText.Value.MeasureString(label) * labelScale;
            Utils.DrawBorderString(sb, label,
                new Vector2(panelRect.Right - 28f - priceW - 10f - labelSize.X, y + 2f),
                CyberwareTheme.TextDim * alpha, labelScale);
        }

        private static readonly RasterizerState ScissorRaster = new() { ScissorTestEnable = true };

        private void DrawGrid(SpriteBatch sb, float alpha, long balance) {
            Texture2D px = VaultAsset.placeholder2.Value;
            //网格底：一块比面板更深的凹陷，让货架读作嵌进去的
            sb.Draw(px, gridRect, CyberwareTheme.InnerShadow * (alpha * 0.6f));

            if (tab == ShopTab.Sell && sellSlots.Count == 0) {
                string empty = SellEmptyText.Value;
                float es = 0.5f * CyberwareTheme.FontScale;
                Vector2 size = FontAssets.MouseText.Value.MeasureString(empty) * es;
                Utils.DrawBorderString(sb, empty,
                    new Vector2(gridRect.Center.X - size.X / 2f, gridRect.Center.Y - size.Y / 2f),
                    CyberwareTheme.TextDim * alpha, es);
                return;
            }

            //滚动时半露的格必须裁在视口内，否则会盖到页签行与状态栏
            bool clip = MaxScroll() > 0.5f;
            Rectangle prevScissor = sb.GraphicsDevice.ScissorRectangle;
            if (clip) {
                sb.End();
                Rectangle safe = Rectangle.Intersect(gridRect, sb.GraphicsDevice.Viewport.Bounds);
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, ScissorRaster, null, Main.UIScaleMatrix);
                sb.GraphicsDevice.ScissorRectangle = VaultUtils.GetClippingRectangle(sb, safe);
            }

            for (int i = 0; i < ItemCount; i++) {
                Rectangle cell = CellRect(i);
                if (cell.Bottom < gridRect.Y || cell.Y > gridRect.Bottom) {
                    continue;
                }
                float hoverT = i < cellHover.Length ? cellHover[i] : 0f;
                if (tab == ShopTab.Buy) {
                    DrawBuyCell(sb, cell, TBUGCatalog.Entries[i], balance, hoverT, alpha);
                }
                else {
                    DrawSellCell(sb, cell, sellSlots[i], hoverT, alpha);
                }
            }

            if (clip) {
                sb.End();
                sb.GraphicsDevice.ScissorRectangle = prevScissor;
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);
            }

            //溢出指示：上下缘各一道细红条，而不是现代滚动条
            float maxScroll = MaxScroll();
            if (maxScroll > 0.5f) {
                float top = scrollOffset / maxScroll;
                if (scrollOffset > 1f) {
                    sb.Draw(px, new Rectangle(gridRect.X + 20, gridRect.Y - 3, gridRect.Width - 40, 1),
                        CyberwareTheme.Accent * (alpha * 0.45f));
                }
                if (top < 0.99f) {
                    sb.Draw(px, new Rectangle(gridRect.X + 20, gridRect.Bottom + 2, gridRect.Width - 40, 1),
                        CyberwareTheme.Accent * (alpha * 0.45f));
                }
            }
        }

        /// <summary>格子底与边框；enabled=false（缺货）压暗且不给强调色</summary>
        private static void DrawCellFrame(SpriteBatch sb, Rectangle r, float hoverT, float alpha, bool enabled) {
            Texture2D px = VaultAsset.placeholder2.Value;
            sb.Draw(px, r, CyberwareTheme.SlotInnerBg * (alpha * (enabled ? 0.72f + 0.2f * hoverT : 0.4f)));
            Color edge = Color.Lerp(CyberwareTheme.SlotBorder,
                enabled ? CyberwareTheme.Accent : CyberwareTheme.Border, hoverT * 0.9f);
            sb.Draw(px, new Rectangle(r.X, r.Y, r.Width, 1), edge * alpha);
            sb.Draw(px, new Rectangle(r.X, r.Bottom - 1, r.Width, 1), edge * (alpha * 0.7f));
            sb.Draw(px, new Rectangle(r.X, r.Y, 1, r.Height), edge * (alpha * 0.8f));
            sb.Draw(px, new Rectangle(r.Right - 1, r.Y, 1, r.Height), edge * (alpha * 0.8f));
            //悬停右上角标，明确"当前选中的是这一格"
            if (hoverT > 0.05f) {
                Color tick = CyberwareTheme.EdgeGlow * (alpha * hoverT);
                sb.Draw(px, new Rectangle(r.Right - 14, r.Y + 1, 13, 2), tick);
                sb.Draw(px, new Rectangle(r.Right - 3, r.Y + 1, 2, 13), tick);
            }
        }

        /// <summary>价条：格底一条独立暗带，把图标区和价格分开</summary>
        private static Rectangle DrawPriceStrip(SpriteBatch sb, Rectangle cell, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            Rectangle strip = new(cell.X + 2, cell.Bottom - PriceStripH - 2, cell.Width - 4, PriceStripH);
            sb.Draw(px, strip, CyberwareTheme.SectionBg * (alpha * 0.85f));
            sb.Draw(px, new Rectangle(strip.X, strip.Y, strip.Width, 1), CyberwareTheme.Border * (alpha * 0.8f));
            return strip;
        }

        private static void DrawStripPrice(SpriteBatch sb, Rectangle strip, long price, Color color, float alpha) {
            //铂金档四组币会超出格宽，超了就降一档字号
            float priceScale = 0.42f * CyberwareTheme.FontScale;
            float priceW = NPCUIStyle.MeasurePrice(price, priceScale);
            if (priceW > strip.Width - 8f) {
                priceScale *= 0.8f;
                priceW = NPCUIStyle.MeasurePrice(price, priceScale);
            }
            NPCUIStyle.DrawPrice(sb, new Vector2(strip.Center.X - priceW * 0.5f, strip.Y + 4f), price,
                alpha, priceScale, rightAlign: false, numberColor: color);
        }

        private void DrawBuyCell(SpriteBatch sb, Rectangle cell, TBUGCatalogEntry entry,
            long balance, float hoverT, float alpha) {
            int stockLeft = TBUGStock.GetStock(entry.ItemType);
            bool soldOut = stockLeft <= 0;
            long price = TBUGCatalog.GetDisplayPrice(entry.ItemType);
            bool affordable = balance >= price;

            DrawCellFrame(sb, cell, hoverT, alpha, !soldOut);
            Rectangle strip = DrawPriceStrip(sb, cell, alpha);

            float iconAlpha = alpha * (soldOut ? 0.3f : 1f);
            Vector2 iconCenter = new(cell.Center.X, cell.Y + (cell.Height - PriceStripH) * 0.5f + 2f);
            NPCUIStyle.DrawItemIcon(sb, entry.ItemType, iconCenter, 40f, iconAlpha);

            float microScale = 0.4f * CyberwareTheme.FontScale;
            //库存角标：左上；余 1 转金提醒手慢无
            string stockTag = "×" + stockLeft;
            Color stockColor = soldOut ? CyberwareTheme.Accent
                : stockLeft <= 1 ? CyberwareTheme.AccentGold : CyberwareTheme.TextNormal;
            Utils.DrawBorderString(sb, stockTag, new Vector2(cell.X + 5, cell.Y + 3), stockColor * alpha, microScale);

            //特惠角标：右上
            if (!soldOut && TBUGStock.IsSpecial(entry.ItemType)) {
                string special = SpecialTagText.Value;
                float sw = FontAssets.MouseText.Value.MeasureString(special).X * microScale;
                Utils.DrawBorderString(sb, special, new Vector2(cell.Right - 5 - sw, cell.Y + 3),
                    CyberwareTheme.AccentCyan * alpha, microScale);
            }

            //缺货压标：盖在图标区中央
            if (soldOut) {
                string tagText = TagSoldOutText.Value;
                float ts = 0.44f * CyberwareTheme.FontScale;
                Vector2 tagSize = FontAssets.MouseText.Value.MeasureString(tagText) * ts;
                Utils.DrawBorderString(sb, tagText,
                    new Vector2(cell.Center.X - tagSize.X / 2f, iconCenter.Y - tagSize.Y / 2f),
                    CyberwareTheme.Accent * (alpha * 0.95f), ts);
            }

            Color priceColor = soldOut ? CyberwareTheme.TextDim
                : affordable ? CyberwareTheme.AccentGold : CyberwareTheme.Accent;
            DrawStripPrice(sb, strip, price, priceColor, alpha * (soldOut ? 0.5f : 1f));
        }

        private void DrawSellCell(SpriteBatch sb, Rectangle cell, int slot, float hoverT, float alpha) {
            Player p = Main.LocalPlayer;
            if (slot < 0 || slot >= 50) {
                return;
            }
            Item item = p.inventory[slot];
            if (item == null || item.IsAir) {
                return;
            }

            DrawCellFrame(sb, cell, hoverT, alpha, true);
            Rectangle strip = DrawPriceStrip(sb, cell, alpha);

            Vector2 iconCenter = new(cell.Center.X, cell.Y + (cell.Height - PriceStripH) * 0.5f + 2f);
            NPCUIStyle.DrawItemIcon(sb, item.type, iconCenter, 40f, alpha);

            //叠数：图标区右下
            if (item.stack > 1) {
                string stackText = item.stack.ToString();
                float ss = 0.42f * CyberwareTheme.FontScale;
                Vector2 ssize = FontAssets.MouseText.Value.MeasureString(stackText) * ss;
                Utils.DrawBorderString(sb, stackText,
                    new Vector2(cell.Right - 6 - ssize.X, strip.Y - ssize.Y - 2),
                    CyberwareTheme.TextBright * alpha, ss);
            }

            DrawStripPrice(sb, strip, UnitSellPrice(p, item), CyberwareTheme.AccentGold, alpha);
        }

        private void DrawHoverTip(SpriteBatch sb, float alpha, long balance) {
            //关闭淡出期间悬停态是残值，别让介绍框跟着鼠标飘
            if (!IsOpen || hoverIndex < 0 || hoverIndex >= ItemCount || purchasePending) {
                return;
            }
            if (tab == ShopTab.Buy) {
                DrawBuyTip(sb, alpha, balance);
            }
            else {
                DrawSellTip(sb, alpha);
            }
        }

        private void DrawBuyTip(SpriteBatch sb, float alpha, long balance) {
            TBUGCatalogEntry entry = TBUGCatalog.Entries[hoverIndex];
            int stockLeft = TBUGStock.GetStock(entry.ItemType);
            bool soldOut = stockLeft <= 0;
            long price = TBUGCatalog.GetDisplayPrice(entry.ItemType);
            bool affordable = balance >= price;

            if (cachedTipType != entry.ItemType) {
                cachedTipType = entry.ItemType;
                cachedTipLines = BuildTooltip(entry.ItemType);
            }

            Item sample = ContentSamples.ItemsByType.TryGetValue(entry.ItemType, out Item it) ? it : null;
            string name = sample?.Name ?? Lang.GetItemNameValue(entry.ItemType);
            //标题用物品稀有度色，和游戏内 tooltip 同语汇
            Color titleColor = sample != null ? ItemRarity.GetColor(sample.rare) : CyberwareTheme.TextBright;
            if (titleColor.R + titleColor.G + titleColor.B < 90) {
                titleColor = CyberwareTheme.TextBright;
            }

            string tagText = soldOut ? TagSoldOutText.Value
                : affordable ? TagAffordText.Value : TagNoFundsText.Value;
            Color tagColor = soldOut ? CyberwareTheme.Accent
                : affordable ? CyberwareTheme.AccentCyan : CyberwareTheme.Accent;

            bool special = TBUGStock.IsSpecial(entry.ItemType);
            string footer = StockText.Format(stockLeft);
            if (special) {
                footer = SpecialTipText.Value + " · " + footer;
            }

            NPCUIStyle.DrawCursorPanel(sb, MousePoint.ToVector2(), alpha,
                name, titleColor, cachedTipLines, tagText, tagColor,
                price, soldOut ? CyberwareTheme.TextDim : affordable ? CyberwareTheme.AccentGold : CyberwareTheme.Accent,
                PriceText.Value,
                footer, special ? CyberwareTheme.AccentCyan : default);
        }

        private void DrawSellTip(SpriteBatch sb, float alpha) {
            int slot = sellSlots[hoverIndex];
            Player p = Main.LocalPlayer;
            if (slot < 0 || slot >= 50) {
                return;
            }
            Item item = p.inventory[slot];
            if (item == null || item.IsAir) {
                return;
            }

            Color titleColor = ItemRarity.GetColor(item.rare);
            if (titleColor.R + titleColor.G + titleColor.B < 90) {
                titleColor = CyberwareTheme.TextBright;
            }

            NPCUIStyle.DrawCursorPanel(sb, MousePoint.ToVector2(), alpha,
                item.Name, titleColor,
                NPCUIStyle.WrapLines(SellHintText.Value, NPCUIStyle.TipBodyScale, 320f, 3),
                "×" + item.stack, CyberwareTheme.TextNormal,
                UnitSellPrice(p, item), CyberwareTheme.AccentGold, SellPriceText.Value);
        }

        /// <summary>取物品自带 tooltip 行作介绍正文，按框宽二次换行</summary>
        private static List<string> BuildTooltip(int itemType) {
            List<string> lines = [];
            if (!ContentSamples.ItemsByType.TryGetValue(itemType, out Item item) || item.ToolTip == null) {
                return lines;
            }
            int n = item.ToolTip.Lines;
            for (int i = 0; i < n; i++) {
                string raw = item.ToolTip.GetLine(i);
                if (string.IsNullOrWhiteSpace(raw)) {
                    continue;
                }
                lines.AddRange(NPCUIStyle.WrapLines(raw, NPCUIStyle.TipBodyScale, 390f, 6));
                if (lines.Count >= 10) {
                    break;
                }
            }
            return lines;
        }

        #endregion
    }
}
