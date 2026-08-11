using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.UI;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.TBUGs.UIs
{
    /// <summary>
    /// 黑客商店：货架是纵横网格而不是列表；光标停在格上弹出介绍框（名称/描述/价格/可否买得起）。
    /// 单击即买，挂起门防连发
    /// </summary>
    internal class TBUGShopUI : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "UI";

        public static TBUGShopUI Instance => UIHandleLoader.GetUIHandleOfType<TBUGShopUI>();

        public override bool CloseOnEscape => true;
        public override SoundStyle? OpenSound => SoundID.MenuOpen;
        public override SoundStyle? CloseSound => SoundID.MenuClose;

        #region 本地化

        public static LocalizedText PromptText { get; private set; }
        public static LocalizedText TitleText { get; private set; }
        public static LocalizedText BalanceText { get; private set; }
        public static LocalizedText PriceText { get; private set; }
        public static LocalizedText TagAffordText { get; private set; }
        public static LocalizedText TagNoFundsText { get; private set; }
        public static LocalizedText StatusIdleText { get; private set; }
        public static LocalizedText StatusPendingText { get; private set; }
        public static LocalizedText ResultSuccessText { get; private set; }
        public static LocalizedText ResultInvalidText { get; private set; }
        public static LocalizedText ResultOutOfRangeText { get; private set; }
        public static LocalizedText ResultNoFundsText { get; private set; }
        public static LocalizedText ResultNoSpaceText { get; private set; }
        public static LocalizedText ResultBusyText { get; private set; }
        public static LocalizedText ResultTimeoutText { get; private set; }

        public override void SetStaticDefaults() {
            PromptText = this.GetLocalization(nameof(PromptText), () => "tbug@rift:~$");
            TitleText = this.GetLocalization(nameof(TitleText), () => "shop --list");
            BalanceText = this.GetLocalization(nameof(BalanceText), () => "BALANCE");
            PriceText = this.GetLocalization(nameof(PriceText), () => "PRICE");
            TagAffordText = this.GetLocalization(nameof(TagAffordText), () => "IN STOCK");
            TagNoFundsText = this.GetLocalization(nameof(TagNoFundsText), () => "NO FUNDS");
            StatusIdleText = this.GetLocalization(nameof(StatusIdleText), () => "connected · one click buys · no refunds");
            StatusPendingText = this.GetLocalization(nameof(StatusPendingText), () => "awaiting authority...");
            ResultSuccessText = this.GetLocalization(nameof(ResultSuccessText), () => "exit 0 · transaction complete");
            ResultInvalidText = this.GetLocalization(nameof(ResultInvalidText), () => "ERR: bad request");
            ResultOutOfRangeText = this.GetLocalization(nameof(ResultOutOfRangeText), () => "ERR: too far from vendor");
            ResultNoFundsText = this.GetLocalization(nameof(ResultNoFundsText), () => "ERR: insufficient funds");
            ResultNoSpaceText = this.GetLocalization(nameof(ResultNoSpaceText), () => "ERR: inventory full");
            ResultBusyText = this.GetLocalization(nameof(ResultBusyText), () => "ERR: too many requests, slow down");
            ResultTimeoutText = this.GetLocalization(nameof(ResultTimeoutText), () => "ERR: request timed out");
        }

        private static LocalizedText ResultTextOf(TBUGShopResult code) => code switch {
            TBUGShopResult.Success => ResultSuccessText,
            TBUGShopResult.OutOfRange => ResultOutOfRangeText,
            TBUGShopResult.InsufficientFunds => ResultNoFundsText,
            TBUGShopResult.InventoryFull => ResultNoSpaceText,
            TBUGShopResult.Busy => ResultBusyText,
            TBUGShopResult.Timeout => ResultTimeoutText,
            _ => ResultInvalidText,
        };

        #endregion

        private Rectangle panelRect;
        private Rectangle gridRect;
        private Rectangle closeRect;
        private bool closeHover;

        private float scrollOffset;
        private int oldWheel;
        private int hoverIndex = -1;
        private float[] cellHover = [];

        private bool purchasePending;
        private uint purchaseSerial;
        private LocalizedText feedback;
        private bool feedbackError;
        private int feedbackFrames;

        //悬停介绍框的描述缓存，避免每帧重建 tooltip 列表
        private int cachedTipType = -1;
        private List<string> cachedTipLines = [];

        private static int CellStride => TBUGTheme.CellSize + TBUGTheme.CellGap;
        private static int ItemCount => TBUGCatalog.Entries.Count;
        private static int RowCount => (ItemCount + TBUGTheme.GridColumns - 1) / TBUGTheme.GridColumns;

        protected override void OnOpen() {
            scrollOffset = 0f;
            hoverIndex = -1;
            purchasePending = false;
            feedback = null;
            feedbackFrames = 0;
            cachedTipType = -1;
            oldWheel = Mouse.GetState().ScrollWheelValue;
            cellHover = new float[Math.Max(1, ItemCount)];
        }

        protected override void OnClose() => TBUGSession.MaybeEndSession();

        private float MaxScroll() => MathF.Max(0f, RowCount * CellStride - TBUGTheme.CellGap - gridRect.Height);

        //窗高按货物行数长出来；超过这个行数才开始滚动，免得空窗吊着一排货
        private const int MaxVisibleRows = 5;
        private const int HeaderBlock = 38;
        private const int BalanceBlock = 32;
        private const int StatusBlock = 34;
        private const int GridPadY = 6;

        private void Layout() {
            float screenW = TBUGTheme.UIScreenW;
            float screenH = TBUGTheme.UIScreenH;

            //宽度按列数定死，网格才不会在窗里飘
            int gridW = TBUGTheme.GridColumns * CellStride - TBUGTheme.CellGap;
            int panelW = gridW + 56;

            int visibleRows = Math.Clamp(RowCount, 1, MaxVisibleRows);
            int gridH = visibleRows * CellStride - TBUGTheme.CellGap;
            //视口再让屏高兜一次底，小分辨率下不许顶出屏幕
            int maxGridH = (int)screenH - 140 - HeaderBlock - BalanceBlock - StatusBlock;
            gridH = Math.Max(CellStride - TBUGTheme.CellGap, Math.Min(gridH, maxGridH));
            int panelH = HeaderBlock + BalanceBlock + GridPadY * 2 + gridH + StatusBlock;

            float ease = VaultUtils.EaseOutCubic(MathHelper.Clamp(OpenProgress.Current, 0f, 1f));
            int x = (int)(screenW - panelW) / 2;
            int y = (int)(screenH - panelH) / 2 + (int)((1f - ease) * 44f);
            panelRect = new Rectangle(x, y, panelW, panelH);

            int gridTop = panelRect.Y + HeaderBlock + BalanceBlock + GridPadY;
            gridRect = new Rectangle(panelRect.X + 28, gridTop, gridW, gridH);

            closeRect = TBUGRenderer.GetCloseRect(panelRect);
        }

        private Rectangle CellRect(int index) {
            int col = index % TBUGTheme.GridColumns;
            int row = index / TBUGTheme.GridColumns;
            return new Rectangle(
                gridRect.X + col * CellStride,
                gridRect.Y + row * CellStride - (int)scrollOffset,
                TBUGTheme.CellSize, TBUGTheme.CellSize);
        }

        public override void Update() {
            if (!IsOpen && OpenProgress.Current <= 0.001f) {
                return;
            }
            Layout();
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
                if (hoverIndex >= 0 && hoverIndex < ItemCount) {
                    DoBuy(TBUGCatalog.Entries[hoverIndex].ItemType);
                }
            }
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
            feedbackError = code != TBUGShopResult.Success;
            feedback = ResultTextOf(code);
            feedbackFrames = 180;
            if (feedbackError) {
                SoundEngine.PlaySound(SoundID.MenuClose with { Pitch = -0.5f });
            }
            else {
                SoundEngine.PlaySound(SoundID.Coins);
                SoundEngine.PlaySound(SoundID.Grab with { Pitch = 0.1f });
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (!IsOpen && OpenProgress.Current <= 0.001f) {
                return;
            }
            Layout();
            float alpha = MathHelper.Clamp(OpenProgress.Current, 0f, 1f);
            long balance = TBUGRenderer.CountCoins(Main.LocalPlayer);

            TBUGRenderer.DrawDropShadow(spriteBatch, panelRect, alpha);
            TBUGRenderer.DrawGlassPanel(spriteBatch, panelRect, alpha);
            TBUGRenderer.DrawScanSweep(spriteBatch, panelRect, alpha, GlobalTimer);
            TBUGRenderer.DrawChamferFrame(spriteBatch, panelRect,
                TBUGTheme.Blue * (alpha * 0.75f), 1.6f, TBUGTheme.Chamfer, glow: true);

            TBUGRenderer.DrawPromptHeader(spriteBatch, panelRect, alpha, GlobalTimer,
                PromptText.Value, TitleText.Value);
            DrawBalanceRow(spriteBatch, alpha, balance);
            DrawGrid(spriteBatch, alpha, balance);

            string status = purchasePending ? StatusPendingText.Value
                : feedbackFrames > 0 && feedback != null ? feedback.Value
                : StatusIdleText.Value;
            bool error = feedbackFrames > 0 && feedback != null && feedbackError && !purchasePending;
            TBUGRenderer.DrawStatusBar(spriteBatch, panelRect, alpha, GlobalTimer, status, error);

            TBUGRenderer.DrawClose(spriteBatch, panelRect, alpha, closeHover);

            //介绍框最后画，压在一切之上
            DrawHoverTip(spriteBatch, alpha, balance);
        }

        private void DrawBalanceRow(SpriteBatch sb, float alpha, long balance) {
            float y = panelRect.Y + HeaderBlock + 6f;
            TBUGRenderer.DrawText(sb, BalanceText.Value, new Vector2(panelRect.X + 28f, y),
                TBUGTheme.TextDim * alpha, TBUGTheme.FontLabel);
            TBUGRenderer.DrawPrice(sb, new Vector2(panelRect.Right - 28f, y), balance, alpha,
                TBUGTheme.FontLabel, rightAlign: true, TBUGTheme.Amber);
        }

        private static readonly RasterizerState ScissorRaster = new() { ScissorTestEnable = true };

        private void DrawGrid(SpriteBatch sb, float alpha, long balance) {
            //网格底：一块比面板更深的凹陷，让货架读作嵌进去的
            TBUGRenderer.FillChamfer(sb, gridRect, TBUGTheme.Void * (alpha * 0.55f), 5);

            //滚动时半露的格必须裁在视口内，否则会盖到余额行与状态栏
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
                TBUGCatalogEntry entry = TBUGCatalog.Entries[i];
                long price = TBUGCatalog.GetDisplayPrice(entry.ItemType);
                TBUGRenderer.DrawShopCell(sb, cell, entry.ItemType, price,
                    balance >= price, i < cellHover.Length ? cellHover[i] : 0f, alpha);
            }

            if (clip) {
                sb.End();
                sb.GraphicsDevice.ScissorRectangle = prevScissor;
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);
            }

            //溢出指示：上下缘各一道细蓝条，而不是现代滚动条
            float maxScroll = MaxScroll();
            if (maxScroll > 0.5f) {
                float top = scrollOffset / maxScroll;
                if (scrollOffset > 1f) {
                    sb.Draw(TBUGRenderer.Pixel, new Rectangle(gridRect.X + 20, gridRect.Y - 3, gridRect.Width - 40, 1),
                        new Rectangle(0, 0, 1, 1), TBUGTheme.Blue * (alpha * 0.5f));
                }
                if (top < 0.99f) {
                    sb.Draw(TBUGRenderer.Pixel, new Rectangle(gridRect.X + 20, gridRect.Bottom + 2, gridRect.Width - 40, 1),
                        new Rectangle(0, 0, 1, 1), TBUGTheme.Blue * (alpha * 0.5f));
                }
            }
        }

        private void DrawHoverTip(SpriteBatch sb, float alpha, long balance) {
            //关闭淡出期间悬停态是残值，别让介绍框跟着鼠标飘
            if (!IsOpen || hoverIndex < 0 || hoverIndex >= ItemCount || purchasePending) {
                return;
            }
            TBUGCatalogEntry entry = TBUGCatalog.Entries[hoverIndex];
            long price = TBUGCatalog.GetDisplayPrice(entry.ItemType);
            bool affordable = balance >= price;

            if (cachedTipType != entry.ItemType) {
                cachedTipType = entry.ItemType;
                cachedTipLines = BuildTooltip(entry.ItemType);
            }

            Item sample = ContentSamples.ItemsByType.TryGetValue(entry.ItemType, out Item it) ? it : null;
            string name = sample?.Name ?? Lang.GetItemNameValue(entry.ItemType);
            //标题用物品稀有度色，和游戏内 tooltip 同语汇
            Color titleColor = sample != null ? ItemRarity.GetColor(sample.rare) : TBUGTheme.Ice;
            if (titleColor.R + titleColor.G + titleColor.B < 90) {
                titleColor = TBUGTheme.Ice;
            }

            TBUGRenderer.DrawCursorPanel(sb, MousePoint.ToVector2(), alpha,
                name, titleColor, cachedTipLines,
                affordable ? TagAffordText.Value : TagNoFundsText.Value,
                affordable ? TBUGTheme.Blue : TBUGTheme.Danger,
                price, affordable ? TBUGTheme.Amber : TBUGTheme.Danger, PriceText.Value);
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
                lines.AddRange(TBUGRenderer.WrapLines(raw, TBUGTheme.FontBody, 390f, 6));
                if (lines.Count >= 10) {
                    break;
                }
            }
            return lines;
        }
    }
}
