using CalamityOverhaul.Content.Cyberwares.UIs;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Victors.UIs
{
    /// <summary>诊所，人体槽位 + 侧栏查看/更换/购买</summary>
    internal class VictorClinicUI : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "UI";

        public static VictorClinicUI Instance => UIHandleLoader.GetUIHandleOfType<VictorClinicUI>();

        public override bool CloseOnEscape => true;
        public override SoundStyle? OpenSound => SoundID.MenuOpen;
        public override SoundStyle? CloseSound => silentClose ? null : SoundID.MenuClose;

        //过场切入时静默关
        private bool silentClose;

        /// <summary>静默关，过场不播 CloseSound</summary>
        public void CloseSilent() {
            silentClose = true;
            Close();
            silentClose = false;
        }

        #region 本地化

        public static LocalizedText TitleText { get; private set; }
        public static LocalizedText StatusText { get; private set; }
        public static LocalizedText GuideText { get; private set; }
        public static LocalizedText PriceFactorText { get; private set; }
        public static LocalizedText SlotFallbackText { get; private set; }
        private static LocalizedText slotSelectedText;
        private static LocalizedText slotEmptyText;
        private static LocalizedText[] slotLabels;
        private readonly string[] slotLabelCache = new string[CyberwarePlayer.SlotCount];

        public override void SetStaticDefaults() {
            TitleText = this.GetLocalization(nameof(TitleText), () => "VICTOR'S CLINIC");
            StatusText = this.GetLocalization(nameof(StatusText), () => "RIPPERDOC ONLINE - SELECT A LIMB");
            GuideText = this.GetLocalization(nameof(GuideText), () => "Select a body part to view, swap and buy its cyberware.");
            PriceFactorText = this.GetLocalization(nameof(PriceFactorText), () => "MOOD PRICE x{0}");
            SlotFallbackText = this.GetLocalization(nameof(SlotFallbackText), () => "CYBERWARE");

            //槽位文案复用 CyberwareUI 翻译
            slotSelectedText = Language.GetText("Mods.CalamityOverhaul.UI.CyberwareUI.SlotSelectedText");
            slotEmptyText = Language.GetText("Mods.CalamityOverhaul.UI.CyberwareUI.SlotEmptyText");
            slotLabels = [
                Language.GetText("Mods.CalamityOverhaul.UI.CyberwareUI.Slot_FrontalCortex"),
                Language.GetText("Mods.CalamityOverhaul.UI.CyberwareUI.Slot_OcularSystem"),
                Language.GetText("Mods.CalamityOverhaul.UI.CyberwareUI.Slot_LeftArm"),
                Language.GetText("Mods.CalamityOverhaul.UI.CyberwareUI.Slot_Hands"),
                Language.GetText("Mods.CalamityOverhaul.UI.CyberwareUI.Slot_LeftLeg"),
                Language.GetText("Mods.CalamityOverhaul.UI.CyberwareUI.Slot_Feet"),
                Language.GetText("Mods.CalamityOverhaul.UI.CyberwareUI.Slot_OperatingSystem"),
                Language.GetText("Mods.CalamityOverhaul.UI.CyberwareUI.Slot_NervousSystem"),
                Language.GetText("Mods.CalamityOverhaul.UI.CyberwareUI.Slot_RightArm"),
                Language.GetText("Mods.CalamityOverhaul.UI.CyberwareUI.Slot_CirculatorySystem"),
                Language.GetText("Mods.CalamityOverhaul.UI.CyberwareUI.Slot_Skeleton"),
                Language.GetText("Mods.CalamityOverhaul.UI.CyberwareUI.Slot_RightLeg"),
            ];
        }

        public string GetSlotLabel(int slotIndex) {
            if (slotIndex >= 0 && slotIndex < slotLabelCache.Length && slotLabelCache[slotIndex] != null) {
                return slotLabelCache[slotIndex];
            }
            //淡出阶段 boundSlot 已解绑时的兜底标题
            return SlotFallbackText?.Value ?? "CYBERWARE";
        }

        #endregion

        #region 渲染组件 / 状态

        private readonly CyberBodyRenderer bodyRenderer = new();
        private readonly CyberPanelRenderer panelRenderer = new();
        private readonly CyberSlotRenderer slotRenderer = new();
        private readonly CyberDataParticleSystem particleSystem = new();
        private readonly VictorClinicPanel clinicPanel = new();

        private float dataStreamPhase;
        private Rectangle panelRect;
        private Vector2 panelCenter;
        private Vector2 bodyOrigin;
        private float currentAlpha;
        private float currentContentAlpha;
        private bool closeButtonHovered;

        #endregion

        protected override void OnClose() {
            //关时清 SelectedSlot 防残留
            slotRenderer.SelectedSlot = -1;
            clinicPanel.Unbind();
        }

        /// <summary>打开并选中 slot，手术后连续操作</summary>
        public void OpenAtSlot(int slot) {
            Open();
            if (slot >= 0 && slot < CyberwarePlayer.SlotCount) {
                slotRenderer.SelectedSlot = slot;
            }
        }

        public override void Update() {
            if (!IsOpen && OpenProgress.Current <= 0.001f) {
                return;
            }

            dataStreamPhase += 0.03f;
            if (dataStreamPhase > MathHelper.TwoPi) {
                dataStreamPhase -= MathHelper.TwoPi;
            }

            float eased = VaultUtils.EaseOutCubic(MathHelper.Clamp(OpenProgress.Current, 0f, 1f));
            currentAlpha = eased;
            currentContentAlpha = eased;

            float panelW = CyberwareTheme.PanelWidth * eased;
            float panelH = CyberwareTheme.PanelHeight * eased;
            panelCenter = new Vector2(Main.screenWidth / 2f, Main.screenHeight / 2f);
            panelRect = new Rectangle((int)(panelCenter.X - panelW / 2f), (int)(panelCenter.Y - panelH / 2f), (int)panelW, (int)panelH);
            bodyOrigin = panelCenter + new Vector2(0, 5);

            RefreshSlotLabelCache();

            bodyRenderer.Update();
            panelRenderer.Update();

            //仅 IsOpen 时响应槽位点击
            if (IsOpen && slotRenderer.UpdateInteraction(panelRect)) {
                panelRenderer.TriggerGlitch(0.3f);
            }
            slotRenderer.UpdateAnimations();
            bodyRenderer.SetFocusNode(slotRenderer.FocusedNodeIndex, slotRenderer.FocusStrength);
            particleSystem.Update(bodyOrigin, OpenProgress.Current);

            CyberwarePlayer cyberPlayer = player.GetModPlayer<CyberwarePlayer>();
            clinicPanel.Update(panelRect, IsOpen ? slotRenderer.SelectedSlot : -1, cyberPlayer);
            if (clinicPanel.ActionThisFrame) {
                panelRenderer.TriggerGlitch(0.5f);
            }

            if (!IsOpen) {
                return;//淡出阶段跳过关闭/输入
            }

            Rectangle closeBtn = CyberPanelRenderer.GetCloseButtonRect(panelRect);
            closeButtonHovered = closeBtn.Contains(Main.mouseX, Main.mouseY);
            if (closeButtonHovered) {
                player.mouseInterface = true;
                if (Main.mouseLeft && Main.mouseLeftRelease) {
                    Close();
                    return;
                }
            }

            if (panelRect.Contains(Main.mouseX, Main.mouseY)) {
                player.mouseInterface = true;
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (!IsOpen && OpenProgress.Current <= 0.001f) {
                return;
            }
            RefreshSlotLabelCache();

            //CyberwarePanel 底层 + 人体光场
            Vector2 bodyLocalCenter = bodyOrigin - new Vector2(panelRect.X, panelRect.Y);
            float bodyR = CyberwareTheme.BodyHaloRadius * MathHelper.Clamp(currentContentAlpha, 0f, 1f);
            CyberPanelRenderer.DrawShaderBackground(spriteBatch, currentAlpha, panelRect, bodyLocalCenter, bodyR, mode: 0);
            CyberPanelRenderer.DrawFrameDecor(spriteBatch, currentAlpha, panelRect, GlobalTimer);

            //Scissor 裁面板内
            RasterizerState rasterizer = new() { ScissorTestEnable = true };
            spriteBatch.End();

            const int margin = 4;
            Vector2 clipPos = Vector2.Transform(new Vector2(panelRect.X + margin, panelRect.Y + margin), Main.UIScaleMatrix);
            Vector2 clipSize = Vector2.Transform(new Vector2(panelRect.Width - margin * 2, panelRect.Height - margin * 2), Main.UIScaleMatrix)
                - Vector2.Transform(Vector2.Zero, Main.UIScaleMatrix);
            Rectangle scissor = new((int)clipPos.X, (int)clipPos.Y, (int)clipSize.X, (int)clipSize.Y);
            Rectangle originalScissor = spriteBatch.GraphicsDevice.ScissorRectangle;
            scissor = Rectangle.Intersect(scissor, spriteBatch.GraphicsDevice.Viewport.Bounds);

            spriteBatch.GraphicsDevice.ScissorRectangle = scissor;
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, rasterizer, null, Main.UIScaleMatrix);

            if (currentContentAlpha > 0.01f) {
                CyberwarePlayer cyberPlayer = player.GetModPlayer<CyberwarePlayer>();
                bodyRenderer.DrawBody(spriteBatch, currentContentAlpha, bodyOrigin, GlobalTimer);
                bodyRenderer.DrawNodes(spriteBatch, currentContentAlpha, bodyOrigin, slotRenderer.ComputeNodeStates());
                slotRenderer.DrawConnectors(spriteBatch, currentContentAlpha, panelRect, bodyRenderer, bodyOrigin, dataStreamPhase);
                slotRenderer.DrawSlots(spriteBatch, currentContentAlpha, panelRect, slotLabelCache,
                    slotSelectedText.Value, slotEmptyText.Value, cyberPlayer);
                panelRenderer.DrawTitleAndDecor(spriteBatch, currentContentAlpha, panelRect, panelCenter,
                    GlobalTimer, TitleText.Value, StatusText.Value);
            }

            particleSystem.Draw(spriteBatch, currentAlpha);

            spriteBatch.End();
            spriteBatch.GraphicsDevice.ScissorRectangle = originalScissor;
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);

            panelRenderer.DrawGlitchEffect(spriteBatch, currentAlpha, panelRect);

            if (currentContentAlpha > 0.01f) {
                panelRenderer.DrawCloseButton(spriteBatch, currentContentAlpha, panelRect, closeButtonHovered);
                CyberwarePlayer cyberPlayer = player.GetModPlayer<CyberwarePlayer>();
                clinicPanel.Draw(spriteBatch, currentContentAlpha, cyberPlayer);

                //未选槽位绘引导卡
                if (IsOpen && slotRenderer.SelectedSlot < 0) {
                    DrawSelectionGuide(spriteBatch, currentContentAlpha);
                }
            }
        }

        /// <summary>未选槽位时右侧全息引导卡 + 脉冲箭头</summary>
        private void DrawSelectionGuide(SpriteBatch sb, float alpha) {
            float pulse = 0.6f + 0.4f * MathF.Sin(GlobalTimer * 3f);
            const int w = 304;
            const int h = 138;
            Rectangle card = new(panelRect.Right + 12, (int)(panelCenter.Y - h / 2f), w, h);
            VictorUIStyle.DrawHoloFrame(sb, card, CyberwareTheme.AccentCyan, alpha * 0.92f, GlobalTimer);

            //脉冲箭头指回人体槽位
            float ax = card.X - 10 - pulse * 8f;
            Utils.DrawBorderString(sb, "◄", new Vector2(ax, card.Center.Y - 14),
                CyberwareTheme.AccentCyan * (alpha * pulse), 0.7f * CyberwareTheme.FontScale);

            string head = TitleText.Value;
            float hs = 0.5f * CyberwareTheme.FontScale;
            Vector2 hsz = FontAssets.MouseText.Value.MeasureString(head) * hs;
            Utils.DrawBorderString(sb, head, new Vector2(card.Center.X - hsz.X / 2f, card.Y + 14),
                CyberwareTheme.AccentCyan * alpha, hs);
            VictorUIStyle.DrawHDivider(sb, card.X + 16, card.Right - 16, card.Y + 38, CyberwareTheme.AccentCyan * (alpha * 0.5f));

            float gs = 0.46f * CyberwareTheme.FontScale;
            string[] lines = VaultUtils.WrapTextArray(GuideText.Value, FontAssets.MouseText.Value, w - 32, 4, out _);
            float lineH = FontAssets.MouseText.Value.MeasureString("A").Y * gs + 6f;
            int cnt = 0;
            foreach (string l in lines) {
                if (!string.IsNullOrEmpty(l)) {
                    cnt++;
                }
            }
            float y = card.Y + 52 + Math.Max(0f, (h - 52 - cnt * lineH) / 2f);
            foreach (string line in lines) {
                if (string.IsNullOrEmpty(line)) {
                    continue;
                }
                Vector2 sz = FontAssets.MouseText.Value.MeasureString(line) * gs;
                Utils.DrawBorderString(sb, line, new Vector2(card.Center.X - sz.X / 2f, y),
                    CyberwareTheme.TextBright * (alpha * (0.75f + 0.25f * pulse)), gs);
                y += lineH;
            }
        }

        private void RefreshSlotLabelCache() {
            if (slotLabels == null) {
                return;
            }
            for (int i = 0; i < slotLabels.Length && i < slotLabelCache.Length; i++) {
                slotLabelCache[i] = slotLabels[i].Value;
            }
        }
    }
}
