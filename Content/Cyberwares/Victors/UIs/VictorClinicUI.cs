using CalamityOverhaul.Content.Cyberwares.UIs;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Victors.UIs
{
    /// <summary>
    /// Victor 的义体诊所界面：在 <see cref="CyberwareUI"/> 的人体 + 槽位视图基础上，
    /// 把"按部位查看/更换义体"与"按部位购买义体"整合到同一面板。
    /// <br/>复用 <see cref="CyberBodyRenderer"/> / <see cref="CyberSlotRenderer"/> / <see cref="CyberPanelRenderer"/> 等渲染组件，
    /// 侧栏换为 <see cref="VictorClinicPanel"/>（已安装 / 已拥有 / 在售）
    /// </summary>
    internal class VictorClinicUI : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "UI";

        public static VictorClinicUI Instance => UIHandleLoader.GetUIHandleOfType<VictorClinicUI>();

        public override bool CloseOnEscape => true;
        public override SoundStyle? OpenSound => SoundID.MenuOpen;
        public override SoundStyle? CloseSound => SoundID.MenuClose;

        #region 本地化

        public static LocalizedText TitleText { get; private set; }
        public static LocalizedText StatusText { get; private set; }
        private static LocalizedText slotSelectedText;
        private static LocalizedText slotEmptyText;
        private static LocalizedText[] slotLabels;
        private readonly string[] slotLabelCache = new string[CyberwarePlayer.SlotCount];

        public override void SetStaticDefaults() {
            TitleText = this.GetLocalization(nameof(TitleText), () => "VICTOR'S CLINIC");
            StatusText = this.GetLocalization(nameof(StatusText), () => "RIPPERDOC ONLINE - SELECT A LIMB");

            //槽位名称 / 选中 / 空 复用义体管理界面的既有翻译，避免重复维护
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
            return "CYBERWARE";
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
            //关闭时收起侧栏选择，避免下次打开残留
            slotRenderer.SelectedSlot = -1;
            clinicPanel.Unbind();
        }

        public override void Update() {
            if (!IsOpen && OpenProgress.Current <= 0.001f) {
                return;
            }

            dataStreamPhase += 0.03f;
            if (dataStreamPhase > MathHelper.TwoPi) {
                dataStreamPhase -= MathHelper.TwoPi;
            }

            float eased = CWRUtils.EaseOutCubic(MathHelper.Clamp(OpenProgress.Current, 0f, 1f));
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

            //仅在完全打开时响应槽位点击，淡出阶段只推进动画
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
                return;//淡出阶段不再处理关闭按钮 / 拦截输入
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

            //主面板着色器底层（含中央人体光场）
            Vector2 bodyLocalCenter = bodyOrigin - new Vector2(panelRect.X, panelRect.Y);
            float bodyR = CyberwareTheme.BodyHaloRadius * MathHelper.Clamp(currentContentAlpha, 0f, 1f);
            CyberPanelRenderer.DrawShaderBackground(spriteBatch, currentAlpha, panelRect, bodyLocalCenter, bodyR, mode: 0);
            CyberPanelRenderer.DrawFrameDecor(spriteBatch, currentAlpha, panelRect, GlobalTimer);

            //裁剪内容到面板内部
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
