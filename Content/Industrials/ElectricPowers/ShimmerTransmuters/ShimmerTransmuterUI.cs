using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Fluids;
using CalamityOverhaul.Content.Industrials.UIs;
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

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.ShimmerTransmuters
{
    /// <summary>
    /// 微光转化槽面板:钢壳仪器语言 + 微光紫点缀。
    /// 左入料口,中转化室(微光液面 + 进度刻度),右四格出料口,
    /// 底行状态灯/微光液位条/电力表盘。
    /// 交互契约与焚化炉一致(点击入料/取料、闩钮关闭、超距自动关、位置持久化)
    /// </summary>
    internal class ShimmerTransmuterUI : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "UI.ShimmerTransmuter";

        #region 布局与状态
        private const float PanelWidth = 440f;
        private const float PanelHeight = 300f;

        private static Color TextMain => IndustrialTerminalRenderer.TextMain;
        private static Color TextDim => IndustrialTerminalRenderer.TextDim;
        private static Color WarnRed => IndustrialTerminalRenderer.WarnRed;
        private static Color Brass => IndustrialTerminalRenderer.Brass;
        private static Color BrassBright => IndustrialTerminalRenderer.BrassBright;
        /// <summary>微光主题紫,与 FluidHelper 的微光液色一致</summary>
        private static readonly Color ShimmerViolet = new(200, 120, 255);

        private static float UIScreenW => PlayerInput.RealScreenWidth / Main.UIScale;
        private static float UIScreenH => PlayerInput.RealScreenHeight / Main.UIScale;

        internal ShimmerTransmuterTP CurrentTP;
        internal bool IsActive;

        //淡入淡出(Active 放宽到淡出结束,收摊有过程)
        private float uiFadeAlpha;
        public override bool Active => IsActive || uiFadeAlpha > 0.01f;

        //电力表指针弹簧
        private float powerDisplay;
        private float powerVel;
        //微光液面显示插值
        private float fluidDisplay;
        private float latchHover;

        //拖拽
        private bool isDragging;
        private Vector2 dragOffset;
        private bool positionInitialized;

        //布局矩形
        private Rectangle panelRect;
        private Rectangle closeRect;
        private Rectangle inputSlotRect;
        private readonly Rectangle[] outputSlotRects = new Rectangle[ShimmerTransmuterTP.OutputSlotCount];
        private Rectangle chamberRect;
        private Rectangle progressBarRect;
        private Rectangle fluidBarRect;
        private Vector2 powerGaugeCenter;
        private Rectangle powerGaugeRect;
        private bool hoveringInputSlot;
        private int hoveringOutputSlot = -1;
        private bool hoveringPowerGauge;
        private bool hoveringFluidBar;

        //转化室微光火花(纯UI粒子)
        private readonly List<ShimmerMote> motes = new();
        private int moteSpawnTimer;

        private float animTimer;
        #endregion

        #region 本地化
        protected static LocalizedText TitleText;
        protected static LocalizedText InputLabel;
        protected static LocalizedText OutputLabel;
        protected static LocalizedText ProgressLabel;
        protected static LocalizedText PowerLabel;
        protected static LocalizedText ShimmerLabel;
        protected static LocalizedText WorkingText;
        protected static LocalizedText IdleText;
        protected static LocalizedText NoPowerText;
        protected static LocalizedText NoShimmerText;
        protected static LocalizedText OutputFullText;
        protected static LocalizedText InputHint;
        protected static LocalizedText OutputHint;
        protected static LocalizedText PowerUnit;

        public override Texture2D Texture => VaultAsset.placeholder2.Value;

        public override void SetStaticDefaults() {
            TitleText = this.GetLocalization(nameof(TitleText), () => "微光转化槽");
            InputLabel = this.GetLocalization(nameof(InputLabel), () => "输入");
            OutputLabel = this.GetLocalization(nameof(OutputLabel), () => "输出");
            ProgressLabel = this.GetLocalization(nameof(ProgressLabel), () => "转化进度");
            PowerLabel = this.GetLocalization(nameof(PowerLabel), () => "电力");
            ShimmerLabel = this.GetLocalization(nameof(ShimmerLabel), () => "微光");
            WorkingText = this.GetLocalization(nameof(WorkingText), () => "转化中");
            IdleText = this.GetLocalization(nameof(IdleText), () => "待机");
            NoPowerText = this.GetLocalization(nameof(NoPowerText), () => "缺电");
            NoShimmerText = this.GetLocalization(nameof(NoShimmerText), () => "缺微光");
            OutputFullText = this.GetLocalization(nameof(OutputFullText), () => "输出已满");
            InputHint = this.GetLocalization(nameof(InputHint), () => "放入可被微光转化的物品");
            OutputHint = this.GetLocalization(nameof(OutputHint), () => "点击取出产物");
            PowerUnit = this.GetLocalization(nameof(PowerUnit), () => "UE");
        }
        #endregion

        public void Interactive(ShimmerTransmuterTP tp, bool newTP) {
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

            Point mouse = MousePosition.ToPoint();
            hoveringInputSlot = inputSlotRect.Contains(mouse) && !isDragging;
            hoveringOutputSlot = -1;
            for (int i = 0; i < outputSlotRects.Length; i++) {
                if (outputSlotRects[i].Contains(mouse) && !isDragging) {
                    hoveringOutputSlot = i;
                    break;
                }
            }
            hoveringPowerGauge = powerGaugeRect.Contains(mouse) && !isDragging;
            hoveringFluidBar = fluidBarRect.Contains(mouse) && !isDragging;
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

            //背景区拖拽,避开料口/表盘/闩钮
            if (keyLeftPressState == KeyPressState.Pressed && hoverInMainPage
                && !hoveringInputSlot && hoveringOutputSlot < 0 && !hoveringPowerGauge && !hoveringFluidBar
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
            if (hoveringInputSlot && CurrentTP != null) {
                Item input = CurrentTP.InputItem;
                if (input != null && !input.IsAir) {
                    Main.HoverItem = input.Clone();
                    Main.hoverItemName = input.Name;
                }
                if (keyLeftPressState == KeyPressState.Pressed) {
                    CurrentTP.HandleInputItem();
                }
            }

            //出料口交互
            if (hoveringOutputSlot >= 0 && CurrentTP != null) {
                Item output = CurrentTP.OutputItems[hoveringOutputSlot];
                if (output != null && !output.IsAir) {
                    Main.HoverItem = output.Clone();
                    Main.hoverItemName = output.Name;
                }
                if (keyLeftPressState == KeyPressState.Pressed) {
                    CurrentTP.HandleOutputItem(hoveringOutputSlot);
                }
            }

            UpdateMotes();
        }

        private void ComputeLayout() {
            Vector2 topLeft = DrawPosition - new Vector2(PanelWidth / 2, PanelHeight / 2);
            panelRect = new Rectangle((int)topLeft.X, (int)topLeft.Y, (int)PanelWidth, (int)PanelHeight);
            closeRect = new Rectangle(panelRect.Right - 38, panelRect.Y + 9, 26, 26);
            inputSlotRect = new Rectangle(panelRect.X + 34, panelRect.Y + 96, 64, 64);
            chamberRect = new Rectangle(panelRect.X + 132, panelRect.Y + 76, 150, 100);
            progressBarRect = new Rectangle(panelRect.X + 132, panelRect.Y + 186, 150, 8);
            //出料口 2×2
            int outBaseX = panelRect.X + 314;
            int outBaseY = panelRect.Y + 78;
            for (int i = 0; i < outputSlotRects.Length; i++) {
                int col = i % 2;
                int row = i / 2;
                outputSlotRects[i] = new Rectangle(outBaseX + col * 56, outBaseY + row * 56, 48, 48);
            }
            //底行:状态灯(左)+微光液位条(中)+电力表(右)
            fluidBarRect = new Rectangle(panelRect.X + 140, panelRect.Y + 236, 140, 10);
            powerGaugeCenter = new Vector2(panelRect.X + 368, panelRect.Y + 240);
            powerGaugeRect = new Rectangle((int)powerGaugeCenter.X - 32, (int)powerGaugeCenter.Y - 32, 64, 64);
        }

        /// <summary>电力表指针弹簧与微光液面插值</summary>
        private void UpdateEnvelopes() {
            float powerTarget = 0f;
            float fluidTarget = 0f;
            if (CurrentTP != null) {
                powerTarget = MathHelper.Clamp(CurrentTP.MachineData.UEvalue / CurrentTP.MaxUEValue, 0f, 1f);
                fluidTarget = MathHelper.Clamp(CurrentTP.FluidAmount / (float)CurrentTP.FluidCapacity, 0f, 1f);
            }
            powerVel = powerVel * 0.80f + (powerTarget - powerDisplay) * 0.05f;
            powerDisplay += powerVel;
            fluidDisplay = MathHelper.Lerp(fluidDisplay, fluidTarget, 0.08f);
        }

        /// <summary>转化室微光火花:工作时密集上浮,待机时零星</summary>
        private void UpdateMotes() {
            if (uiFadeAlpha < 0.3f || CurrentTP == null) {
                return;
            }

            bool working = CurrentTP.IsWorking;
            moteSpawnTimer++;
            int interval = working ? 3 : 20;
            if (fluidDisplay > 0.02f && moteSpawnTimer >= interval && motes.Count < 40) {
                moteSpawnTimer = 0;
                //从液面附近冒出
                float surfaceY = chamberRect.Bottom - 8 - (chamberRect.Height - 16) * fluidDisplay;
                float xPos = Main.rand.NextFloat(chamberRect.X + 10, chamberRect.Right - 10);
                motes.Add(new ShimmerMote(new Vector2(xPos, surfaceY)));
            }
            for (int i = motes.Count - 1; i >= 0; i--) {
                if (motes[i].Update()) {
                    motes.RemoveAt(i);
                }
            }
        }

        public override void OnEnterWorld() => IsActive = false;

        public override void SaveUIData(TagCompound tag) {
            tag["ShimmerTransmuterUI_DrawPos_X"] = DrawPosition.X;
            tag["ShimmerTransmuterUI_DrawPos_Y"] = DrawPosition.Y;
        }

        public override void LoadUIData(TagCompound tag) {
            if (tag.TryGet("ShimmerTransmuterUI_DrawPos_X", out float x)) {
                DrawPosition.X = x;
            }
            else {
                DrawPosition.X = Main.screenWidth / 2;
            }

            if (tag.TryGet("ShimmerTransmuterUI_DrawPos_Y", out float y)) {
                DrawPosition.Y = y;
            }
            else {
                DrawPosition.Y = Main.screenHeight / 2;
            }
        }

        #region 绘制
        public override void Draw(SpriteBatch spriteBatch) {
            if (uiFadeAlpha < 0.01f || CurrentTP == null) {
                return;
            }

            DrawShell(spriteBatch);
            DrawChamber(spriteBatch);

            foreach (ShimmerMote mote in motes) {
                mote.Draw(spriteBatch, uiFadeAlpha);
            }

            DrawSlots(spriteBatch);
            DrawStatusRow(spriteBatch);
            DrawHoverTips(spriteBatch);
        }

        /// <summary>钢壳 + 铆钉 + 黄铜铭牌 + 闩钮</summary>
        private void DrawShell(SpriteBatch sb) {
            float alpha = uiFadeAlpha;

            IndustrialTerminalRenderer.ShaderPanel(sb, panelRect, alpha, mode: 0);

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

        /// <summary>转化室:凹槽膛体 + 微光液面(波动) + 进度刻度条</summary>
        private void DrawChamber(SpriteBatch sb) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float alpha = uiFadeAlpha;
            Rectangle src = new(0, 0, 1, 1);

            Vector2 labelSize = FontAssets.MouseText.Value.MeasureString(ProgressLabel.Value) * 0.6f;
            Utils.DrawBorderString(sb, ProgressLabel.Value,
                new Vector2(chamberRect.Center.X - labelSize.X * 0.5f, chamberRect.Y - 20), TextDim * alpha, 0.6f);

            IndustrialTerminalRenderer.DrawRecess(sb, chamberRect, alpha, 0.8f);

            //微光液体:底部向上填充,液面正弦波动,体色紫、面口亮
            if (fluidDisplay > 0.01f) {
                int innerH = chamberRect.Height - 16;
                int fillH = (int)(innerH * fluidDisplay);
                Rectangle body = new(chamberRect.X + 8, chamberRect.Bottom - 8 - fillH, chamberRect.Width - 16, fillH);
                sb.Draw(px, body, src, ShimmerViolet * (alpha * 0.30f));
                //液面横线:分段波动,微光的不安分
                int segments = 12;
                float segW = body.Width / (float)segments;
                for (int i = 0; i < segments; i++) {
                    float wave = MathF.Sin(animTimer * 3.2f + i * 0.9f) * 2f;
                    Rectangle seg = new((int)(body.X + i * segW), (int)(body.Y + wave), (int)segW + 1, 2);
                    sb.Draw(px, seg, src, Color.Lerp(ShimmerViolet, Color.White, 0.45f) * (alpha * 0.7f));
                }
            }

            //转化中的输入物品:悬在液面上方,随进度下沉渐隐
            Item input = CurrentTP.InputItem;
            if (input != null && !input.IsAir && CurrentTP.IsWorking) {
                float t = CurrentTP.Progress / (float)ShimmerTransmuterTP.BeatTicks;
                float sink = 14f * t;
                float fade = 1f - t * 0.55f;
                Vector2 itemPos = new(chamberRect.Center.X, chamberRect.Center.Y - 12 + sink);
                Main.instance.LoadItem(input.type);
                VaultUtils.SimpleDrawItem(sb, input.type, itemPos, 34, 1f, 0, Color.White * (alpha * fade));
            }

            //进度刻度条:微光紫
            float progress = MathHelper.Clamp(CurrentTP.Progress / (float)ShimmerTransmuterTP.BeatTicks, 0f, 1f);
            IndustrialTerminalRenderer.DrawTickBar(sb, progressBarRect, progress, ShimmerViolet, alpha);
        }

        /// <summary>入料口与出料口:插座语法,出料口走黄铜亮色</summary>
        private void DrawSlots(SpriteBatch sb) {
            float alpha = uiFadeAlpha;

            Vector2 inSize = FontAssets.MouseText.Value.MeasureString(InputLabel.Value) * 0.62f;
            Utils.DrawBorderString(sb, InputLabel.Value,
                new Vector2(inputSlotRect.Center.X - inSize.X * 0.5f, inputSlotRect.Y - 20), TextDim * alpha, 0.62f);
            Vector2 outSize = FontAssets.MouseText.Value.MeasureString(OutputLabel.Value) * 0.62f;
            float outCenterX = (outputSlotRects[0].X + outputSlotRects[1].Right) * 0.5f;
            Utils.DrawBorderString(sb, OutputLabel.Value,
                new Vector2(outCenterX - outSize.X * 0.5f, outputSlotRects[0].Y - 20),
                Color.Lerp(TextDim, BrassBright, 0.4f) * alpha, 0.62f);

            IndustrialTerminalRenderer.DrawSocket(sb, inputSlotRect, alpha, hoveringInputSlot ? 1f : 0f, 0f);
            DrawSlotItem(sb, CurrentTP.InputItem, inputSlotRect, alpha, 42);

            for (int i = 0; i < outputSlotRects.Length; i++) {
                IndustrialTerminalRenderer.DrawSocket(sb, outputSlotRects[i], alpha, hoveringOutputSlot == i ? 1f : 0f, 0f);
                DrawSlotItem(sb, CurrentTP.OutputItems[i], outputSlotRects[i], alpha, 32);
            }
        }

        private static void DrawSlotItem(SpriteBatch sb, Item item, Rectangle rect, float alpha, int drawSize) {
            if (item == null || item.IsAir) {
                return;
            }
            Main.instance.LoadItem(item.type);
            VaultUtils.SimpleDrawItem(sb, item.type, rect.Center.ToVector2(), drawSize, 1f, 0, Color.White * alpha);

            if (item.stack > 1) {
                string stackText = item.stack.ToString();
                Vector2 stackSize = FontAssets.ItemStack.Value.MeasureString(stackText);
                Utils.DrawBorderStringFourWay(sb, FontAssets.ItemStack.Value, stackText,
                    rect.Right - stackSize.X * 0.8f - 6, rect.Bottom - stackSize.Y * 0.8f - 6,
                    Color.White * alpha, Color.Black * alpha, new Vector2(0.3f), 0.8f);
            }
        }

        /// <summary>状态灯(左) + 微光液位条(中) + 电力表盘(右) + 操作提示</summary>
        private void DrawStatusRow(SpriteBatch sb) {
            float alpha = uiFadeAlpha;
            var tp = CurrentTP;
            float x = panelRect.X + 34;
            float y = panelRect.Y + 232;

            //状态灯:阻塞原因按优先级点名
            string state;
            Color lampColor;
            float lampBright;
            bool hasInput = tp.InputItem != null && !tp.InputItem.IsAir;
            if (tp.IsWorking) {
                state = WorkingText.Value;
                lampColor = ShimmerViolet;
                lampBright = MathF.Sin(animTimer * 2.6f) * 0.2f + 0.72f;
            }
            else if (hasInput && tp.MachineData.UEvalue < ShimmerTransmuterTP.JobCostUE) {
                state = NoPowerText.Value;
                lampColor = WarnRed;
                lampBright = MathF.Sin(animTimer * 5f) * 0.35f + 0.55f;
            }
            else if (hasInput && tp.FluidAmount < ShimmerTransmuterTP.ShimmerPerJob) {
                state = NoShimmerText.Value;
                lampColor = WarnRed;
                lampBright = MathF.Sin(animTimer * 5f) * 0.35f + 0.55f;
            }
            else if (hasInput && !tp.CanRunJob()) {
                state = OutputFullText.Value;
                lampColor = IndustrialTerminalRenderer.Amber;
                lampBright = MathF.Sin(animTimer * 4f) * 0.25f + 0.6f;
            }
            else {
                state = IdleText.Value;
                lampColor = TextDim;
                lampBright = 0.2f;
            }
            IndustrialTerminalRenderer.DrawLamp(sb, new Vector2(x + 7, y + 9), lampColor, alpha, lampBright);
            Utils.DrawBorderString(sb, state, new Vector2(x + 21, y + 1),
                Color.Lerp(TextMain, lampColor, 0.35f) * alpha, 0.66f);

            //微光液位条
            Vector2 fluidLabelSize = FontAssets.MouseText.Value.MeasureString(ShimmerLabel.Value) * 0.56f;
            Utils.DrawBorderString(sb, ShimmerLabel.Value,
                new Vector2(fluidBarRect.Center.X - fluidLabelSize.X * 0.5f, fluidBarRect.Y - 17), TextDim * alpha, 0.56f);
            IndustrialTerminalRenderer.DrawTickBar(sb, fluidBarRect, fluidDisplay, ShimmerViolet, alpha);

            //电力表盘
            float powerRatio = MathHelper.Clamp(tp.MachineData.UEvalue / tp.MaxUEValue, 0f, 1f);
            float jitter = tp.IsWorking ? MathF.Sin(animTimer * 34f) * 0.006f : 0f;
            IndustrialTerminalRenderer.DrawGauge(sb, powerGaugeCenter, 30f, powerDisplay + jitter,
                IndustrialTerminalRenderer.Amber, alpha, PowerLabel.Value, $"{(int)(powerRatio * 100f)}%");

            //操作提示:底缘呼吸
            string hint = string.Empty;
            if (hoveringInputSlot) {
                hint = InputHint.Value;
            }
            else if (hoveringOutputSlot >= 0) {
                Item output = tp.OutputItems[hoveringOutputSlot];
                if (output != null && !output.IsAir) {
                    hint = OutputHint.Value;
                }
            }
            if (!string.IsNullOrEmpty(hint)) {
                float blink = MathF.Sin(animTimer * 6f) * 0.3f + 0.7f;
                Utils.DrawBorderString(sb, hint,
                    new Vector2(panelRect.X + 34, panelRect.Bottom - 26),
                    Color.Lerp(TextDim, ShimmerViolet, 0.5f) * (alpha * blink), 0.62f);
            }
        }

        private void DrawHoverTips(SpriteBatch sb) {
            if (isDragging || CurrentTP == null) {
                return;
            }
            if (hoveringPowerGauge) {
                ShowTip(sb, $"{(int)CurrentTP.MachineData.UEvalue}/{(int)CurrentTP.MaxUEValue} {PowerUnit.Value}");
            }
            else if (hoveringFluidBar) {
                ShowTip(sb, FluidText.BarFormat.Format(FluidHelper.GetName(LiquidID.Shimmer),
                    CurrentTP.FluidAmount, CurrentTP.FluidCapacity));
            }
        }

        private static void ShowTip(SpriteBatch sb, string text) {
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
            Utils.DrawBorderString(sb, text, pos, TextMain, 0.75f);
        }
        #endregion

        /// <summary>转化室微光火花:紫白色小点,上浮渐隐,轻微横向摇摆</summary>
        private sealed class ShimmerMote
        {
            private Vector2 pos;
            private readonly float driftPhase;
            private readonly float riseSpeed;
            private float life;
            private const float MaxLife = 50f;

            public ShimmerMote(Vector2 spawn) {
                pos = spawn;
                driftPhase = Main.rand.NextFloat(MathHelper.TwoPi);
                riseSpeed = Main.rand.NextFloat(0.4f, 0.9f);
            }

            /// <summary>推进一帧,返回 true 表示寿命耗尽</summary>
            public bool Update() {
                life += 1f;
                pos.Y -= riseSpeed;
                pos.X += MathF.Sin(life * 0.12f + driftPhase) * 0.35f;
                return life >= MaxLife;
            }

            public void Draw(SpriteBatch sb, float alpha) {
                float lifeRatio = 1f - life / MaxLife;
                Color color = Color.Lerp(ShimmerViolet, Color.White, lifeRatio * 0.5f) * (alpha * lifeRatio * 0.85f);
                sb.Draw(VaultAsset.placeholder2.Value, new Rectangle((int)pos.X, (int)pos.Y, 2, 2),
                    new Rectangle(0, 0, 1, 1), color);
            }
        }
    }
}
