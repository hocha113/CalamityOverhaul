using CalamityOverhaul.Common;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.UIs
{
    /// <summary>义体管理界面，中心像素人体+周围槽位</summary>
    internal class CyberwareUI : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "UI";

        #region 本地化文本

        public static LocalizedText TitleText { get; private set; }
        public static LocalizedText StatusText { get; private set; }
        public static LocalizedText SlotSelectedText { get; private set; }
        public static LocalizedText SlotEmptyText { get; private set; }

        //只读界面点击换装时"请找 Victor"提醒
        public static LocalizedText ClinicRequiredTitle { get; private set; }
        public static LocalizedText ClinicRequiredDesc { get; private set; }

        //对应 Definitions 12 槽位
        private static LocalizedText[] slotLabelTexts;

        //槽位标签缓存
        private readonly string[] slotLabelCache = new string[12];

        public override void SetStaticDefaults() {
            TitleText = this.GetLocalization(nameof(TitleText), () => "CYBERWARE");
            StatusText = this.GetLocalization(nameof(StatusText), () => "SYSTEM STATUS: OPERATIONAL");
            SlotSelectedText = this.GetLocalization(nameof(SlotSelectedText), () => "[ SELECTED ]");
            SlotEmptyText = this.GetLocalization(nameof(SlotEmptyText), () => "EMPTY");
            ClinicRequiredTitle = this.GetLocalization(nameof(ClinicRequiredTitle), () => "CYBERWARE LOCKED");
            ClinicRequiredDesc = this.GetLocalization(nameof(ClinicRequiredDesc), () => "Only a ripperdoc can install implants. Visit Victor.");

            slotLabelTexts = [
                this.GetLocalization("Slot_FrontalCortex", () => "FRONTAL CORTEX"),
                this.GetLocalization("Slot_OcularSystem", () => "OCULAR SYSTEM"),
                this.GetLocalization("Slot_LeftArm", () => "LEFT ARM"),
                this.GetLocalization("Slot_Hands", () => "HANDS"),
                this.GetLocalization("Slot_LeftLeg", () => "LEFT LEG"),
                this.GetLocalization("Slot_Feet", () => "FEET"),
                this.GetLocalization("Slot_OperatingSystem", () => "OPERATING SYSTEM"),
                this.GetLocalization("Slot_NervousSystem", () => "NERVOUS SYSTEM"),
                this.GetLocalization("Slot_RightArm", () => "RIGHT ARM"),
                this.GetLocalization("Slot_CirculatorySystem", () => "CIRCULATORY SYS"),
                this.GetLocalization("Slot_Skeleton", () => "SKELETON"),
                this.GetLocalization("Slot_RightLeg", () => "RIGHT LEG"),
            ];
        }

        #endregion

        #region 子渲染器

        private readonly CyberBodyRenderer bodyRenderer = new();
        private readonly CyberPanelRenderer panelRenderer = new();
        private readonly CyberSlotRenderer slotRenderer = new();
        private readonly CyberDataParticleSystem particleSystem = new();
        private readonly CyberInventoryPanel inventoryPanel = new();

        #endregion

        #region 状态

        private bool isOpen;
        private float openProgress;
        private bool isClosing;
        private float closeAnimTimer;
        private float globalTimer;
        private float dataStreamPhase;
        private Rectangle panelRect;
        private Vector2 panelCenter;
        private Vector2 bodyOrigin;

        //动画中间值，Draw 直读
        private float currentWidthProgress;
        private float currentHeightProgress;
        private float currentAlpha;
        private float currentContentAlpha;
        private float closeLineGlow;
        private bool closeButtonHovered;

        #endregion

        #region 属性

        public static CyberwareUI Instance => UIHandleLoader.GetUIHandleOfType<CyberwareUI>();

        public override bool Active => isOpen || openProgress > 0.01f || isClosing;

        /// <summary>槽位本地化标签</summary>
        public string GetSlotLabel(int slotIndex) {
            if (slotIndex >= 0 && slotIndex < slotLabelCache.Length) {
                return slotLabelCache[slotIndex];
            }
            return TitleText.Value;
        }

        #endregion

        #region 公共接口

        /// <summary>打开界面</summary>
        public void Open() {
            if (!isOpen) {
                isOpen = true;
                if (isClosing) {
                    openProgress = Math.Max(0.05f, currentWidthProgress);
                    isClosing = false;
                    closeAnimTimer = 0f;
                }
                panelRenderer.TriggerGlitch(0.8f);

                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(CWRSound.Scanning, Main.LocalPlayer.Center);
                }
            }
        }

        /// <summary>关闭界面</summary>
        public void Close() {
            if (isOpen) {
                isOpen = false;
                isClosing = true;
                closeAnimTimer = 0f;
                panelRenderer.TriggerGlitch(0.6f);

                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(CWRSound.Scanning, Main.LocalPlayer.Center);
                }
            }
        }

        /// <summary>切换开关</summary>
        public void Toggle() {
            if (isOpen) Close();
            else Open();
        }

        #endregion

        #region 更新

        public override void Update() {
            //关闭动画
            if (isClosing) {
                UpdateCloseAnimation();
                return;
            }

            //打开过渡动画
            float targetProgress = isOpen ? 1f : 0f;
            openProgress += (targetProgress - openProgress) * 0.12f;
            if (!isOpen && openProgress < 0.01f) openProgress = 0f;
            if (openProgress < 0.01f) return;

            //推进全局计时器
            globalTimer += 0.016f;
            dataStreamPhase += 0.03f;
            if (dataStreamPhase > MathHelper.TwoPi) dataStreamPhase -= MathHelper.TwoPi;

            //计算面板布局（开启时宽高统一缩放）
            float easedProgress = VaultUtils.EaseOutCubic(Math.Clamp(openProgress, 0, 1));
            currentWidthProgress = easedProgress;
            currentHeightProgress = easedProgress;
            currentAlpha = easedProgress;
            currentContentAlpha = easedProgress;
            closeLineGlow = 0f;

            float panelW = CyberwareTheme.PanelWidth * currentWidthProgress;
            float panelH = CyberwareTheme.PanelHeight * currentHeightProgress;
            panelCenter = new Vector2(Main.screenWidth / 2f, Main.screenHeight / 2f);
            panelRect = new Rectangle(
                (int)(panelCenter.X - panelW / 2f),
                (int)(panelCenter.Y - panelH / 2f),
                (int)panelW, (int)panelH
            );
            bodyOrigin = panelCenter + new Vector2(0, 5);

            //更新各子渲染器
            bodyRenderer.Update();
            panelRenderer.Update();

            if (slotRenderer.UpdateInteraction(panelRect)) {
                panelRenderer.TriggerGlitch(0.3f);
            }
            slotRenderer.UpdateAnimations();
            bodyRenderer.SetFocusNode(slotRenderer.FocusedNodeIndex, slotRenderer.FocusStrength);

            particleSystem.Update(bodyOrigin, openProgress);

            //更新义体背包面板
            var cyberPlayer = player.GetModPlayer<CyberwarePlayer>();
            inventoryPanel.Update(panelRect, slotRenderer.SelectedSlot, cyberPlayer);
            if (inventoryPanel.ActionThisFrame) {
                panelRenderer.TriggerGlitch(0.5f);
            }

            //检测关闭按钮交互
            Rectangle closeBtnRect = CyberPanelRenderer.GetCloseButtonRect(panelRect);
            closeButtonHovered = closeBtnRect.Contains(Main.mouseX, Main.mouseY);
            if (closeButtonHovered) {
                player.mouseInterface = true;
                if (Main.mouseLeft && Main.mouseLeftRelease) {
                    Close();
                    return;
                }
            }

            //拦截面板区域内的游戏输入
            if (isOpen && panelRect.Contains(Main.mouseX, Main.mouseY)) {
                player.mouseInterface = true;
            }
        }

        /// <summary>关闭动画：Phase1 纵向压缩→Phase2 水平坍缩</summary>
        private void UpdateCloseAnimation() {
            const float closeSpeed = 0.038f;
            const float phase1End = 0.6f;

            float prevTimer = closeAnimTimer;
            closeAnimTimer = Math.Min(closeAnimTimer + closeSpeed, 1f);

            //Phase1→2 过渡故障闪
            if (prevTimer < phase1End && closeAnimTimer >= phase1End) {
                panelRenderer.TriggerGlitch(0.9f);
            }

            if (closeAnimTimer >= 1f) {
                isClosing = false;
                openProgress = 0f;
                closeAnimTimer = 0f;
                return;
            }

            float t = closeAnimTimer;

            if (t < phase1End) {
                //Phase1 纵向压细线，内容淡出
                float p = t / phase1End;
                float hp = VaultUtils.EaseInCubic(p);
                currentHeightProgress = Math.Max(0.007f, 1f - hp);
                currentWidthProgress = 1f - p * 0.015f;
                currentAlpha = 1f - p * 0.1f;
                currentContentAlpha = Math.Max(0f, 1f - p * 3.5f);
                closeLineGlow = p * 0.5f;
            }
            else {
                //Phase2 水平坍缩+亮线
                float p = (t - phase1End) / (1f - phase1End);
                float wp = VaultUtils.EaseInQuad(p);
                currentHeightProgress = 0.007f;
                currentWidthProgress = (1f - 0.015f) * (1f - wp);
                currentAlpha = 0.9f * (1f - wp);
                currentContentAlpha = 0f;
                closeLineGlow = (1f - p) * 1.5f;
            }

            //推进计时器
            globalTimer += 0.016f;
            dataStreamPhase += 0.03f;
            if (dataStreamPhase > MathHelper.TwoPi) dataStreamPhase -= MathHelper.TwoPi;

            //计算面板布局
            panelCenter = new Vector2(Main.screenWidth / 2f, Main.screenHeight / 2f);
            float panelW = CyberwareTheme.PanelWidth * currentWidthProgress;
            float panelH = Math.Max(3, CyberwareTheme.PanelHeight * currentHeightProgress);
            panelRect = new Rectangle(
                (int)(panelCenter.X - panelW / 2f),
                (int)(panelCenter.Y - panelH / 2f),
                (int)panelW, (int)panelH
            );
            bodyOrigin = panelCenter + new Vector2(0, 5);

            //子渲染器继续更新
            bodyRenderer.SetFocusNode(-1, 0f);
            bodyRenderer.Update();
            panelRenderer.Update();
            particleSystem.Update(bodyOrigin, currentAlpha);
        }

        #endregion

        #region 绘制

        public override void Draw(SpriteBatch spriteBatch) {
            if (!isClosing && openProgress < 0.01f) return;

            //刷新本地化缓存
            RefreshSlotLabelCache();

            //shader 底+人体光场，bodyR 随 contentAlpha 衰减
            Vector2 bodyLocalCenter = bodyOrigin - new Vector2(panelRect.X, panelRect.Y);
            float bodyR = CyberwareTheme.BodyHaloRadius * MathHelper.Clamp(currentContentAlpha, 0f, 1f);
            CyberPanelRenderer.DrawShaderBackground(spriteBatch, currentAlpha, panelRect, bodyLocalCenter, bodyR, mode: 0);
            //CPU 四角括号+顶脉冲
            CyberPanelRenderer.DrawFrameDecor(spriteBatch, currentAlpha, panelRect, globalTimer);

            RasterizerState rasterizerState = new RasterizerState { ScissorTestEnable = true };
            spriteBatch.End();

            int margin = 4;
            Vector2 clipPos = Vector2.Transform(new Vector2(panelRect.X + margin, panelRect.Y + margin), Main.UIScaleMatrix);
            Vector2 clipSize = Vector2.Transform(new Vector2(panelRect.Width - margin * 2, panelRect.Height - margin * 2), Main.UIScaleMatrix)
                - Vector2.Transform(Vector2.Zero, Main.UIScaleMatrix);
            Rectangle scissorRect = new Rectangle((int)clipPos.X, (int)clipPos.Y, (int)clipSize.X, (int)clipSize.Y);
            Rectangle originalScissorRect = spriteBatch.GraphicsDevice.ScissorRectangle;
            scissorRect = Rectangle.Intersect(scissorRect, spriteBatch.GraphicsDevice.Viewport.Bounds);

            spriteBatch.GraphicsDevice.ScissorRectangle = scissorRect;
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, rasterizerState, null, Main.UIScaleMatrix);

            //内容层，关闭时淡出
            if (currentContentAlpha > 0.01f) {
                bodyRenderer.DrawBody(spriteBatch, currentContentAlpha, bodyOrigin, globalTimer);
                bodyRenderer.DrawNodes(spriteBatch, currentContentAlpha, bodyOrigin, slotRenderer.ComputeNodeStates());

                slotRenderer.DrawConnectors(spriteBatch, currentContentAlpha, panelRect, bodyRenderer, bodyOrigin, dataStreamPhase);
                var cyberPlayer = player.GetModPlayer<CyberwarePlayer>();
                slotRenderer.DrawSlots(spriteBatch, currentContentAlpha, panelRect, slotLabelCache,
                    SlotSelectedText.Value, SlotEmptyText.Value, cyberPlayer);

                panelRenderer.DrawTitleAndDecor(spriteBatch, currentContentAlpha, panelRect, panelCenter,
                    globalTimer, TitleText.Value, StatusText.Value);
            }

            particleSystem.Draw(spriteBatch, currentAlpha);

            spriteBatch.End();
            spriteBatch.GraphicsDevice.ScissorRectangle = originalScissorRect;
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);

            panelRenderer.DrawGlitchEffect(spriteBatch, currentAlpha, panelRect);

            //关闭钮，Scissor 外绘
            if (currentContentAlpha > 0.01f) {
                panelRenderer.DrawCloseButton(spriteBatch, currentContentAlpha, panelRect, closeButtonHovered);
            }

            //侧栏背包，Scissor 外绘
            if (currentContentAlpha > 0.01f) {
                var cyberPlayer = player.GetModPlayer<CyberwarePlayer>();
                inventoryPanel.Draw(spriteBatch, currentContentAlpha, cyberPlayer);
            }

            //关闭动画的科幻亮线效果
            if (isClosing && closeLineGlow > 0.01f) {
                DrawCloseEffectLine(spriteBatch);
            }
        }

        /// <summary>关闭动画水平亮线</summary>
        private void DrawCloseEffectLine(SpriteBatch spriteBatch) {
            Texture2D px = CWRAsset.Placeholder_White?.Value;
            if (px == null) return;

            float glow = Math.Min(closeLineGlow, 1f);
            int lineY = (int)panelCenter.Y;
            int lineX = panelRect.X;
            int lineW = panelRect.Width;

            //核心亮线
            Color lineColor = CyberwareTheme.Accent * glow;
            spriteBatch.Draw(px, new Rectangle(lineX, lineY - 1, lineW, 3),
                new Rectangle(0, 0, 1, 1), lineColor);

            //外层柔光
            Color softColor = CyberwareTheme.Accent * (glow * 0.35f);
            spriteBatch.Draw(px, new Rectangle(lineX, lineY - 5, lineW, 11),
                new Rectangle(0, 0, 1, 1), softColor);

            //SoftGlow纹理增强发光感
            Texture2D glowTex = CWRAsset.SoftGlow?.Value;
            if (glowTex != null) {
                Color addGlow = CyberwareTheme.Accent * (glow * 0.5f);
                addGlow.A = 0;
                float scaleX = Math.Max(0.01f, lineW / 80f);
                spriteBatch.Draw(glowTex, panelCenter, null, addGlow, 0,
                    glowTex.Size() / 2, new Vector2(scaleX, 0.2f), SpriteEffects.None, 0);
            }
        }

        private void RefreshSlotLabelCache() {
            for (int i = 0; i < slotLabelTexts.Length && i < slotLabelCache.Length; i++) {
                slotLabelCache[i] = slotLabelTexts[i].Value;
            }
        }

        #endregion
    }
}
