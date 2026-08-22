using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Industrials.UIs;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Launchers
{
    /// <summary>
    /// 弹射平台控制面板:方向/力度调节 + 启停 + 电力表盘,
    /// 笔刷与材质在 <see cref="IndustrialTerminalRenderer"/>
    /// </summary>
    internal class PlayerLauncherUI : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "UI.PlayerLauncher";

        #region 布局与状态
        private const float PanelWidth = 360f;
        private const float PanelHeight = 252f;

        private static Color TextMain => IndustrialTerminalRenderer.TextMain;
        private static Color TextDim => IndustrialTerminalRenderer.TextDim;
        private static Color WarnRed => IndustrialTerminalRenderer.WarnRed;
        private static Color Accent => PlayerLauncher.Tint;

        private static float UIScreenW => PlayerInput.RealScreenWidth / Main.UIScale;
        private static float UIScreenH => PlayerInput.RealScreenHeight / Main.UIScale;

        public static PlayerLauncherUI Instance => UIHandleLoader.GetUIHandleOfType<PlayerLauncherUI>();

        internal PlayerLauncherTP Station;
        internal bool IsActive;

        private float uiFadeAlpha;
        public override bool Active => IsActive || uiFadeAlpha > 0.01f;

        //电力表指针弹簧
        private float powerDisplay;
        private float powerVel;
        private float latchHover;
        private float animTimer;

        //拖拽
        private bool isDragging;
        private Vector2 dragOffset;
        private bool positionInitialized;

        //布局矩形
        private Rectangle panelRect;
        private Rectangle closeRect;
        private Rectangle dirLeftBtn;
        private Rectangle dirRightBtn;
        private Rectangle powerDownBtn;
        private Rectangle powerUpBtn;
        private Rectangle powerBarRect;
        private Rectangle toggleBtn;
        private Vector2 gaugeCenter;
        private Rectangle gaugeRect;
        private Vector2 previewCenter;

        private bool hoveringDirLeft;
        private bool hoveringDirRight;
        private bool hoveringPowerDown;
        private bool hoveringPowerUp;
        private bool hoveringToggle;
        private bool hoveringGauge;
        private bool hoveringPowerBar;
        #endregion

        #region 本地化
        protected static LocalizedText TitleText;
        protected static LocalizedText DirectionLabel;
        protected static LocalizedText PowerLabel;
        protected static LocalizedText EnableText;
        protected static LocalizedText DisableText;
        protected static LocalizedText StatusReady;
        protected static LocalizedText StatusNoPower;
        protected static LocalizedText StatusOff;
        protected static LocalizedText EnergyLabel;
        protected static LocalizedText CostTip;
        protected static LocalizedText PowerUnit;

        public override Texture2D Texture => VaultAsset.placeholder2.Value;

        public override void SetStaticDefaults() {
            TitleText = this.GetLocalization(nameof(TitleText), () => "弹射平台");
            DirectionLabel = this.GetLocalization(nameof(DirectionLabel), () => "弹射方向");
            PowerLabel = this.GetLocalization(nameof(PowerLabel), () => "弹射力度");
            EnableText = this.GetLocalization(nameof(EnableText), () => "启用");
            DisableText = this.GetLocalization(nameof(DisableText), () => "停用");
            StatusReady = this.GetLocalization(nameof(StatusReady), () => "就绪");
            StatusNoPower = this.GetLocalization(nameof(StatusNoPower), () => "缺电");
            StatusOff = this.GetLocalization(nameof(StatusOff), () => "已停用");
            EnergyLabel = this.GetLocalization(nameof(EnergyLabel), () => "电力");
            CostTip = this.GetLocalization(nameof(CostTip), () => "单次弹射消耗 {0} UE");
            PowerUnit = this.GetLocalization(nameof(PowerUnit), () => "UE");
        }
        #endregion

        public void Interactive(PlayerLauncherTP tp) {
            if (tp == null) {
                return;
            }

            if (Station == tp) {
                IsActive = !IsActive;
            }
            else {
                IsActive = true;
            }

            Station = tp;
            SoundEngine.PlaySound(CWRSound.ButtonZero with { Volume = 0.3f, Pitch = -0.5f });
        }

        public override void Update() {
            if (!positionInitialized && Main.screenWidth > 0) {
                positionInitialized = true;
                if (DrawPosition.X < PanelWidth / 2 + 10 && DrawPosition.Y < PanelHeight / 2 + 10) {
                    DrawPosition = new Vector2(UIScreenW * 0.5f, UIScreenH * 0.5f);
                }
            }

            DrawPosition.X = MathHelper.Clamp(DrawPosition.X, PanelWidth / 2 + 10, UIScreenW - PanelWidth / 2 - 10);
            DrawPosition.Y = MathHelper.Clamp(DrawPosition.Y, PanelHeight / 2 + 10, UIScreenH - PanelHeight / 2 - 10);

            animTimer += 1f / 60f;

            float targetAlpha = IsActive ? 1f : 0f;
            uiFadeAlpha = MathHelper.Lerp(uiFadeAlpha, targetAlpha, 0.15f);
            if (uiFadeAlpha < 0.01f && !IsActive) {
                return;
            }

            if (Station == null || !Station.Active) {
                IsActive = false;
                return;
            }

            if (Main.LocalPlayer.DistanceSQ(Station.CenterInWorld) > 40000) {
                IsActive = false;
                SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.4f });
                return;
            }

            ComputeLayout();

            //表针弹簧
            float powerTarget = MathHelper.Clamp(Station.MachineData.UEvalue / Station.MaxUEValue, 0f, 1f);
            powerVel = powerVel * 0.80f + (powerTarget - powerDisplay) * 0.05f;
            powerDisplay += powerVel;

            Point mouse = MousePosition.ToPoint();
            hoveringDirLeft = dirLeftBtn.Contains(mouse) && !isDragging;
            hoveringDirRight = dirRightBtn.Contains(mouse) && !isDragging;
            hoveringPowerDown = powerDownBtn.Contains(mouse) && !isDragging;
            hoveringPowerUp = powerUpBtn.Contains(mouse) && !isDragging;
            hoveringToggle = toggleBtn.Contains(mouse) && !isDragging;
            hoveringGauge = gaugeRect.Contains(mouse) && !isDragging;
            hoveringPowerBar = powerBarRect.Contains(mouse) && !isDragging;
            hoverInMainPage = panelRect.Contains(mouse);
            latchHover = MathHelper.Lerp(latchHover, closeRect.Contains(mouse) ? 1f : 0f, 0.2f);

            if (hoverInMainPage) {
                player.mouseInterface = true;
                UIInputGuard.SuppressWeaponSwitch();
            }

            //闩钮关闭
            if (closeRect.Contains(mouse) && keyLeftPressState == KeyPressState.Pressed) {
                IsActive = false;
                SoundEngine.PlaySound(CWRSound.ButtonZero with { Pitch = -0.3f, Volume = 0.5f });
                return;
            }

            HandleButtonClicks();

            //背景区拖拽,避开控件
            bool overControl = hoveringDirLeft || hoveringDirRight || hoveringPowerDown
                || hoveringPowerUp || hoveringToggle || closeRect.Contains(mouse);
            if (keyLeftPressState == KeyPressState.Pressed && hoverInMainPage && !overControl && !isDragging) {
                isDragging = true;
                dragOffset = MousePosition - DrawPosition;
            }
            if (isDragging) {
                DrawPosition = MousePosition - dragOffset;
                if (keyLeftPressState == KeyPressState.Released) {
                    isDragging = false;
                }
            }
        }

        private void ComputeLayout() {
            Vector2 topLeft = DrawPosition - new Vector2(PanelWidth / 2, PanelHeight / 2);
            panelRect = new Rectangle((int)topLeft.X, (int)topLeft.Y, (int)PanelWidth, (int)PanelHeight);
            closeRect = new Rectangle(panelRect.Right - 38, panelRect.Y + 9, 26, 26);
            dirLeftBtn = new Rectangle(panelRect.X + 178, panelRect.Y + 56, 24, 22);
            dirRightBtn = new Rectangle(panelRect.X + 206, panelRect.Y + 56, 24, 22);
            powerDownBtn = new Rectangle(panelRect.X + 178, panelRect.Y + 90, 24, 22);
            powerUpBtn = new Rectangle(panelRect.X + 206, panelRect.Y + 90, 24, 22);
            powerBarRect = new Rectangle(panelRect.X + 26, panelRect.Y + 126, 204, 10);
            toggleBtn = new Rectangle(panelRect.X + 26, panelRect.Y + 152, 96, 30);
            gaugeCenter = new Vector2(panelRect.X + 296, panelRect.Y + 186);
            gaugeRect = new Rectangle((int)gaugeCenter.X - 32, (int)gaugeCenter.Y - 32, 64, 64);
            previewCenter = new Vector2(panelRect.X + 296, panelRect.Y + 88);
        }

        private void HandleButtonClicks() {
            if (Station == null || keyLeftPressState != KeyPressState.Pressed) {
                return;
            }

            if (hoveringDirLeft || hoveringDirRight) {
                Station.LaunchDirection += hoveringDirRight ? 15f : -15f;
                if (Station.LaunchDirection > 180f) {
                    Station.LaunchDirection -= 360f;
                }
                if (Station.LaunchDirection < -180f) {
                    Station.LaunchDirection += 360f;
                }
                Station.SendData();
                SoundEngine.PlaySound(SoundID.MenuTick);
            }
            else if (hoveringPowerDown || hoveringPowerUp) {
                float delta = hoveringPowerUp ? 2f : -2f;
                Station.LaunchPower = MathHelper.Clamp(Station.LaunchPower + delta, 4f, 32f);
                Station.SendData();
                SoundEngine.PlaySound(SoundID.MenuTick);
            }
            else if (hoveringToggle) {
                Station.Enabled = !Station.Enabled;
                Station.SendData();
                SoundEngine.PlaySound(Station.Enabled ? SoundID.MenuOpen : SoundID.MenuClose);
            }
        }

        public override void OnEnterWorld() => IsActive = false;

        public override void SaveUIData(TagCompound tag) {
            tag["PlayerLauncherUI_DrawPos_X"] = DrawPosition.X;
            tag["PlayerLauncherUI_DrawPos_Y"] = DrawPosition.Y;
        }

        public override void LoadUIData(TagCompound tag) {
            if (tag.TryGet("PlayerLauncherUI_DrawPos_X", out float x)) {
                DrawPosition.X = x;
            }
            if (tag.TryGet("PlayerLauncherUI_DrawPos_Y", out float y)) {
                DrawPosition.Y = y;
            }
        }

        #region 绘制
        public override void Draw(SpriteBatch spriteBatch) {
            if (uiFadeAlpha < 0.01f || Station == null) {
                return;
            }

            DrawShell(spriteBatch);
            DrawControls(spriteBatch);
            DrawPreviewAndGauge(spriteBatch);
            DrawStatusLamp(spriteBatch);
            DrawHoverTips(spriteBatch);
        }

        private void DrawShell(SpriteBatch sb) {
            float alpha = uiFadeAlpha;

            IndustrialTerminalRenderer.ShaderPanel(sb, panelRect, alpha);

            int inset = IndustrialTerminalRenderer.Chamfer + 2;
            IndustrialTerminalRenderer.DrawRivet(sb, new Vector2(panelRect.X + inset, panelRect.Y + inset), alpha);
            IndustrialTerminalRenderer.DrawRivet(sb, new Vector2(panelRect.Right - inset, panelRect.Y + inset), alpha);
            IndustrialTerminalRenderer.DrawRivet(sb, new Vector2(panelRect.X + inset, panelRect.Bottom - inset), alpha);
            IndustrialTerminalRenderer.DrawRivet(sb, new Vector2(panelRect.Right - inset, panelRect.Bottom - inset), alpha);

            string title = TitleText.Value;
            Vector2 titleSize = FontAssets.MouseText.Value.MeasureString(title) * 0.86f;
            Rectangle plate = new(panelRect.X + 22, panelRect.Y + 9, (int)titleSize.X + 30, 27);
            IndustrialTerminalRenderer.DrawNameplate(sb, plate, alpha);
            IndustrialTerminalRenderer.DrawPlateTitle(sb, plate, title, alpha, 0.86f);

            IndustrialTerminalRenderer.DrawEtchedLine(sb, panelRect.X + 14, panelRect.Width - 28, panelRect.Y + 44, alpha, 0.8f);
            IndustrialTerminalRenderer.DrawLatch(sb, closeRect.Center.ToVector2(), alpha, latchHover);
        }

        private void DrawControls(SpriteBatch sb) {
            float alpha = uiFadeAlpha;

            //方向行
            Utils.DrawBorderString(sb, DirectionLabel.Value,
                new Vector2(panelRect.X + 26, panelRect.Y + 59), TextDim * alpha, 0.62f);
            Utils.DrawBorderString(sb, $"{Station.LaunchDirection:F0}°",
                new Vector2(panelRect.X + 116, panelRect.Y + 59), Color.Lerp(TextMain, Accent, 0.4f) * alpha, 0.62f);
            IndustrialTerminalRenderer.DrawButton(sb, dirLeftBtn, alpha, hoveringDirLeft ? 1f : 0f,
                hoveringDirLeft && keyLeftPressState == KeyPressState.Held, "<");
            IndustrialTerminalRenderer.DrawButton(sb, dirRightBtn, alpha, hoveringDirRight ? 1f : 0f,
                hoveringDirRight && keyLeftPressState == KeyPressState.Held, ">");

            //力度行
            Utils.DrawBorderString(sb, PowerLabel.Value,
                new Vector2(panelRect.X + 26, panelRect.Y + 93), TextDim * alpha, 0.62f);
            Utils.DrawBorderString(sb, $"{Station.LaunchPower:F0}",
                new Vector2(panelRect.X + 116, panelRect.Y + 93), Color.Lerp(TextMain, Accent, 0.4f) * alpha, 0.62f);
            IndustrialTerminalRenderer.DrawButton(sb, powerDownBtn, alpha, hoveringPowerDown ? 1f : 0f,
                hoveringPowerDown && keyLeftPressState == KeyPressState.Held, "-");
            IndustrialTerminalRenderer.DrawButton(sb, powerUpBtn, alpha, hoveringPowerUp ? 1f : 0f,
                hoveringPowerUp && keyLeftPressState == KeyPressState.Held, "+");

            //力度刻度条
            IndustrialTerminalRenderer.DrawTickBar(sb, powerBarRect,
                (Station.LaunchPower - 4f) / 28f, Accent, alpha);

            //启停按钮
            string toggleLabel = Station.Enabled ? DisableText.Value : EnableText.Value;
            IndustrialTerminalRenderer.DrawButton(sb, toggleBtn, alpha, hoveringToggle ? 1f : 0f,
                hoveringToggle && keyLeftPressState == KeyPressState.Held, toggleLabel);
        }

        private void DrawPreviewAndGauge(SpriteBatch sb) {
            float alpha = uiFadeAlpha;

            //方向预览:刻度圈 + 复用输入箭头
            Texture2D px = VaultAsset.placeholder2.Value;
            for (int i = 0; i < 24; i++) {
                float angle = MathHelper.TwoPi * i / 24f;
                Vector2 pos = previewCenter + angle.ToRotationVector2() * 22f;
                sb.Draw(px, pos, new Rectangle(0, 0, 1, 1),
                    TextDim * (alpha * 0.4f), 0f, new Vector2(0.5f), 2f, SpriteEffects.None, 0f);
            }

            var arrowAsset = Throwers.Thrower.InputArrow;
            if (arrowAsset != null) {
                float radians = MathHelper.ToRadians(Station.LaunchDirection);
                float pulse = MathF.Sin(animTimer * 3f) * 0.15f + 0.85f;
                sb.Draw(arrowAsset.Value, previewCenter + radians.ToRotationVector2() * 12f, null,
                    Accent * (alpha * pulse), radians, arrowAsset.Value.Size() / 2f, 0.55f, SpriteEffects.None, 0f);
            }

            //电力表盘
            float jitter = Station.GlowIntensity > 0.5f ? MathF.Sin(animTimer * 30f) * 0.004f : 0f;
            float ratio = MathHelper.Clamp(Station.MachineData.UEvalue / Station.MaxUEValue, 0f, 1f);
            IndustrialTerminalRenderer.DrawGauge(sb, gaugeCenter, 30f, powerDisplay + jitter,
                Accent, alpha, EnergyLabel.Value, $"{(int)(ratio * 100f)}%");
        }

        private void DrawStatusLamp(SpriteBatch sb) {
            float alpha = uiFadeAlpha;
            Vector2 lampPos = new(panelRect.X + 146, panelRect.Y + 167);

            string state;
            Color lampColor;
            float lampBright;
            if (!Station.Enabled) {
                state = StatusOff.Value;
                lampColor = TextDim;
                lampBright = 0.2f;
            }
            else if (Station.MachineData.UEvalue < Station.LaunchCost) {
                state = StatusNoPower.Value;
                lampColor = WarnRed;
                lampBright = MathF.Sin(animTimer * 5f) * 0.35f + 0.55f;
            }
            else {
                state = StatusReady.Value;
                lampColor = Accent;
                lampBright = MathF.Sin(animTimer * 2.6f) * 0.2f + 0.72f;
            }

            IndustrialTerminalRenderer.DrawLamp(sb, lampPos, lampColor, alpha, lampBright);
            Utils.DrawBorderString(sb, state, lampPos + new Vector2(14, -8),
                Color.Lerp(TextMain, lampColor, 0.35f) * alpha, 0.66f);
        }

        private void DrawHoverTips(SpriteBatch sb) {
            if (isDragging) {
                return;
            }
            if (hoveringGauge) {
                ShowTip(sb, $"{(int)Station.MachineData.UEvalue}/{(int)Station.MaxUEValue} {PowerUnit.Value}");
            }
            else if (hoveringPowerBar) {
                ShowTip(sb, string.Format(CostTip.Value, (int)Station.LaunchCost));
            }
        }

        private static void ShowTip(SpriteBatch sb, string text) {
            Vector2 textSize = FontAssets.MouseText.Value.MeasureString(text) * 0.75f;
            Vector2 pos = new Vector2(Main.mouseX, Main.mouseY) + new Vector2(18, 18);
            if (pos.X + textSize.X + 20 > UIScreenW) {
                pos.X = Main.mouseX - textSize.X - 24;
            }
            if (pos.Y + textSize.Y + 12 > UIScreenH) {
                pos.Y = Main.mouseY - textSize.Y - 18;
            }

            Rectangle bg = new((int)pos.X - 9, (int)pos.Y - 5, (int)textSize.X + 18, (int)textSize.Y + 10);
            IndustrialTerminalRenderer.DrawTooltipPlate(sb, bg, 1f);
            Utils.DrawBorderString(sb, text, pos, IndustrialTerminalRenderer.TextMain, 0.75f);
        }
        #endregion
    }
}
