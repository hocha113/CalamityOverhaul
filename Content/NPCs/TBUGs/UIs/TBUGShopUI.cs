using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.TBUGs.UIs
{
    /// <summary>黑客商店终端窗口：标题提示符 + 余额 + 货架列表 + 状态栏</summary>
    internal class TBUGShopUI : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "UI";

        public static TBUGShopUI Instance => UIHandleLoader.GetUIHandleOfType<TBUGShopUI>();

        public override bool CloseOnEscape => true;
        public override SoundStyle? OpenSound => SoundID.MenuOpen;
        public override SoundStyle? CloseSound => SoundID.MenuClose;

        #region 本地化

        public static LocalizedText TitleText { get; private set; }
        public static LocalizedText StockHeaderText { get; private set; }
        public static LocalizedText BalanceText { get; private set; }
        public static LocalizedText StatusIdleText { get; private set; }
        public static LocalizedText ResultSuccessText { get; private set; }
        public static LocalizedText ResultInvalidText { get; private set; }
        public static LocalizedText ResultOutOfRangeText { get; private set; }
        public static LocalizedText ResultNoFundsText { get; private set; }
        public static LocalizedText ResultNoSpaceText { get; private set; }
        public static LocalizedText ResultBusyText { get; private set; }
        public static LocalizedText ResultTimeoutText { get; private set; }

        public override void SetStaticDefaults() {
            TitleText = this.GetLocalization(nameof(TitleText), () => "shop --list");
            StockHeaderText = this.GetLocalization(nameof(StockHeaderText), () => "STOCK");
            BalanceText = this.GetLocalization(nameof(BalanceText), () => "BALANCE");
            StatusIdleText = this.GetLocalization(nameof(StatusIdleText), () => "connected · one click buys · no refunds");
            ResultSuccessText = this.GetLocalization(nameof(ResultSuccessText), () => "exit 0 · transaction complete");
            ResultInvalidText = this.GetLocalization(nameof(ResultInvalidText), () => "ERR: bad request");
            ResultOutOfRangeText = this.GetLocalization(nameof(ResultOutOfRangeText), () => "ERR: too far from vendor");
            ResultNoFundsText = this.GetLocalization(nameof(ResultNoFundsText), () => "ERR: insufficient funds"); 
            ResultNoSpaceText = this.GetLocalization(nameof(ResultNoSpaceText), () => "ERR: inventory full");
            ResultBusyText = this.GetLocalization(nameof(ResultBusyText), () => "ERR: too many requests, slow down");
            ResultTimeoutText = this.GetLocalization(nameof(ResultTimeoutText), () => "ERR: request timed out");
        }

        /// <summary>结果码 → 底栏文案</summary>
        internal static LocalizedText ResultText(TBUGShopResult code) => code switch {
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
        private Rectangle listRect;
        private Rectangle closeBtnRect;
        private bool closeHover;

        private readonly TBUGPanelRenderer panelRenderer = new();
        private readonly TBUGShopPanel shopPanel = new();

        protected override void OnOpen() {
            shopPanel.ResetView();
            panelRenderer.TriggerGlitch(0.3f);
        }

        protected override void OnClose() => TBUGSession.MaybeEndSession();

        private void Layout() {
            int screenW = (int)TBUGTheme.UIScreenW;
            int screenH = (int)TBUGTheme.UIScreenH;
            int panelW = (int)MathHelper.Clamp(screenW * 0.42f, 560, 720);
            int panelH = (int)MathHelper.Clamp(screenH * 0.62f, 420, 540);

            float ease = VaultUtils.EaseOutCubic(MathHelper.Clamp(OpenProgress.Current, 0f, 1f));
            int x = (screenW - panelW) / 2;
            int baseY = (screenH - panelH) / 2;
            int y = baseY + (int)((1f - ease) * 46f);
            panelRect = new Rectangle(x, y, panelW, panelH);

            //标题栏(29) + 余额/分区头(≈54) 之下到状态栏(24)之上是列表
            listRect = new Rectangle(panelRect.X + 16, panelRect.Y + 88,
                panelRect.Width - 32, panelRect.Height - 88 - 30);

            closeBtnRect = TBUGPanelRenderer.GetCloseButtonRect(panelRect);
        }

        public override void Update() {
            if (!IsOpen && OpenProgress.Current <= 0.001f) {
                return;
            }
            Layout();
            panelRenderer.Update();
            if (!IsOpen) {
                return;
            }

            if (panelRect.Contains(MousePoint)) {
                player.mouseInterface = true;
            }

            shopPanel.Update(listRect, MousePoint, Main.LocalPlayer);

            closeHover = closeBtnRect.Contains(MousePoint);
            if (keyLeftPressState == KeyPressState.Pressed) {
                if (closeHover) {
                    Close();
                    return;
                }
                shopPanel.HandleClick(MousePoint, Main.LocalPlayer);
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (!IsOpen && OpenProgress.Current <= 0.001f) {
                return;
            }
            Layout();
            float alpha = MathHelper.Clamp(OpenProgress.Current, 0f, 1f);

            TBUGPanelRenderer.DrawShaderBackground(spriteBatch, alpha * 0.97f, panelRect);
            TBUGPanelRenderer.DrawFrameDecor(spriteBatch, alpha, panelRect, GlobalTimer);
            int divY = panelRenderer.DrawPromptTitle(spriteBatch, alpha, panelRect, GlobalTimer, TitleText.Value);

            //余额行：左分区头，右余额
            Rectangle headerRect = new(panelRect.X + 16, divY + 10, panelRect.Width - 32, 24);
            TBUGUIStyle.DrawSectionHeader(spriteBatch, headerRect, StockHeaderText.Value,
                TBUGTheme.Accent, alpha, 0.5f * TBUGTheme.FontScale);
            long balance = TBUGUIStyle.CountCoins(Main.LocalPlayer);
            float balScale = 0.46f * TBUGTheme.FontScale;
            string balLabel = BalanceText.Value;
            Vector2 balLabelSize = FontAssets.MouseText.Value.MeasureString(balLabel) * balScale;
            //价格从右缘往左排，标签再靠左一点
            TBUGUIStyle.DrawPrice(spriteBatch, new Vector2(headerRect.Right - 4, headerRect.Y + 3),
                balance, alpha, balScale, rightAlign: true);
            Utils.DrawBorderString(spriteBatch, balLabel,
                new Vector2(headerRect.Right - 210 - balLabelSize.X, headerRect.Y + 3),
                TBUGTheme.TextDim * alpha, balScale);

            shopPanel.Draw(spriteBatch, alpha, Main.LocalPlayer);

            //状态栏：反馈优先，其次常驻提示
            string status = shopPanel.HasFeedback ? shopPanel.FeedbackText : StatusIdleText.Value;
            TBUGPanelRenderer.DrawStatusFooter(spriteBatch, alpha, panelRect, GlobalTimer, status);

            panelRenderer.DrawGlitchEffect(spriteBatch, alpha, panelRect);
            panelRenderer.DrawCloseButton(spriteBatch, alpha, panelRect, closeHover);
        }
    }
}
