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

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.AutoFishers
{
    /// <summary>
    /// 自动钓鱼机面板:鱼饵仓+渔获仓+垂钓间隔+状态灯+电力表盘,
    /// 笔刷与材质在 <see cref="IndustrialTerminalRenderer"/>
    /// </summary>
    internal class AutoFisherUI : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "UI.AutoFisher";

        #region 布局与状态
        private const float PanelWidth = 500f;
        private const float PanelHeight = 312f;
        private const int SlotSize = 44;
        private const int SlotGap = 10;

        private static Color TextMain => IndustrialTerminalRenderer.TextMain;
        private static Color TextDim => IndustrialTerminalRenderer.TextDim;
        private static Color WarnRed => IndustrialTerminalRenderer.WarnRed;
        private static Color Accent => AutoFisher.Tint;

        private static float UIScreenW => PlayerInput.RealScreenWidth / Main.UIScale;
        private static float UIScreenH => PlayerInput.RealScreenHeight / Main.UIScale;

        public static AutoFisherUI Instance => UIHandleLoader.GetUIHandleOfType<AutoFisherUI>();

        internal AutoFisherTP Station;
        internal bool IsActive;

        private float uiFadeAlpha;
        public override bool Active => IsActive || uiFadeAlpha > 0.01f;

        private float powerDisplay;
        private float powerVel;
        private float latchHover;
        private float animTimer;

        private bool isDragging;
        private Vector2 dragOffset;
        private bool positionInitialized;

        private Rectangle panelRect;
        private Rectangle closeRect;
        private readonly Rectangle[] baitRects = new Rectangle[AutoFisherTP.BaitSlotCount];
        private readonly Rectangle[] catchRects = new Rectangle[AutoFisherTP.CatchSlotCount];
        private Rectangle toggleBtn;
        private Rectangle intervalDownBtn;
        private Rectangle intervalUpBtn;
        private Vector2 gaugeCenter;
        private Rectangle gaugeRect;

        private int hoveringBait = -1;
        private int hoveringCatch = -1;
        private bool hoveringToggle;
        private bool hoveringIntervalDown;
        private bool hoveringIntervalUp;
        private bool hoveringGauge;
        #endregion

        #region 本地化
        protected static LocalizedText TitleText;
        protected static LocalizedText BaitLabel;
        protected static LocalizedText CatchLabel;
        protected static LocalizedText IntervalLabel;
        protected static LocalizedText PowerReadout;
        protected static LocalizedText LakeReadout;
        protected static LocalizedText EnableText;
        protected static LocalizedText DisableText;
        protected static LocalizedText StatusFishing;
        protected static LocalizedText StatusIdle;
        protected static LocalizedText StatusNoPower;
        protected static LocalizedText StatusNoBait;
        protected static LocalizedText StatusNoWater;
        protected static LocalizedText StatusFull;
        protected static LocalizedText StatusOff;
        protected static LocalizedText EnergyLabel;
        protected static LocalizedText BaitHint;
        protected static LocalizedText CatchHint;
        protected static LocalizedText PowerUnit;

        public override Texture2D Texture => VaultAsset.placeholder2.Value;

        public override void SetStaticDefaults() {
            TitleText = this.GetLocalization(nameof(TitleText), () => "自动钓鱼机");
            BaitLabel = this.GetLocalization(nameof(BaitLabel), () => "鱼饵仓");
            CatchLabel = this.GetLocalization(nameof(CatchLabel), () => "渔获仓");
            IntervalLabel = this.GetLocalization(nameof(IntervalLabel), () => "垂钓间隔");
            PowerReadout = this.GetLocalization(nameof(PowerReadout), () => "钓力 {0}");
            LakeReadout = this.GetLocalization(nameof(LakeReadout), () => "水体 {0}");
            EnableText = this.GetLocalization(nameof(EnableText), () => "启用");
            DisableText = this.GetLocalization(nameof(DisableText), () => "停用");
            StatusFishing = this.GetLocalization(nameof(StatusFishing), () => "垂钓中");
            StatusIdle = this.GetLocalization(nameof(StatusIdle), () => "待机");
            StatusNoPower = this.GetLocalization(nameof(StatusNoPower), () => "缺电");
            StatusNoBait = this.GetLocalization(nameof(StatusNoBait), () => "缺饵");
            StatusNoWater = this.GetLocalization(nameof(StatusNoWater), () => "缺水");
            StatusFull = this.GetLocalization(nameof(StatusFull), () => "仓满");
            StatusOff = this.GetLocalization(nameof(StatusOff), () => "已停用");
            EnergyLabel = this.GetLocalization(nameof(EnergyLabel), () => "电力");
            BaitHint = this.GetLocalization(nameof(BaitHint), () => "放入鱼饵");
            CatchHint = this.GetLocalization(nameof(CatchHint), () => "渔获存放于此");
            PowerUnit = this.GetLocalization(nameof(PowerUnit), () => "UE");
        }
        #endregion

        public void Interactive(AutoFisherTP tp) {
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

            float powerTarget = MathHelper.Clamp(Station.MachineData.UEvalue / Station.MaxUEValue, 0f, 1f);
            powerVel = powerVel * 0.80f + (powerTarget - powerDisplay) * 0.05f;
            powerDisplay += powerVel;

            Point mouse = MousePosition.ToPoint();
            hoveringBait = -1;
            hoveringCatch = -1;
            for (int i = 0; i < baitRects.Length; i++) {
                if (baitRects[i].Contains(mouse) && !isDragging) {
                    hoveringBait = i;
                    break;
                }
            }
            if (hoveringBait < 0) {
                for (int i = 0; i < catchRects.Length; i++) {
                    if (catchRects[i].Contains(mouse) && !isDragging) {
                        hoveringCatch = i;
                        break;
                    }
                }
            }
            hoveringToggle = toggleBtn.Contains(mouse) && !isDragging;
            hoveringIntervalDown = intervalDownBtn.Contains(mouse) && !isDragging;
            hoveringIntervalUp = intervalUpBtn.Contains(mouse) && !isDragging;
            hoveringGauge = gaugeRect.Contains(mouse) && !isDragging;
            hoverInMainPage = panelRect.Contains(mouse);
            latchHover = MathHelper.Lerp(latchHover, closeRect.Contains(mouse) ? 1f : 0f, 0.2f);

            if (hoverInMainPage) {
                player.mouseInterface = true;
                UIInputGuard.SuppressWeaponSwitch();
            }

            if (closeRect.Contains(mouse) && keyLeftPressState == KeyPressState.Pressed) {
                IsActive = false;
                SoundEngine.PlaySound(CWRSound.ButtonZero with { Pitch = -0.3f, Volume = 0.5f });
                return;
            }

            if (keyLeftPressState == KeyPressState.Pressed) {
                if (hoveringToggle) {
                    Station.Enabled = !Station.Enabled;
                    Station.SendData();
                    SoundEngine.PlaySound(Station.Enabled ? SoundID.MenuOpen : SoundID.MenuClose);
                }
                else if (hoveringIntervalDown || hoveringIntervalUp) {
                    int delta = hoveringIntervalUp ? 60 : -60;
                    Station.FishInterval = Math.Clamp(Station.FishInterval + delta, 300, 1200);
                    Station.SendData();
                    SoundEngine.PlaySound(SoundID.MenuTick);
                }
                else if (hoveringBait >= 0) {
                    HandleBaitClick(hoveringBait);
                }
                else if (hoveringCatch >= 0) {
                    HandleCatchClick(hoveringCatch);
                }
            }

            //背景区拖拽,避开控件
            bool overControl = hoveringToggle || hoveringBait >= 0 || hoveringCatch >= 0
                || hoveringIntervalDown || hoveringIntervalUp || closeRect.Contains(mouse);
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

            for (int i = 0; i < baitRects.Length; i++) {
                baitRects[i] = new Rectangle(panelRect.X + 26 + i * (SlotSize + SlotGap), panelRect.Y + 72, SlotSize, SlotSize);
            }
            for (int i = 0; i < catchRects.Length; i++) {
                int col = i % 6;
                int row = i / 6;
                catchRects[i] = new Rectangle(panelRect.X + 26 + col * (SlotSize + 8),
                    panelRect.Y + 160 + row * (SlotSize + 8), SlotSize, SlotSize);
            }

            toggleBtn = new Rectangle(panelRect.X + 366, panelRect.Y + 66, 100, 30);
            intervalDownBtn = new Rectangle(panelRect.X + 366, panelRect.Y + 128, 24, 22);
            intervalUpBtn = new Rectangle(panelRect.X + 442, panelRect.Y + 128, 24, 22);
            gaugeCenter = new Vector2(panelRect.X + 416, panelRect.Y + 244);
            gaugeRect = new Rectangle((int)gaugeCenter.X - 32, (int)gaugeCenter.Y - 32, 64, 64);
        }

        /// <summary>鱼饵槽交互:本地改+SendData推送(客户端权威的UI编辑模型)</summary>
        private void HandleBaitClick(int index) {
            Item slotItem = Station.Baits[index];
            Item mouseItem = Main.mouseItem;

            if (mouseItem.IsAir) {
                if (slotItem != null && !slotItem.IsAir) {
                    Main.mouseItem = slotItem.Clone();
                    Station.Baits[index] = new Item();
                    SoundEngine.PlaySound(SoundID.Grab);
                    Station.SendData();
                }
                return;
            }

            if (!AutoFisherTP.IsBait(mouseItem)) {
                SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.4f, Pitch = -0.2f });
                return;
            }

            if (slotItem == null || slotItem.IsAir) {
                Station.Baits[index] = mouseItem.Clone();
                mouseItem.TurnToAir();
                SoundEngine.PlaySound(SoundID.Grab);
                Station.SendData();
            }
            else if (slotItem.type == mouseItem.type && slotItem.stack < slotItem.maxStack) {
                int add = Math.Min(mouseItem.stack, slotItem.maxStack - slotItem.stack);
                slotItem.stack += add;
                mouseItem.stack -= add;
                if (mouseItem.stack <= 0) {
                    mouseItem.TurnToAir();
                }
                SoundEngine.PlaySound(SoundID.Grab);
                Station.SendData();
            }
            else {
                Item temp = slotItem.Clone();
                Station.Baits[index] = mouseItem.Clone();
                Main.mouseItem = temp;
                SoundEngine.PlaySound(SoundID.Grab);
                Station.SendData();
            }
        }

        /// <summary>渔获槽只出不进:空手点击取出整叠,同类可叠进手上</summary>
        private void HandleCatchClick(int index) {
            Item slotItem = Station.Catches[index];
            if (slotItem == null || slotItem.IsAir) {
                return;
            }

            Item mouseItem = Main.mouseItem;
            if (mouseItem.IsAir) {
                Main.mouseItem = slotItem.Clone();
                Station.Catches[index] = new Item();
                SoundEngine.PlaySound(SoundID.Grab);
                Station.SendData();
            }
            else if (mouseItem.type == slotItem.type && mouseItem.stack < mouseItem.maxStack) {
                int add = Math.Min(slotItem.stack, mouseItem.maxStack - mouseItem.stack);
                mouseItem.stack += add;
                slotItem.stack -= add;
                if (slotItem.stack <= 0) {
                    slotItem.TurnToAir();
                }
                SoundEngine.PlaySound(SoundID.Grab);
                Station.SendData();
            }
        }

        public override void OnEnterWorld() => IsActive = false;

        public override void SaveUIData(TagCompound tag) {
            tag["AutoFisherUI_DrawPos_X"] = DrawPosition.X;
            tag["AutoFisherUI_DrawPos_Y"] = DrawPosition.Y;
        }

        public override void LoadUIData(TagCompound tag) {
            if (tag.TryGet("AutoFisherUI_DrawPos_X", out float x)) {
                DrawPosition.X = x;
            }
            if (tag.TryGet("AutoFisherUI_DrawPos_Y", out float y)) {
                DrawPosition.Y = y;
            }
        }

        #region 绘制
        public override void Draw(SpriteBatch spriteBatch) {
            if (uiFadeAlpha < 0.01f || Station == null) {
                return;
            }

            DrawShell(spriteBatch);
            DrawSlots(spriteBatch);
            DrawControlColumn(spriteBatch);
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

        private void DrawSlots(SpriteBatch sb) {
            float alpha = uiFadeAlpha;

            Utils.DrawBorderString(sb, BaitLabel.Value,
                new Vector2(panelRect.X + 26, panelRect.Y + 50), TextDim * alpha, 0.62f);
            for (int i = 0; i < baitRects.Length; i++) {
                IndustrialTerminalRenderer.DrawSocket(sb, baitRects[i], alpha, hoveringBait == i ? 1f : 0f, 0f);
                DrawSlotItem(sb, Station.Baits[i], baitRects[i], alpha);
            }

            //钓力与水体读数,贴在鱼饵仓右侧
            string powerText = string.Format(PowerReadout.Value, Station.CurrentPower);
            string lakeText = string.Format(LakeReadout.Value, Station.LakeSize);
            Utils.DrawBorderString(sb, powerText,
                new Vector2(panelRect.X + 254, panelRect.Y + 76),
                Color.Lerp(TextMain, Accent, 0.4f) * alpha, 0.66f);
            Utils.DrawBorderString(sb, lakeText,
                new Vector2(panelRect.X + 254, panelRect.Y + 98),
                (Station.WaterOK ? TextDim : WarnRed) * alpha, 0.62f);

            Utils.DrawBorderString(sb, CatchLabel.Value,
                new Vector2(panelRect.X + 26, panelRect.Y + 138),
                Color.Lerp(TextDim, IndustrialTerminalRenderer.BrassBright, 0.4f) * alpha, 0.62f);
            for (int i = 0; i < catchRects.Length; i++) {
                IndustrialTerminalRenderer.DrawSocket(sb, catchRects[i], alpha, hoveringCatch == i ? 1f : 0f, 0f);
                DrawSlotItem(sb, Station.Catches[i], catchRects[i], alpha);
            }
        }

        private static void DrawSlotItem(SpriteBatch sb, Item item, Rectangle rect, float alpha) {
            if (item == null || item.IsAir) {
                return;
            }
            Main.instance.LoadItem(item.type);
            VaultUtils.SimpleDrawItem(sb, item.type, rect.Center.ToVector2(), 30, 1f, 0, Color.White * alpha);

            if (item.stack > 1) {
                string stackText = item.stack.ToString();
                Vector2 stackSize = FontAssets.ItemStack.Value.MeasureString(stackText);
                Utils.DrawBorderStringFourWay(sb, FontAssets.ItemStack.Value, stackText,
                    rect.Right - stackSize.X * 0.7f - 4, rect.Bottom - stackSize.Y * 0.7f - 4,
                    Color.White * alpha, Color.Black * alpha, new Vector2(0.3f), 0.7f);
            }
        }

        private void DrawControlColumn(SpriteBatch sb) {
            float alpha = uiFadeAlpha;

            //启停按钮
            string toggleLabel = Station.Enabled ? DisableText.Value : EnableText.Value;
            IndustrialTerminalRenderer.DrawButton(sb, toggleBtn, alpha, hoveringToggle ? 1f : 0f,
                hoveringToggle && keyLeftPressState == KeyPressState.Held, toggleLabel);

            //垂钓间隔
            Utils.DrawBorderString(sb, IntervalLabel.Value,
                new Vector2(panelRect.X + 366, panelRect.Y + 106), TextDim * alpha, 0.62f);
            IndustrialTerminalRenderer.DrawButton(sb, intervalDownBtn, alpha, hoveringIntervalDown ? 1f : 0f,
                hoveringIntervalDown && keyLeftPressState == KeyPressState.Held, "-");
            IndustrialTerminalRenderer.DrawButton(sb, intervalUpBtn, alpha, hoveringIntervalUp ? 1f : 0f,
                hoveringIntervalUp && keyLeftPressState == KeyPressState.Held, "+");
            string intervalText = $"{Station.FishInterval / 60f:F0}s";
            Vector2 intervalSize = FontAssets.MouseText.Value.MeasureString(intervalText) * 0.66f;
            Utils.DrawBorderString(sb, intervalText,
                new Vector2(panelRect.X + 416 - intervalSize.X * 0.5f, panelRect.Y + 132),
                Color.Lerp(TextMain, Accent, 0.4f) * alpha, 0.66f);

            //状态灯
            Vector2 lampPos = new(panelRect.X + 374, panelRect.Y + 182);
            string state;
            Color lampColor;
            float lampBright;
            if (!Station.Enabled) {
                state = StatusOff.Value;
                lampColor = TextDim;
                lampBright = 0.2f;
            }
            else if (Station.FishState == 1) {
                state = StatusFishing.Value;
                lampColor = Accent;
                lampBright = MathF.Sin(animTimer * 2.6f) * 0.2f + 0.72f;
            }
            else if (!Station.WaterOK) {
                state = StatusNoWater.Value;
                lampColor = WarnRed;
                lampBright = MathF.Sin(animTimer * 5f) * 0.35f + 0.55f;
            }
            else if (!Station.HasBait) {
                state = StatusNoBait.Value;
                lampColor = new Color(255, 200, 80);
                lampBright = 0.6f;
            }
            else if (!Station.CatchHasSpace) {
                state = StatusFull.Value;
                lampColor = new Color(255, 200, 80);
                lampBright = 0.6f;
            }
            else if (Station.MachineData.UEvalue < AutoFisherTP.CastCost) {
                state = StatusNoPower.Value;
                lampColor = WarnRed;
                lampBright = MathF.Sin(animTimer * 5f) * 0.35f + 0.55f;
            }
            else {
                state = StatusIdle.Value;
                lampColor = TextDim;
                lampBright = 0.3f;
            }
            IndustrialTerminalRenderer.DrawLamp(sb, lampPos, lampColor, alpha, lampBright);
            Utils.DrawBorderString(sb, state, lampPos + new Vector2(14, -8),
                Color.Lerp(TextMain, lampColor, 0.35f) * alpha, 0.66f);

            //电力表盘
            float jitter = Station.FishState == 1 ? MathF.Sin(animTimer * 30f) * 0.004f : 0f;
            float ratio = MathHelper.Clamp(Station.MachineData.UEvalue / Station.MaxUEValue, 0f, 1f);
            IndustrialTerminalRenderer.DrawGauge(sb, gaugeCenter, 30f, powerDisplay + jitter,
                Accent, alpha, EnergyLabel.Value, $"{(int)(ratio * 100f)}%");
        }

        private void DrawHoverTips(SpriteBatch sb) {
            if (isDragging) {
                return;
            }

            if (hoveringBait >= 0) {
                Item item = Station.Baits[hoveringBait];
                if (item != null && !item.IsAir) {
                    Main.HoverItem = item.Clone();
                    Main.hoverItemName = item.Name;
                }
                else {
                    ShowTip(sb, BaitHint.Value);
                }
            }
            else if (hoveringCatch >= 0) {
                Item item = Station.Catches[hoveringCatch];
                if (item != null && !item.IsAir) {
                    Main.HoverItem = item.Clone();
                    Main.hoverItemName = item.Name;
                }
                else {
                    ShowTip(sb, CatchHint.Value);
                }
            }
            else if (hoveringGauge) {
                ShowTip(sb, $"{(int)Station.MachineData.UEvalue}/{(int)Station.MaxUEValue} {PowerUnit.Value}");
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
