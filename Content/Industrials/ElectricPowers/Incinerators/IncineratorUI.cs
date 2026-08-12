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

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Incinerators
{
    /// <summary>
    /// 电动焚化炉面板:电熔炉仪器语言——钢壳(随工况发热)、入料口/出料口插座、
    /// 熔炼室电热棒组(通电炭红转亮橙,断电冷却)、流向箭标、电力表盘。<br/>
    /// 交互契约与旧版一致(点击入料/取料、右键开合、超距自动关、位置持久化),
    /// 笔刷与材质在 <see cref="IndustrialTerminalRenderer"/>
    /// </summary>
    internal class IncineratorUI : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "UI.Incinerator";

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

        //关联的TP
        internal IncineratorTP CurrentTP;
        internal bool IsActive;

        //淡入淡出(Active 放宽到淡出结束,收摊有过程)
        private float uiFadeAlpha;
        public override bool Active => IsActive || uiFadeAlpha > 0.01f;

        //机壳热度包络:开工升温、停机冷却,喂 uHeat 与电热棒
        private float heatDisplay;
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

        //炉膛粒子:余烬与灰烬,从熔炼室冒出
        private readonly List<EmberPRT> embers = new();
        private int emberSpawnTimer;
        private readonly List<AshPRT> ashes = new();
        private int ashSpawnTimer;

        private float animTimer;

        private IncineratorData IncData => CurrentTP?.IncData;
        #endregion

        #region 本地化
        protected static LocalizedText TitleText;
        protected static LocalizedText InputLabel;
        protected static LocalizedText OutputLabel;
        protected static LocalizedText ProgressLabel;
        protected static LocalizedText PowerLabel;
        protected static LocalizedText StatusLabel;
        protected static LocalizedText SmeltingText;
        protected static LocalizedText IdleText;
        protected static LocalizedText NoPowerText;
        protected static LocalizedText InputHint;
        protected static LocalizedText OutputHint;
        protected static LocalizedText PowerUnit;

        public override Texture2D Texture => VaultAsset.placeholder2.Value;

        public override void SetStaticDefaults() {
            TitleText = this.GetLocalization(nameof(TitleText), () => "电动焚化炉");
            InputLabel = this.GetLocalization(nameof(InputLabel), () => "输入");
            OutputLabel = this.GetLocalization(nameof(OutputLabel), () => "输出");
            ProgressLabel = this.GetLocalization(nameof(ProgressLabel), () => "进度");
            PowerLabel = this.GetLocalization(nameof(PowerLabel), () => "电力");
            StatusLabel = this.GetLocalization(nameof(StatusLabel), () => "状态:");
            SmeltingText = this.GetLocalization(nameof(SmeltingText), () => "焚烧中");
            IdleText = this.GetLocalization(nameof(IdleText), () => "待机");
            NoPowerText = this.GetLocalization(nameof(NoPowerText), () => "缺电");
            InputHint = this.GetLocalization(nameof(InputHint), () => "放入可焚烧物品");
            OutputHint = this.GetLocalization(nameof(OutputHint), () => "点击取出成品");
            PowerUnit = this.GetLocalization(nameof(PowerUnit), () => "UE");
        }
        #endregion

        public void Interactive(IncineratorTP tp, bool newTP) {
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
                socketStrip.HandleClick(mouse, CurrentTP.ModuleRack, IncineratorTP.ModuleSlotCount,
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
            if (hoveringInputSlot && IncData != null) {
                if (IncData.InputItem != null && !IncData.InputItem.IsAir) {
                    Main.HoverItem = IncData.InputItem.Clone();
                    Main.hoverItemName = IncData.InputItem.Name;
                }

                if (keyLeftPressState == KeyPressState.Pressed) {
                    CurrentTP.HandleInputItem();
                }
            }

            //出料口交互
            if (hoveringOutputSlot && IncData != null) {
                if (IncData.OutputItem != null && !IncData.OutputItem.IsAir) {
                    Main.HoverItem = IncData.OutputItem.Clone();
                    Main.hoverItemName = IncData.OutputItem.Name;
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
                CurrentTP != null ? IncineratorTP.ModuleSlotCount : 0, 40, 8);
        }

        /// <summary>机壳热度包络与电力表指针弹簧</summary>
        private void UpdateEnvelopes() {
            IncineratorData data = IncData;
            bool working = data != null && data.IsWorking;
            //升温快冷却慢,像真炉子
            float heatTarget = working ? 0.85f : 0f;
            heatDisplay = MathHelper.Lerp(heatDisplay, heatTarget, working ? 0.035f : 0.012f);

            float powerTarget = data != null ? MathHelper.Clamp(data.UEvalue / data.MaxUE, 0f, 1f) : 0f;
            powerVel = powerVel * 0.80f + (powerTarget - powerDisplay) * 0.05f;
            powerDisplay += powerVel;
        }

        /// <summary>余烬与灰烬都从熔炼室冒出,有来源感</summary>
        private void UpdateParticles() {
            if (uiFadeAlpha < 0.3f || IncData == null) {
                return;
            }

            bool working = IncData.IsWorking;

            emberSpawnTimer++;
            int emberInterval = working ? 3 : 16;
            if (heatDisplay > 0.05f && emberSpawnTimer >= emberInterval && embers.Count < 30) {
                emberSpawnTimer = 0;
                float xPos = Main.rand.NextFloat(chamberRect.X + 8, chamberRect.Right - 8);
                embers.Add(new EmberPRT(new Vector2(xPos, chamberRect.Bottom - 8)));
            }
            for (int i = embers.Count - 1; i >= 0; i--) {
                if (embers[i].Update()) {
                    embers.RemoveAt(i);
                }
            }

            ashSpawnTimer++;
            if (working && ashSpawnTimer >= 10 && ashes.Count < 18) {
                ashSpawnTimer = 0;
                float xPos = Main.rand.NextFloat(chamberRect.X + 6, chamberRect.Right - 6);
                ashes.Add(new AshPRT(new Vector2(xPos, chamberRect.Y + 12)));
            }
            for (int i = ashes.Count - 1; i >= 0; i--) {
                if (ashes[i].Update()) {
                    ashes.RemoveAt(i);
                }
            }
        }

        public override void OnEnterWorld() => IsActive = false;

        public override void SaveUIData(TagCompound tag) {
            tag["IncineratorUI_DrawPos_X"] = DrawPosition.X;
            tag["IncineratorUI_DrawPos_Y"] = DrawPosition.Y;
        }

        public override void LoadUIData(TagCompound tag) {
            if (tag.TryGet("IncineratorUI_DrawPos_X", out float x)) {
                DrawPosition.X = x;
            }
            else {
                DrawPosition.X = Main.screenWidth / 2;
            }

            if (tag.TryGet("IncineratorUI_DrawPos_Y", out float y)) {
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
            if (IncData == null) {
                return;
            }

            DrawShell(spriteBatch);
            DrawChamber(spriteBatch);

            //粒子夹在熔炼室与料口之间:灰烬沉底,余烬压上
            foreach (AshPRT ash in ashes) {
                ash.Draw(spriteBatch, uiFadeAlpha * 0.55f);
            }
            foreach (EmberPRT ember in embers) {
                ember.Draw(spriteBatch, uiFadeAlpha * 0.9f);
            }

            DrawSlots(spriteBatch);
            DrawFlowChevrons(spriteBatch);
            DrawStatusRow(spriteBatch);
            DrawHoverTips(spriteBatch);
        }

        /// <summary>钢壳(随工况发热) + 铆钉 + 黄铜铭牌 + 闩钮</summary>
        private void DrawShell(SpriteBatch sb) {
            float alpha = uiFadeAlpha;

            IndustrialTerminalRenderer.ShaderPanel(sb, panelRect, alpha, mode: 0, heat: heatDisplay);

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

        /// <summary>熔炼室:凹槽膛体 + 三根电热棒(通电炭红转亮橙) + 进度刻度条</summary>
        private void DrawChamber(SpriteBatch sb) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float alpha = uiFadeAlpha;
            Rectangle src = new(0, 0, 1, 1);

            //膛体标签
            Vector2 labelSize = FontAssets.MouseText.Value.MeasureString(ProgressLabel.Value) * 0.6f;
            Utils.DrawBorderString(sb, ProgressLabel.Value,
                new Vector2(chamberRect.Center.X - labelSize.X * 0.5f, chamberRect.Y - 20), TextDim * alpha, 0.6f);

            IndustrialTerminalRenderer.DrawRecess(sb, chamberRect, alpha, 0.8f);

            //三根电热棒:热度决定颜色,炭黑→炭红→亮橙,各棒相位错开轻微闪变
            for (int i = 0; i < 3; i++) {
                int rodY = chamberRect.Y + 20 + i * 24;
                Rectangle rod = new(chamberRect.X + 12, rodY, chamberRect.Width - 24, 7);
                float shimmer = MathF.Sin(animTimer * 5f + i * 2.1f) * 0.5f + 0.5f;
                float rodHeat = MathHelper.Clamp(heatDisplay * (0.85f + shimmer * 0.15f), 0f, 1f);

                //棒座:两端黄铜端子
                sb.Draw(px, new Rectangle(rod.X - 5, rod.Y - 1, 5, rod.Height + 2), src, Brass * (alpha * 0.8f));
                sb.Draw(px, new Rectangle(rod.Right, rod.Y - 1, 5, rod.Height + 2), src, Brass * (alpha * 0.8f));

                //棒体:冷态是暗铁,热态向炭红/亮橙走
                Color cold = new(38, 32, 28);
                Color hot = Color.Lerp(new Color(150, 42, 20), new Color(255, 150, 60), rodHeat);
                Color rodColor = Color.Lerp(cold, hot, rodHeat);
                sb.Draw(px, rod, src, rodColor * alpha);
                //棒芯亮线:热起来才有
                if (rodHeat > 0.15f) {
                    sb.Draw(px, new Rectangle(rod.X + 2, rod.Center.Y, rod.Width - 4, 1), src,
                        Color.Lerp(hot, Color.White, 0.35f) * (alpha * rodHeat * 0.8f));
                    SvgPathPen.SoftDot(sb, rod.Center.ToVector2(), rod.Width * 0.32f,
                        new Color(255, 120, 45), alpha * rodHeat * 0.10f);
                }
            }

            //进度:膛下刻度条,与掷骰无关的诚实工序读数
            float progress = IncData.MaxSmeltingProgress > 0
                ? MathHelper.Clamp(IncData.SmeltingProgress / (float)IncData.MaxSmeltingProgress, 0f, 1f) : 0f;
            IndustrialTerminalRenderer.DrawTickBar(sb, progressBarRect, progress, Amber, alpha);
        }

        /// <summary>入料口与出料口:插座语法,出料口走黄铜亮色</summary>
        private void DrawSlots(SpriteBatch sb) {
            float alpha = uiFadeAlpha;

            //标签
            Vector2 inSize = FontAssets.MouseText.Value.MeasureString(InputLabel.Value) * 0.62f;
            Utils.DrawBorderString(sb, InputLabel.Value,
                new Vector2(inputSlotRect.Center.X - inSize.X * 0.5f, inputSlotRect.Y - 20), TextDim * alpha, 0.62f);
            Vector2 outSize = FontAssets.MouseText.Value.MeasureString(OutputLabel.Value) * 0.62f;
            Utils.DrawBorderString(sb, OutputLabel.Value,
                new Vector2(outputSlotRect.Center.X - outSize.X * 0.5f, outputSlotRect.Y - 20),
                Color.Lerp(TextDim, BrassBright, 0.4f) * alpha, 0.62f);

            IndustrialTerminalRenderer.DrawSocket(sb, inputSlotRect, alpha, hoveringInputSlot ? 1f : 0f, 0f);
            IndustrialTerminalRenderer.DrawSocket(sb, outputSlotRect, alpha, hoveringOutputSlot ? 1f : 0f, 0f);

            //料口物品
            DrawSlotItem(sb, IncData.InputItem, inputSlotRect, alpha);
            DrawSlotItem(sb, IncData.OutputItem, outputSlotRect, alpha);
        }

        private static void DrawSlotItem(SpriteBatch sb, Item item, Rectangle rect, float alpha) {
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

        /// <summary>流向箭标:入料口→熔炼室→出料口,工作时逐个点亮流动</summary>
        private void DrawFlowChevrons(SpriteBatch sb) {
            float alpha = uiFadeAlpha;
            bool working = IncData.IsWorking;
            int cy = inputSlotRect.Center.Y;

            void chevron(float x, int index) {
                //工作时相位流动,停机时静置暗刻
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

            //入料口 → 熔炼室
            chevron(inputSlotRect.Right + 12, 0);
            chevron(inputSlotRect.Right + 24, 1);
            //熔炼室 → 出料口
            chevron(chamberRect.Right + 12, 2);
            chevron(chamberRect.Right + 24, 3);
        }

        /// <summary>状态灯(左) + 模块插座行(中) + 电力表盘(右) + 操作提示</summary>
        private void DrawStatusRow(SpriteBatch sb) {
            float alpha = uiFadeAlpha;
            IncineratorData data = IncData;
            float x = panelRect.X + 36;
            float y = panelRect.Y + 234;

            //状态灯
            string state;
            Color lampColor;
            float lampBright;
            if (data.UEvalue < data.UEPerTick) {
                state = NoPowerText.Value;
                lampColor = WarnRed;
                lampBright = MathF.Sin(animTimer * 5f) * 0.35f + 0.55f;
            }
            else if (data.IsWorking) {
                state = SmeltingText.Value;
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

            //电力表盘:指针带欠阻尼摆动,焚烧时微颤
            float jitter = data.IsWorking ? MathF.Sin(animTimer * 34f) * 0.006f : 0f;
            float powerRatio = MathHelper.Clamp(data.UEvalue / data.MaxUE, 0f, 1f);
            IndustrialTerminalRenderer.DrawGauge(sb, powerGaugeCenter, 30f, powerDisplay + jitter,
                Amber, alpha, PowerLabel.Value, $"{(int)(powerRatio * 100f)}%");

            //模块插座行
            if (CurrentTP != null) {
                socketStrip.Draw(sb, CurrentTP.ModuleRack, IncineratorTP.ModuleSlotCount,
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
                && socketStrip.DrawHoverTip(sb, CurrentTP.ModuleRack, IncineratorTP.ModuleSlotCount,
                    MousePosition.ToPoint(), (text, color) => ShowTip(sb, text, color))) {
                return;
            }
            if (hoveringPowerGauge) {
                ShowTip(sb, $"{(int)IncData.UEvalue}/{(int)IncData.MaxUE} {PowerUnit.Value}");
            }
        }

        private static void ShowTip(SpriteBatch sb, string text) => ShowTip(sb, text, TextMain);

        private static void ShowTip(SpriteBatch sb, string text, Color color) {
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
