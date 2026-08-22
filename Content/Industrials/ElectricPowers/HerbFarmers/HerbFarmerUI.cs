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

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.HerbFarmers
{
    /// <summary>
    /// 草药农场机面板:种子仓+产出仓+启停+电力表盘,
    /// 笔刷与材质在 <see cref="IndustrialTerminalRenderer"/>
    /// </summary>
    internal class HerbFarmerUI : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "UI.HerbFarmer";

        #region 布局与状态
        private const float PanelWidth = 460f;
        private const float PanelHeight = 300f;
        private const int SlotSize = 44;
        private const int SlotGap = 10;

        private static Color TextMain => IndustrialTerminalRenderer.TextMain;
        private static Color TextDim => IndustrialTerminalRenderer.TextDim;
        private static Color WarnRed => IndustrialTerminalRenderer.WarnRed;
        private static Color Accent => HerbFarmer.Tint;

        private static float UIScreenW => PlayerInput.RealScreenWidth / Main.UIScale;
        private static float UIScreenH => PlayerInput.RealScreenHeight / Main.UIScale;

        public static HerbFarmerUI Instance => UIHandleLoader.GetUIHandleOfType<HerbFarmerUI>();

        internal HerbFarmerTP Station;
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
        private readonly Rectangle[] seedRects = new Rectangle[HerbFarmerTP.SeedSlotCount];
        private readonly Rectangle[] produceRects = new Rectangle[HerbFarmerTP.ProduceSlotCount];
        private Rectangle toggleBtn;
        private Vector2 gaugeCenter;
        private Rectangle gaugeRect;

        private int hoveringSeed = -1;
        private int hoveringProduce = -1;
        private bool hoveringToggle;
        private bool hoveringGauge;
        #endregion

        #region 本地化
        protected static LocalizedText TitleText;
        protected static LocalizedText SeedLabel;
        protected static LocalizedText ProduceLabel;
        protected static LocalizedText EnableText;
        protected static LocalizedText DisableText;
        protected static LocalizedText StatusWorking;
        protected static LocalizedText StatusIdle;
        protected static LocalizedText StatusNoPower;
        protected static LocalizedText StatusOff;
        protected static LocalizedText EnergyLabel;
        protected static LocalizedText SeedHint;
        protected static LocalizedText TakeHint;
        protected static LocalizedText PowerUnit;

        public override Texture2D Texture => VaultAsset.placeholder2.Value;

        public override void SetStaticDefaults() {
            TitleText = this.GetLocalization(nameof(TitleText), () => "草药农场机");
            SeedLabel = this.GetLocalization(nameof(SeedLabel), () => "种子仓");
            ProduceLabel = this.GetLocalization(nameof(ProduceLabel), () => "产出仓");
            EnableText = this.GetLocalization(nameof(EnableText), () => "启用");
            DisableText = this.GetLocalization(nameof(DisableText), () => "停用");
            StatusWorking = this.GetLocalization(nameof(StatusWorking), () => "耕作中");
            StatusIdle = this.GetLocalization(nameof(StatusIdle), () => "待机");
            StatusNoPower = this.GetLocalization(nameof(StatusNoPower), () => "缺电");
            StatusOff = this.GetLocalization(nameof(StatusOff), () => "已停用");
            EnergyLabel = this.GetLocalization(nameof(EnergyLabel), () => "电力");
            SeedHint = this.GetLocalization(nameof(SeedHint), () => "放入草药种子");
            TakeHint = this.GetLocalization(nameof(TakeHint), () => "点击取出");
            PowerUnit = this.GetLocalization(nameof(PowerUnit), () => "UE");
        }
        #endregion

        public void Interactive(HerbFarmerTP tp) {
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
            hoveringSeed = -1;
            hoveringProduce = -1;
            for (int i = 0; i < seedRects.Length; i++) {
                if (seedRects[i].Contains(mouse) && !isDragging) {
                    hoveringSeed = i;
                    break;
                }
            }
            if (hoveringSeed < 0) {
                for (int i = 0; i < produceRects.Length; i++) {
                    if (produceRects[i].Contains(mouse) && !isDragging) {
                        hoveringProduce = i;
                        break;
                    }
                }
            }
            hoveringToggle = toggleBtn.Contains(mouse) && !isDragging;
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
                else if (hoveringSeed >= 0) {
                    HandleSeedClick(hoveringSeed);
                }
                else if (hoveringProduce >= 0) {
                    HandleProduceClick(hoveringProduce);
                }
            }

            //背景区拖拽,避开控件
            bool overControl = hoveringToggle || hoveringSeed >= 0 || hoveringProduce >= 0 || closeRect.Contains(mouse);
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

            for (int i = 0; i < seedRects.Length; i++) {
                seedRects[i] = new Rectangle(panelRect.X + 26 + i * (SlotSize + SlotGap), panelRect.Y + 76, SlotSize, SlotSize);
            }
            for (int i = 0; i < produceRects.Length; i++) {
                int col = i % 4;
                int row = i / 4;
                produceRects[i] = new Rectangle(panelRect.X + 26 + col * (SlotSize + SlotGap),
                    panelRect.Y + 164 + row * (SlotSize + SlotGap), SlotSize, SlotSize);
            }

            toggleBtn = new Rectangle(panelRect.X + 300, panelRect.Y + 76, 110, 30);
            gaugeCenter = new Vector2(panelRect.X + 372, panelRect.Y + 210);
            gaugeRect = new Rectangle((int)gaugeCenter.X - 32, (int)gaugeCenter.Y - 32, 64, 64);
        }

        /// <summary>种子槽交互:本地改+SendData推送(客户端权威的UI编辑模型)</summary>
        private void HandleSeedClick(int index) {
            Item slotItem = Station.Seeds[index];
            Item mouseItem = Main.mouseItem;

            if (mouseItem.IsAir) {
                if (slotItem != null && !slotItem.IsAir) {
                    Main.mouseItem = slotItem.Clone();
                    Station.Seeds[index] = new Item();
                    SoundEngine.PlaySound(SoundID.Grab);
                    Station.SendData();
                }
                return;
            }

            if (!HerbFarmerTP.IsHerbSeed(mouseItem)) {
                SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.4f, Pitch = -0.2f });
                return;
            }

            if (slotItem == null || slotItem.IsAir) {
                Station.Seeds[index] = mouseItem.Clone();
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
                Station.Seeds[index] = mouseItem.Clone();
                Main.mouseItem = temp;
                SoundEngine.PlaySound(SoundID.Grab);
                Station.SendData();
            }
        }

        /// <summary>产出槽只出不进:空手点击取出整叠</summary>
        private void HandleProduceClick(int index) {
            Item slotItem = Station.Produce[index];
            if (slotItem == null || slotItem.IsAir) {
                return;
            }

            Item mouseItem = Main.mouseItem;
            if (mouseItem.IsAir) {
                Main.mouseItem = slotItem.Clone();
                Station.Produce[index] = new Item();
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
            tag["HerbFarmerUI_DrawPos_X"] = DrawPosition.X;
            tag["HerbFarmerUI_DrawPos_Y"] = DrawPosition.Y;
        }

        public override void LoadUIData(TagCompound tag) {
            if (tag.TryGet("HerbFarmerUI_DrawPos_X", out float x)) {
                DrawPosition.X = x;
            }
            if (tag.TryGet("HerbFarmerUI_DrawPos_Y", out float y)) {
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
            DrawStatusColumn(spriteBatch);
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

            Utils.DrawBorderString(sb, SeedLabel.Value,
                new Vector2(panelRect.X + 26, panelRect.Y + 54), TextDim * alpha, 0.62f);
            for (int i = 0; i < seedRects.Length; i++) {
                IndustrialTerminalRenderer.DrawSocket(sb, seedRects[i], alpha, hoveringSeed == i ? 1f : 0f, 0f);
                DrawSlotItem(sb, Station.Seeds[i], seedRects[i], alpha);
            }

            Utils.DrawBorderString(sb, ProduceLabel.Value,
                new Vector2(panelRect.X + 26, panelRect.Y + 142),
                Color.Lerp(TextDim, IndustrialTerminalRenderer.BrassBright, 0.4f) * alpha, 0.62f);
            for (int i = 0; i < produceRects.Length; i++) {
                IndustrialTerminalRenderer.DrawSocket(sb, produceRects[i], alpha, hoveringProduce == i ? 1f : 0f, 0f);
                DrawSlotItem(sb, Station.Produce[i], produceRects[i], alpha);
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

        private void DrawStatusColumn(SpriteBatch sb) {
            float alpha = uiFadeAlpha;

            //启停按钮
            string toggleLabel = Station.Enabled ? DisableText.Value : EnableText.Value;
            IndustrialTerminalRenderer.DrawButton(sb, toggleBtn, alpha, hoveringToggle ? 1f : 0f,
                hoveringToggle && keyLeftPressState == KeyPressState.Held, toggleLabel);

            //状态灯
            Vector2 lampPos = new(panelRect.X + 310, panelRect.Y + 132);
            string state;
            Color lampColor;
            float lampBright;
            if (!Station.Enabled) {
                state = StatusOff.Value;
                lampColor = TextDim;
                lampBright = 0.2f;
            }
            else if (Station.MachineData.UEvalue < HerbFarmerTP.PlantCost) {
                state = StatusNoPower.Value;
                lampColor = WarnRed;
                lampBright = MathF.Sin(animTimer * 5f) * 0.35f + 0.55f;
            }
            else if (Station.IsWorking) {
                state = StatusWorking.Value;
                lampColor = Accent;
                lampBright = MathF.Sin(animTimer * 2.6f) * 0.2f + 0.72f;
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
            float jitter = Station.IsWorking ? MathF.Sin(animTimer * 30f) * 0.004f : 0f;
            float ratio = MathHelper.Clamp(Station.MachineData.UEvalue / Station.MaxUEValue, 0f, 1f);
            IndustrialTerminalRenderer.DrawGauge(sb, gaugeCenter, 30f, powerDisplay + jitter,
                Accent, alpha, EnergyLabel.Value, $"{(int)(ratio * 100f)}%");
        }

        private void DrawHoverTips(SpriteBatch sb) {
            if (isDragging) {
                return;
            }

            if (hoveringSeed >= 0) {
                Item item = Station.Seeds[hoveringSeed];
                if (item != null && !item.IsAir) {
                    Main.HoverItem = item.Clone();
                    Main.hoverItemName = item.Name;
                }
                else {
                    ShowTip(sb, SeedHint.Value);
                }
            }
            else if (hoveringProduce >= 0) {
                Item item = Station.Produce[hoveringProduce];
                if (item != null && !item.IsAir) {
                    Main.HoverItem = item.Clone();
                    Main.hoverItemName = item.Name;
                }
                else {
                    ShowTip(sb, TakeHint.Value);
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
