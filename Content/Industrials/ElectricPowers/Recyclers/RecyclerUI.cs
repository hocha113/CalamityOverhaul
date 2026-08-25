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

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Recyclers
{
    /// <summary>
    /// 回收机面板:拆解仪器语言,钢壳、装备口/出锭口插座、拆解台(扫描线+预估锭读数)、
    /// 进度刻度条、模块插座行、电力表盘。交互契约与焚化炉面板一致
    /// </summary>
    internal class RecyclerUI : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "Items";

        #region 布局与状态
        private const float PanelWidth = 420f;
        private const float PanelHeight = 332f;

        private static Color TextMain => IndustrialTerminalRenderer.TextMain;
        private static Color TextDim => IndustrialTerminalRenderer.TextDim;
        private static Color Amber => IndustrialTerminalRenderer.Amber;
        private static Color WarnRed => IndustrialTerminalRenderer.WarnRed;
        private static Color OkGreen => IndustrialTerminalRenderer.OkGreen;
        private static Color BrassBright => IndustrialTerminalRenderer.BrassBright;

        private static float UIScreenW => PlayerInput.RealScreenWidth / Main.UIScale;
        private static float UIScreenH => PlayerInput.RealScreenHeight / Main.UIScale;

        internal RecyclerTP CurrentTP;
        internal bool IsActive;

        //淡入淡出(Active 放宽到淡出结束,收摊有过程)
        private float uiFadeAlpha;
        public override bool Active => IsActive || uiFadeAlpha > 0.01f;

        //工况包络
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

        //拆解火花
        private readonly List<EmberPRT> sparks = new();
        private int sparkSpawnTimer;

        private float animTimer;

        private RecyclerData RecData => CurrentTP?.RecData;
        #endregion

        #region 本地化
        protected static LocalizedText TitleText;
        protected static LocalizedText InputLabel;
        protected static LocalizedText OutputLabel;
        protected static LocalizedText ProgressLabel;
        protected static LocalizedText PowerLabel;
        protected static LocalizedText WorkingText;
        protected static LocalizedText IdleText;
        protected static LocalizedText NoPowerText;
        protected static LocalizedText InputHint;
        protected static LocalizedText OutputHint;
        protected static LocalizedText EstimateLabel;
        protected static LocalizedText PowerUnit;

        public override Texture2D Texture => VaultAsset.placeholder2.Value;

        public override void SetStaticDefaults() {
            TitleText = this.GetLocalization(nameof(TitleText), () => "回收机");
            InputLabel = this.GetLocalization(nameof(InputLabel), () => "装备");
            OutputLabel = this.GetLocalization(nameof(OutputLabel), () => "锭料");
            ProgressLabel = this.GetLocalization(nameof(ProgressLabel), () => "拆解");
            PowerLabel = this.GetLocalization(nameof(PowerLabel), () => "电力");
            WorkingText = this.GetLocalization(nameof(WorkingText), () => "拆解中");
            IdleText = this.GetLocalization(nameof(IdleText), () => "待机");
            NoPowerText = this.GetLocalization(nameof(NoPowerText), () => "缺电");
            InputHint = this.GetLocalization(nameof(InputHint), () => "放入武器、盔甲或饰品");
            OutputHint = this.GetLocalization(nameof(OutputHint), () => "点击取出锭料");
            EstimateLabel = this.GetLocalization(nameof(EstimateLabel), () => "预估产出:");
            PowerUnit = this.GetLocalization(nameof(PowerUnit), () => "UE");
        }
        #endregion

        public void Interactive(RecyclerTP tp, bool newTP) {
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
                socketStrip.HandleClick(mouse, CurrentTP.ModuleRack, RecyclerTP.ModuleSlotCount,
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

            //装备口交互
            if (hoveringInputSlot && RecData != null) {
                if (RecData.InputItem != null && !RecData.InputItem.IsAir) {
                    Main.HoverItem = RecData.InputItem.Clone();
                    Main.hoverItemName = RecData.InputItem.Name;
                }

                if (keyLeftPressState == KeyPressState.Pressed) {
                    CurrentTP.HandleInputItem();
                }
            }

            //出锭口交互
            if (hoveringOutputSlot && RecData != null) {
                if (RecData.OutputItem != null && !RecData.OutputItem.IsAir) {
                    Main.HoverItem = RecData.OutputItem.Clone();
                    Main.hoverItemName = RecData.OutputItem.Name;
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
            socketStrip.Layout(panelRect.X + 150, panelRect.Y + 222,
                CurrentTP != null ? RecyclerTP.ModuleSlotCount : 0, 40, 8);
        }

        /// <summary>工况包络与电力表指针弹簧</summary>
        private void UpdateEnvelopes() {
            RecyclerData data = RecData;
            bool working = data != null && data.IsWorking;
            workDisplay = MathHelper.Lerp(workDisplay, working ? 1f : 0f, working ? 0.06f : 0.03f);

            float powerTarget = data != null ? MathHelper.Clamp(data.UEvalue / data.MaxUE, 0f, 1f) : 0f;
            powerVel = powerVel * 0.80f + (powerTarget - powerDisplay) * 0.05f;
            powerDisplay += powerVel;
        }

        /// <summary>拆解火花从台面溅出</summary>
        private void UpdateParticles() {
            if (uiFadeAlpha < 0.3f || RecData == null) {
                return;
            }

            bool working = RecData.IsWorking;
            sparkSpawnTimer++;
            if (working && sparkSpawnTimer >= 5 && sparks.Count < 26) {
                sparkSpawnTimer = 0;
                float xPos = Main.rand.NextFloat(chamberRect.X + 14, chamberRect.Right - 14);
                sparks.Add(new EmberPRT(new Vector2(xPos, chamberRect.Center.Y + 10)));
            }
            for (int i = sparks.Count - 1; i >= 0; i--) {
                if (sparks[i].Update()) {
                    sparks.RemoveAt(i);
                }
            }
        }

        public override void OnEnterWorld() => IsActive = false;

        public override void SaveUIData(TagCompound tag) {
            tag["RecyclerUI_DrawPos_X"] = DrawPosition.X;
            tag["RecyclerUI_DrawPos_Y"] = DrawPosition.Y;
        }

        public override void LoadUIData(TagCompound tag) {
            if (tag.TryGet("RecyclerUI_DrawPos_X", out float x)) {
                DrawPosition.X = x;
            }
            else {
                DrawPosition.X = Main.screenWidth / 2;
            }

            if (tag.TryGet("RecyclerUI_DrawPos_Y", out float y)) {
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
            if (RecData == null) {
                return;
            }

            DrawShell(spriteBatch);
            DrawChamber(spriteBatch);

            foreach (EmberPRT spark in sparks) {
                spark.Draw(spriteBatch, uiFadeAlpha * 0.8f);
            }

            DrawSlots(spriteBatch);
            DrawStatusRow(spriteBatch);
            DrawHoverTips(spriteBatch);
        }

        /// <summary>钢壳 + 铆钉 + 黄铜铭牌 + 闩钮</summary>
        private void DrawShell(SpriteBatch sb) {
            float alpha = uiFadeAlpha;

            IndustrialTerminalRenderer.ShaderPanel(sb, panelRect, alpha, mode: 0, heat: workDisplay * 0.18f);

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

        /// <summary>拆解台:凹槽膛体 + 装备剪影 + 扫描线 + 预估锭读数 + 进度刻度条</summary>
        private void DrawChamber(SpriteBatch sb) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float alpha = uiFadeAlpha;
            Rectangle src = new(0, 0, 1, 1);

            Vector2 labelSize = FontAssets.MouseText.Value.MeasureString(ProgressLabel.Value) * 0.6f;
            Utils.DrawBorderString(sb, ProgressLabel.Value,
                new Vector2(chamberRect.Center.X - labelSize.X * 0.5f, chamberRect.Y - 20), TextDim * alpha, 0.6f);

            IndustrialTerminalRenderer.DrawRecess(sb, chamberRect, alpha, 0.8f);

            //台面上的待拆装备
            bool hasInput = RecData.InputItem != null && !RecData.InputItem.IsAir;
            if (hasInput) {
                Main.instance.LoadItem(RecData.InputItem.type);
                VaultUtils.SimpleDrawItem(sb, RecData.InputItem.type,
                    chamberRect.Center.ToVector2() - new Vector2(0, 8), 36, 1f, 0, Color.White * (alpha * 0.9f));
            }

            //扫描线:工作时上下往复,拆解在读装备
            if (workDisplay > 0.05f) {
                float scanPhase = MathF.Sin(animTimer * 3.4f) * 0.5f + 0.5f;
                int scanY = chamberRect.Y + 10 + (int)(scanPhase * (chamberRect.Height - 26));
                sb.Draw(px, new Rectangle(chamberRect.X + 10, scanY, chamberRect.Width - 20, 1), src,
                    OkGreen * (alpha * workDisplay * 0.8f));
                sb.Draw(px, new Rectangle(chamberRect.X + 10, scanY + 1, chamberRect.Width - 20, 2), src,
                    OkGreen * (alpha * workDisplay * 0.25f));
            }

            //预估锭读数:装备在位时显示确定性锭种与基础数量
            if (hasInput) {
                (int barType, int baseCount) = RecyclerTables.ResolveByRarity(RecData.InputItem.rare);
                Utils.DrawBorderString(sb, EstimateLabel.Value,
                    new Vector2(chamberRect.X + 4, chamberRect.Bottom + 4), TextDim * alpha, 0.58f);
                Main.instance.LoadItem(barType);
                VaultUtils.SimpleDrawItem(sb, barType,
                    new Vector2(chamberRect.X + 86, chamberRect.Bottom + 11), 18, 1f, 0, Color.White * alpha);
                Utils.DrawBorderString(sb, $"x{baseCount}",
                    new Vector2(chamberRect.X + 98, chamberRect.Bottom + 4),
                    Color.Lerp(TextDim, BrassBright, 0.5f) * alpha, 0.58f);
            }

            //进度刻度条
            float progress = RecData.MaxRecycleProgress > 0
                ? MathHelper.Clamp(RecData.RecycleProgress / (float)RecData.MaxRecycleProgress, 0f, 1f) : 0f;
            IndustrialTerminalRenderer.DrawTickBar(sb, progressBarRect, progress, OkGreen, alpha);
        }

        /// <summary>装备口与出锭口:插座语法</summary>
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

            Crushers.CrusherUI.DrawSlotItem(sb, RecData.InputItem, inputSlotRect, alpha);
            Crushers.CrusherUI.DrawSlotItem(sb, RecData.OutputItem, outputSlotRect, alpha);
        }

        /// <summary>状态灯(左) + 模块插座行(中) + 电力表盘(右) + 操作提示</summary>
        private void DrawStatusRow(SpriteBatch sb) {
            float alpha = uiFadeAlpha;
            RecyclerData data = RecData;
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
                state = WorkingText.Value;
                lampColor = OkGreen;
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

            //电力表盘
            float jitter = data.IsWorking ? MathF.Sin(animTimer * 34f) * 0.006f : 0f;
            float powerRatio = MathHelper.Clamp(data.UEvalue / data.MaxUE, 0f, 1f);
            IndustrialTerminalRenderer.DrawGauge(sb, powerGaugeCenter, 30f, powerDisplay + jitter,
                Amber, alpha, PowerLabel.Value, $"{(int)(powerRatio * 100f)}%");

            //模块插座行
            if (CurrentTP != null) {
                socketStrip.Draw(sb, CurrentTP.ModuleRack, RecyclerTP.ModuleSlotCount,
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
                && socketStrip.DrawHoverTip(sb, CurrentTP.ModuleRack, RecyclerTP.ModuleSlotCount,
                    MousePosition.ToPoint(), (text, color) => Crushers.CrusherUI.ShowTip(sb, text, color))) {
                return;
            }
            if (hoveringPowerGauge) {
                Crushers.CrusherUI.ShowTip(sb, $"{(int)RecData.UEvalue}/{(int)RecData.MaxUE} {PowerUnit.Value}", TextMain);
            }
        }
        #endregion
    }
}
