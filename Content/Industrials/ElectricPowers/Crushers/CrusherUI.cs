using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Industrials.UIs;
using CalamityOverhaul.Content.UIs.UIEffect;
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
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Crushers
{
    /// <summary>
    /// 粉碎机面板:破碎仪器语言,钢壳、入料/出料插座、破碎腔(双颚咬合动画)、
    /// 进度刻度条、模块插座行、电力表盘。交互契约与焚化炉面板一致
    /// (点击入料/取料、超距自动关、位置持久化)
    /// </summary>
    internal class CrusherUI : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "Items";

        #region 布局与状态
        private const float PanelWidth = 420f;
        private const float PanelHeight = 332f;

        private static Color TextMain => IndustrialTerminalRenderer.TextMain;
        private static Color TextDim => IndustrialTerminalRenderer.TextDim;
        private static Color Amber => IndustrialTerminalRenderer.Amber;
        private static Color WarnRed => IndustrialTerminalRenderer.WarnRed;
        private static Color Brass => IndustrialTerminalRenderer.Brass;
        private static Color BrassBright => IndustrialTerminalRenderer.BrassBright;

        private static float UIScreenW => PlayerInput.RealScreenWidth / Main.UIScale;
        private static float UIScreenH => PlayerInput.RealScreenHeight / Main.UIScale;

        internal CrusherTP CurrentTP;
        internal bool IsActive;

        //淡入淡出(Active 放宽到淡出结束,收摊有过程)
        private float uiFadeAlpha;
        public override bool Active => IsActive || uiFadeAlpha > 0.01f;

        //工况包络:开工渐强,喂颚动画与状态灯
        private float workDisplay;
        //电力表指针弹簧
        private float powerDisplay;
        private float powerVel;
        private float latchHover;

        //拖拽
        private bool isDragging;
        private Vector2 dragOffset;
        private bool positionInitialized;

        //布局矩形
        private Rectangle panelRect;
        private Rectangle closeRect;
        private Rectangle inputSlotRect;
        private Rectangle outputSlotRect;
        private Rectangle chamberRect;
        private Rectangle progressBarRect;
        private Vector2 powerGaugeCenter;
        private Rectangle powerGaugeRect;
        private bool hoveringInputSlot;
        private bool hoveringOutputSlot;
        private bool hoveringPowerGauge;
        private bool hoveringSockets;

        //模块插座行(点击/校验/红闪/绘制在共享件里)
        private readonly ModuleSocketStrip socketStrip = new();

        //破碎腔石尘
        private readonly List<AshPRT> dusts = new();
        private int dustSpawnTimer;

        private float animTimer;

        private CrusherData CruData => CurrentTP?.CruData;
        #endregion

        #region 本地化
        protected static LocalizedText TitleText;
        protected static LocalizedText InputLabel;
        protected static LocalizedText OutputLabel;
        protected static LocalizedText ProgressLabel;
        protected static LocalizedText PowerLabel;
        protected static LocalizedText CrushingText;
        protected static LocalizedText IdleText;
        protected static LocalizedText NoPowerText;
        protected static LocalizedText InputHint;
        protected static LocalizedText OutputHint;
        protected static LocalizedText PowerUnit;

        public override Texture2D Texture => VaultAsset.placeholder2.Value;

        public override void SetStaticDefaults() {
            TitleText = this.GetLocalization(nameof(TitleText), () => "矿石粉碎机");
            InputLabel = this.GetLocalization(nameof(InputLabel), () => "矿料");
            OutputLabel = this.GetLocalization(nameof(OutputLabel), () => "产出");
            ProgressLabel = this.GetLocalization(nameof(ProgressLabel), () => "破碎");
            PowerLabel = this.GetLocalization(nameof(PowerLabel), () => "电力");
            CrushingText = this.GetLocalization(nameof(CrushingText), () => "破碎中");
            IdleText = this.GetLocalization(nameof(IdleText), () => "待机");
            NoPowerText = this.GetLocalization(nameof(NoPowerText), () => "缺电");
            InputHint = this.GetLocalization(nameof(InputHint), () => "放入矿石,两份碎出三份");
            OutputHint = this.GetLocalization(nameof(OutputHint), () => "点击取出碎矿");
            PowerUnit = this.GetLocalization(nameof(PowerUnit), () => "UE");
        }
        #endregion

        public void Interactive(CrusherTP tp, bool newTP) {
            if (tp == null) {
                return;
            }

            if (CurrentTP == tp && !newTP) {
                IsActive = !IsActive;
            }
            else {
                IsActive = true;
            }

            CurrentTP = tp;
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

            //验证TP有效性
            if (CurrentTP == null || !CurrentTP.Active) {
                IsActive = false;
                return;
            }

            //检查距离
            if (Main.LocalPlayer.DistanceSQ(CurrentTP.CenterInWorld) > 40000) {
                IsActive = false;
                SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.4f });
                return;
            }

            ComputeLayout();
            UpdateEnvelopes();
            socketStrip.Update();

            Point mouse = MousePosition.ToPoint();
            hoveringInputSlot = inputSlotRect.Contains(mouse) && !isDragging;
            hoveringOutputSlot = outputSlotRect.Contains(mouse) && !isDragging;
            hoveringPowerGauge = powerGaugeRect.Contains(mouse) && !isDragging;
            hoveringSockets = socketStrip.Contains(mouse) && !isDragging;
            hoverInMainPage = panelRect.Contains(mouse);
            latchHover = MathHelper.Lerp(latchHover, closeRect.Contains(mouse) ? 1f : 0f, 0.2f);

            if (hoverInMainPage) {
                player.mouseInterface = true;
                //悬停期间滚轮不翻快捷栏
                UIInputGuard.SuppressWeaponSwitch();
            }

            //闩钮关闭
            if (closeRect.Contains(mouse) && keyLeftPressState == KeyPressState.Pressed) {
                IsActive = false;
                SoundEngine.PlaySound(CWRSound.ButtonZero with { Pitch = -0.3f, Volume = 0.5f });
                return;
            }

            //模块插座行点击(先于拖拽捕获)
            if (keyLeftPressState == KeyPressState.Pressed && hoveringSockets && CurrentTP != null) {
                socketStrip.HandleClick(mouse, CurrentTP.ModuleRack, CrusherTP.ModuleSlotCount,
                    player, () => CurrentTP.SendData());
            }

            //背景区拖拽,避开料口/表盘/插座/闩钮
            if (keyLeftPressState == KeyPressState.Pressed && hoverInMainPage
                && !hoveringInputSlot && !hoveringOutputSlot && !hoveringPowerGauge && !hoveringSockets
                && !closeRect.Contains(mouse) && !isDragging) {
                isDragging = true;
                dragOffset = MousePosition - DrawPosition;
            }
            if (isDragging) {
                DrawPosition = MousePosition - dragOffset;
                if (keyLeftPressState == KeyPressState.Released) {
                    isDragging = false;
                }
            }

            //入料口交互
            if (hoveringInputSlot && CruData != null) {
                if (CruData.InputItem != null && !CruData.InputItem.IsAir) {
                    Main.HoverItem = CruData.InputItem.Clone();
                    Main.hoverItemName = CruData.InputItem.Name;
                }

                if (keyLeftPressState == KeyPressState.Pressed) {
                    CurrentTP.HandleInputItem();
                }
            }

            //出料口交互
            if (hoveringOutputSlot && CruData != null) {
                if (CruData.OutputItem != null && !CruData.OutputItem.IsAir) {
                    Main.HoverItem = CruData.OutputItem.Clone();
                    Main.hoverItemName = CruData.OutputItem.Name;
                }

                if (keyLeftPressState == KeyPressState.Pressed) {
                    CurrentTP.HandleOutputItem();
                }
            }

            UpdateParticles();
        }

        private void ComputeLayout() {
            Vector2 topLeft = DrawPosition - new Vector2(PanelWidth / 2, PanelHeight / 2);
            panelRect = new Rectangle((int)topLeft.X, (int)topLeft.Y, (int)PanelWidth, (int)PanelHeight);
            closeRect = new Rectangle(panelRect.Right - 38, panelRect.Y + 9, 26, 26);
            inputSlotRect = new Rectangle(panelRect.X + 36, panelRect.Y + 92, 64, 64);
            outputSlotRect = new Rectangle(panelRect.X + 320, panelRect.Y + 92, 64, 64);
            chamberRect = new Rectangle(panelRect.X + 140, panelRect.Y + 80, 140, 88);
            progressBarRect = new Rectangle(panelRect.X + 140, panelRect.Y + 178, 140, 8);
            powerGaugeCenter = new Vector2(panelRect.X + 352, panelRect.Y + 244);
            powerGaugeRect = new Rectangle((int)powerGaugeCenter.X - 32, (int)powerGaugeCenter.Y - 32, 64, 64);
            //底行:状态灯(左)+插座行(中)+电力表(右)
            socketStrip.Layout(panelRect.X + 150, panelRect.Y + 222,
                CurrentTP != null ? CrusherTP.ModuleSlotCount : 0, 40, 8);
        }

        /// <summary>工况包络与电力表指针弹簧</summary>
        private void UpdateEnvelopes() {
            CrusherData data = CruData;
            bool working = data != null && data.IsWorking;
            workDisplay = MathHelper.Lerp(workDisplay, working ? 1f : 0f, working ? 0.06f : 0.03f);

            float powerTarget = data != null ? MathHelper.Clamp(data.UEvalue / data.MaxUE, 0f, 1f) : 0f;
            powerVel = powerVel * 0.80f + (powerTarget - powerDisplay) * 0.05f;
            powerDisplay += powerVel;
        }

        /// <summary>石尘从破碎腔咬合处溅出</summary>
        private void UpdateParticles() {
            if (uiFadeAlpha < 0.3f || CruData == null) {
                return;
            }

            bool working = CruData.IsWorking;
            dustSpawnTimer++;
            if (working && dustSpawnTimer >= 6 && dusts.Count < 24) {
                dustSpawnTimer = 0;
                float xPos = Main.rand.NextFloat(chamberRect.X + 10, chamberRect.Right - 10);
                dusts.Add(new AshPRT(new Vector2(xPos, chamberRect.Center.Y)));
            }
            for (int i = dusts.Count - 1; i >= 0; i--) {
                if (dusts[i].Update()) {
                    dusts.RemoveAt(i);
                }
            }
        }

        public override void OnEnterWorld() => IsActive = false;

        public override void SaveUIData(TagCompound tag) {
            tag["CrusherUI_DrawPos_X"] = DrawPosition.X;
            tag["CrusherUI_DrawPos_Y"] = DrawPosition.Y;
        }

        public override void LoadUIData(TagCompound tag) {
            if (tag.TryGet("CrusherUI_DrawPos_X", out float x)) {
                DrawPosition.X = x;
            }
            else {
                DrawPosition.X = Main.screenWidth / 2;
            }

            if (tag.TryGet("CrusherUI_DrawPos_Y", out float y)) {
                DrawPosition.Y = y;
            }
            else {
                DrawPosition.Y = Main.screenHeight / 2;
            }
        }

        #region 绘制
        public override void Draw(SpriteBatch spriteBatch) {
            if (uiFadeAlpha < 0.01f) {
                return;
            }
            if (CruData == null) {
                return;
            }

            DrawShell(spriteBatch);
            DrawChamber(spriteBatch);

            foreach (AshPRT dust in dusts) {
                dust.Draw(spriteBatch, uiFadeAlpha * 0.6f);
            }

            DrawSlots(spriteBatch);
            DrawFlowChevrons(spriteBatch);
            DrawStatusRow(spriteBatch);
            DrawHoverTips(spriteBatch);
        }

        /// <summary>钢壳 + 铆钉 + 黄铜铭牌 + 闩钮</summary>
        private void DrawShell(SpriteBatch sb) {
            float alpha = uiFadeAlpha;

            IndustrialTerminalRenderer.ShaderPanel(sb, panelRect, alpha, mode: 0, heat: workDisplay * 0.25f);

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

        /// <summary>破碎腔:凹槽膛体 + 双颚咬合动画 + 进度刻度条</summary>
        private void DrawChamber(SpriteBatch sb) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float alpha = uiFadeAlpha;
            Rectangle src = new(0, 0, 1, 1);

            Vector2 labelSize = FontAssets.MouseText.Value.MeasureString(ProgressLabel.Value) * 0.6f;
            Utils.DrawBorderString(sb, ProgressLabel.Value,
                new Vector2(chamberRect.Center.X - labelSize.X * 0.5f, chamberRect.Y - 20), TextDim * alpha, 0.6f);

            IndustrialTerminalRenderer.DrawRecess(sb, chamberRect, alpha, 0.8f);

            //双颚:上颚随进度往复咬合,下颚固定;颚面带齿
            float jawPhase = CruData.IsWorking
                ? MathF.Abs(MathF.Sin(CruData.CrushProgress * (MathHelper.Pi / 15f))) : 0f;
            int jawGapTop = chamberRect.Y + 18 + (int)(jawPhase * 16f);
            int jawBottom = chamberRect.Bottom - 26;

            Color jawCold = new(70, 66, 60);
            Color jawHot = Color.Lerp(jawCold, new Color(150, 128, 92), workDisplay);
            //上颚体
            sb.Draw(px, new Rectangle(chamberRect.X + 16, jawGapTop, chamberRect.Width - 32, 9), src, jawHot * alpha);
            //下颚体
            sb.Draw(px, new Rectangle(chamberRect.X + 16, jawBottom, chamberRect.Width - 32, 9), src, jawCold * alpha);
            //颚齿
            for (int k = 0; k < 5; k++) {
                int toothX = chamberRect.X + 22 + k * 20;
                sb.Draw(px, new Rectangle(toothX, jawGapTop + 9, 5, 4), src, jawHot * (alpha * 0.9f));
                sb.Draw(px, new Rectangle(toothX + 8, jawBottom - 4, 5, 4), src, jawCold * (alpha * 0.9f));
            }
            //咬合闪光:接近闭合时腔心亮一下
            if (jawPhase > 0.82f) {
                SvgPathPen.SoftDot(sb, chamberRect.Center.ToVector2(),
                    chamberRect.Width * 0.24f, new Color(255, 200, 120), alpha * (jawPhase - 0.82f) * 1.6f);
            }

            //进度刻度条
            float progress = CruData.MaxCrushProgress > 0
                ? MathHelper.Clamp(CruData.CrushProgress / (float)CruData.MaxCrushProgress, 0f, 1f) : 0f;
            IndustrialTerminalRenderer.DrawTickBar(sb, progressBarRect, progress, Amber, alpha);
        }

        /// <summary>入料口与出料口:插座语法,出料口走黄铜亮色</summary>
        private void DrawSlots(SpriteBatch sb) {
            float alpha = uiFadeAlpha;

            Vector2 inSize = FontAssets.MouseText.Value.MeasureString(InputLabel.Value) * 0.62f;
            Utils.DrawBorderString(sb, InputLabel.Value,
                new Vector2(inputSlotRect.Center.X - inSize.X * 0.5f, inputSlotRect.Y - 20), TextDim * alpha, 0.62f);
            Vector2 outSize = FontAssets.MouseText.Value.MeasureString(OutputLabel.Value) * 0.62f;
            Utils.DrawBorderString(sb, OutputLabel.Value,
                new Vector2(outputSlotRect.Center.X - outSize.X * 0.5f, outputSlotRect.Y - 20),
                Color.Lerp(TextDim, BrassBright, 0.4f) * alpha, 0.62f);

            IndustrialTerminalRenderer.DrawSocket(sb, inputSlotRect, alpha, hoveringInputSlot ? 1f : 0f, 0f);
            IndustrialTerminalRenderer.DrawSocket(sb, outputSlotRect, alpha, hoveringOutputSlot ? 1f : 0f, 0f);

            DrawSlotItem(sb, CruData.InputItem, inputSlotRect, alpha);
            DrawSlotItem(sb, CruData.OutputItem, outputSlotRect, alpha);
        }

        internal static void DrawSlotItem(SpriteBatch sb, Item item, Rectangle rect, float alpha) {
            if (item == null || item.IsAir) {
                return;
            }
            Main.instance.LoadItem(item.type);
            VaultUtils.SimpleDrawItem(sb, item.type, rect.Center.ToVector2(), 42, 1f, 0, Color.White * alpha);

            if (item.stack > 1) {
                string stackText = item.stack.ToString();
                Vector2 stackSize = FontAssets.ItemStack.Value.MeasureString(stackText);
                Utils.DrawBorderStringFourWay(sb, FontAssets.ItemStack.Value, stackText,
                    rect.Right - stackSize.X * 0.8f - 6, rect.Bottom - stackSize.Y * 0.8f - 6,
                    Color.White * alpha, Color.Black * alpha, new Vector2(0.3f), 0.8f);
            }
        }

        /// <summary>流向箭标:入料口→破碎腔→出料口,工作时逐个点亮流动</summary>
        private void DrawFlowChevrons(SpriteBatch sb) {
            float alpha = uiFadeAlpha;
            bool working = CruData.IsWorking;
            int cy = inputSlotRect.Center.Y;

            void chevron(float x, int index) {
                float lit = working
                    ? MathF.Sin(animTimer * 4.5f - index * 0.9f) * 0.5f + 0.5f
                    : 0.12f;
                Color color = Color.Lerp(TextDim * 0.6f, Amber, lit) * (alpha * (0.35f + lit * 0.55f));
                Vector2 top = new(x, cy - 6);
                Vector2 mid = new(x + 7, cy);
                Vector2 bottom = new(x, cy + 6);
                Texture2D px = VaultAsset.placeholder2.Value;
                Vector2 dirA = mid - top;
                sb.Draw(px, top, new Rectangle(0, 0, 1, 1), color, dirA.ToRotation(),
                    new Vector2(0f, 0.5f), new Vector2(dirA.Length(), 2f), SpriteEffects.None, 0f);
                Vector2 dirB = bottom - mid;
                sb.Draw(px, mid, new Rectangle(0, 0, 1, 1), color, dirB.ToRotation(),
                    new Vector2(0f, 0.5f), new Vector2(dirB.Length(), 2f), SpriteEffects.None, 0f);
            }

            chevron(inputSlotRect.Right + 12, 0);
            chevron(inputSlotRect.Right + 24, 1);
            chevron(chamberRect.Right + 12, 2);
            chevron(chamberRect.Right + 24, 3);
        }

        /// <summary>状态灯(左) + 模块插座行(中) + 电力表盘(右) + 操作提示</summary>
        private void DrawStatusRow(SpriteBatch sb) {
            float alpha = uiFadeAlpha;
            CrusherData data = CruData;
            float x = panelRect.X + 36;
            float y = panelRect.Y + 234;

            string state;
            Color lampColor;
            float lampBright;
            if (data.UEvalue < data.UEPerTick) {
                state = NoPowerText.Value;
                lampColor = WarnRed;
                lampBright = MathF.Sin(animTimer * 5f) * 0.35f + 0.55f;
            }
            else if (data.IsWorking) {
                state = CrushingText.Value;
                lampColor = Amber;
                lampBright = MathF.Sin(animTimer * 2.6f) * 0.2f + 0.72f;
            }
            else {
                state = IdleText.Value;
                lampColor = TextDim;
                lampBright = 0.2f;
            }
            IndustrialTerminalRenderer.DrawLamp(sb, new Vector2(x + 7, y + 9), lampColor, alpha, lampBright);
            Utils.DrawBorderString(sb, state, new Vector2(x + 21, y + 1),
                Color.Lerp(TextMain, lampColor, 0.35f) * alpha, 0.66f);

            //电力表盘:破碎时微颤
            float jitter = data.IsWorking ? MathF.Sin(animTimer * 34f) * 0.006f : 0f;
            float powerRatio = MathHelper.Clamp(data.UEvalue / data.MaxUE, 0f, 1f);
            IndustrialTerminalRenderer.DrawGauge(sb, powerGaugeCenter, 30f, powerDisplay + jitter,
                Amber, alpha, PowerLabel.Value, $"{(int)(powerRatio * 100f)}%");

            //模块插座行
            if (CurrentTP != null) {
                socketStrip.Draw(sb, CurrentTP.ModuleRack, CrusherTP.ModuleSlotCount,
                    alpha, MousePosition.ToPoint());
            }

            //操作提示:底缘呼吸
            string hint = string.Empty;
            if (hoveringInputSlot) {
                hint = InputHint.Value;
            }
            else if (hoveringOutputSlot && data.OutputItem != null && !data.OutputItem.IsAir) {
                hint = OutputHint.Value;
            }
            if (!string.IsNullOrEmpty(hint)) {
                float blink = MathF.Sin(animTimer * 6f) * 0.3f + 0.7f;
                Utils.DrawBorderString(sb, hint,
                    new Vector2(panelRect.X + 36, panelRect.Bottom - 28),
                    Color.Lerp(TextDim, Amber, 0.5f) * (alpha * blink), 0.62f);
            }
        }

        private void DrawHoverTips(SpriteBatch sb) {
            if (isDragging) {
                return;
            }
            if (CurrentTP != null
                && socketStrip.DrawHoverTip(sb, CurrentTP.ModuleRack, CrusherTP.ModuleSlotCount,
                    MousePosition.ToPoint(), (text, color) => ShowTip(sb, text, color))) {
                return;
            }
            if (hoveringPowerGauge) {
                ShowTip(sb, $"{(int)CruData.UEvalue}/{(int)CruData.MaxUE} {PowerUnit.Value}");
            }
        }

        private static void ShowTip(SpriteBatch sb, string text) => ShowTip(sb, text, TextMain);

        internal static void ShowTip(SpriteBatch sb, string text, Color color) {
            Vector2 textSize = FontAssets.MouseText.Value.MeasureString(text) * 0.75f;
            Vector2 pos = new Vector2(Main.mouseX, Main.mouseY) + new Vector2(18, 18);
            //贴屏缘时翻转与钳制
            if (pos.X + textSize.X + 20 > UIScreenW) {
                pos.X = Main.mouseX - textSize.X - 24;
            }
            if (pos.Y + textSize.Y + 12 > UIScreenH) {
                pos.Y = Main.mouseY - textSize.Y - 18;
            }

            Rectangle bg = new((int)pos.X - 9, (int)pos.Y - 5, (int)textSize.X + 18, (int)textSize.Y + 10);
            IndustrialTerminalRenderer.DrawTooltipPlate(sb, bg, 1f);
            Utils.DrawBorderString(sb, text, pos, color, 0.75f);
        }
        #endregion
    }
}
