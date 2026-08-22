using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Industrials.MachineModules;
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

namespace CalamityOverhaul.Content.Industrials.Generator.Thermal
{
    /// <summary>
    /// 热力发电机面板:锅炉房仪器语言，钢壳(随炉温沁暖)、黄铜门框的炉门观火窗、
    /// 温度/电力双指针表、状态灯。笔刷与材质在 <see cref="IndustrialTerminalRenderer"/>。<br/>
    /// 生命周期沿用 <see cref="BaseGeneratorUI"/>(IsActive/右键开合/超距自动关/位置持久化),
    /// 燃料交互(点击投放/满手燃料右键直投)与旧版语义一致
    /// </summary>
    internal class ThermalGeneratorUI : BaseGeneratorUI, ILocalizedModType
    {
        public string LocalizationCategory => "UI.Generator";

        #region 布局与状态
        private const float PanelWidth = 460f;
        private const float PanelHeight = 332f;

        private static Color TextMain => IndustrialTerminalRenderer.TextMain;
        private static Color TextDim => IndustrialTerminalRenderer.TextDim;
        private static Color Amber => IndustrialTerminalRenderer.Amber;
        private static Color WarnRed => IndustrialTerminalRenderer.WarnRed;
        private static Color Brass => IndustrialTerminalRenderer.Brass;
        private static Color BrassBright => IndustrialTerminalRenderer.BrassBright;

        private static float UIScreenW => PlayerInput.RealScreenWidth / Main.UIScale;
        private static float UIScreenH => PlayerInput.RealScreenHeight / Main.UIScale;

        //UI淡入淡出(IsActive 驱动;Active 放宽到淡出结束,收摊有过程)
        private float uiFadeAlpha;
        public override bool Active => IsActive || uiFadeAlpha > 0.01f;

        //仪表指针的欠阻尼弹簧
        private float tempDisplay;
        private float tempVel;
        private float powerDisplay;
        private float powerVel;
        private float latchHover;

        //拖拽
        private bool isDragging;
        private Vector2 dragOffset;
        private bool positionInitialized;

        //布局矩形
        private Rectangle panelRect;
        private Rectangle doorRect;
        private Rectangle burnBarRect;
        private Rectangle closeRect;
        private Vector2 tempGaugeCenter;
        private Vector2 powerGaugeCenter;
        private Rectangle tempGaugeRect;
        private Rectangle powerGaugeRect;
        private bool hoveringFuelSlot;
        private bool hoveringTempGauge;
        private bool hoveringPowerGauge;
        private bool hoveringSockets;

        //模块插座行(点击/校验/红闪/绘制在共享件里)
        private readonly ModuleSocketStrip socketStrip = new();

        //炉膛粒子:余烬与灰烬,从炉门缝里冒
        private readonly List<EmberPRT> embers = new();
        private int emberSpawnTimer;
        private readonly List<AshPRT> ashes = new();
        private int ashSpawnTimer;

        private float animTimer;

        private ThermalData ThermalData => GeneratorTP?.MachineData as ThermalData;
        #endregion

        #region 本地化
        protected static LocalizedText TitleText;
        protected static LocalizedText FuelLabel;
        protected static LocalizedText TemperatureLabel;
        protected static LocalizedText PowerLabel;
        protected static LocalizedText StatusLabel;
        protected static LocalizedText ActiveText;
        protected static LocalizedText IdleText;
        protected static LocalizedText EfficiencyText;
        protected static LocalizedText InsertFuelHint;
        protected static LocalizedText TemperatureUnit;
        protected static LocalizedText PowerUnit;

        public override Texture2D Texture => VaultAsset.placeholder2.Value;

        public override void SetStaticDefaults() {
            TitleText = this.GetLocalization(nameof(TitleText), () => "热能发电机");
            FuelLabel = this.GetLocalization(nameof(FuelLabel), () => "燃料");
            TemperatureLabel = this.GetLocalization(nameof(TemperatureLabel), () => "温度");
            PowerLabel = this.GetLocalization(nameof(PowerLabel), () => "电力");
            StatusLabel = this.GetLocalization(nameof(StatusLabel), () => "状态:");
            ActiveText = this.GetLocalization(nameof(ActiveText), () => "运行中");
            IdleText = this.GetLocalization(nameof(IdleText), () => "待机");
            EfficiencyText = this.GetLocalization(nameof(EfficiencyText), () => "效率: {0}%");
            InsertFuelHint = this.GetLocalization(nameof(InsertFuelHint), () => "点击放入/取出燃料");
            TemperatureUnit = this.GetLocalization(nameof(TemperatureUnit), () => "°C");
            PowerUnit = this.GetLocalization(nameof(PowerUnit), () => "UE");
        }
        #endregion

        public override void UpdateElement() {
            //首帧夹到屏内(LoadUIData 可能早于屏初始化)
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

            ComputeLayout();
            UpdateNeedles();
            socketStrip.Update();

            Point mouse = MousePosition.ToPoint();
            hoveringFuelSlot = doorRect.Contains(mouse) && !isDragging;
            hoveringTempGauge = tempGaugeRect.Contains(mouse) && !isDragging;
            hoveringPowerGauge = powerGaugeRect.Contains(mouse) && !isDragging;
            hoveringSockets = socketStrip.Contains(mouse) && !isDragging;
            hoverInMainPage = panelRect.Contains(mouse);
            latchHover = MathHelper.Lerp(latchHover, closeRect.Contains(mouse) ? 1f : 0f, 0.2f);

            if (hoverInMainPage) {
                player.mouseInterface = true;
                //无滚动列表,但悬停期间滚轮不该翻快捷栏
                UIInputGuard.SuppressWeaponSwitch();
            }

            //闩钮关闭
            if (closeRect.Contains(mouse) && keyLeftPressState == KeyPressState.Pressed) {
                IsActive = false;
                SoundEngine.PlaySound(CWRSound.ButtonZero with { Pitch = -0.3f, Volume = 0.5f });
                return;
            }

            //模块插座行点击(先于拖拽捕获)
            if (keyLeftPressState == KeyPressState.Pressed && hoveringSockets && GeneratorTP != null) {
                socketStrip.HandleClick(mouse, GeneratorTP.ModuleRack, GeneratorTP.ModuleSlotCount,
                    player, () => GeneratorTP.SendData());
            }

            //背景区拖拽,避开炉门/表盘/插座/闩钮
            if (keyLeftPressState == KeyPressState.Pressed && hoverInMainPage
                && !hoveringFuelSlot && !hoveringTempGauge && !hoveringPowerGauge && !hoveringSockets
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

            if (hoveringFuelSlot && ThermalData != null) {
                if (!ThermalData.FuelItem.IsAir) {
                    Main.HoverItem = ThermalData.FuelItem.Clone();
                    Main.hoverItemName = ThermalData.FuelItem.Name;
                }

                if (keyLeftPressState == KeyPressState.Pressed) {
                    if (GeneratorTP is ThermalGeneratorTP thermal) {
                        thermal.HandlerItem();
                    }
                }
            }

            UpdateParticles();
        }

        private void ComputeLayout() {
            Vector2 topLeft = DrawPosition - new Vector2(PanelWidth / 2, PanelHeight / 2);
            panelRect = new Rectangle((int)topLeft.X, (int)topLeft.Y, (int)PanelWidth, (int)PanelHeight);
            closeRect = new Rectangle(panelRect.Right - 40, panelRect.Y + 9, 26, 26);
            doorRect = new Rectangle(panelRect.X + 30, panelRect.Y + 80, 92, 92);
            burnBarRect = new Rectangle(panelRect.X + 30, panelRect.Y + 180, 92, 8);
            tempGaugeCenter = new Vector2(panelRect.X + 240, panelRect.Y + 138);
            powerGaugeCenter = new Vector2(panelRect.X + 360, panelRect.Y + 138);
            tempGaugeRect = new Rectangle((int)tempGaugeCenter.X - 36, (int)tempGaugeCenter.Y - 36, 72, 72);
            powerGaugeRect = new Rectangle((int)powerGaugeCenter.X - 36, (int)powerGaugeCenter.Y - 36, 72, 72);
            //插座行:状态行下方左侧,槽数随 TP(MK1 两槽/MK2 三槽)
            socketStrip.Layout(panelRect.X + 34, panelRect.Y + 272,
                GeneratorTP?.ModuleSlotCount ?? 0, 44, 10);
        }

        /// <summary>仪表指针的欠阻尼弹簧:上电摆动、变化时过冲回稳</summary>
        private void UpdateNeedles() {
            ThermalData data = ThermalData;
            float tempTarget = data != null ? MathHelper.Clamp(data.Temperature / data.MaxTemperature, 0f, 1f) : 0f;
            tempVel = tempVel * 0.80f + (tempTarget - tempDisplay) * 0.05f;
            tempDisplay += tempVel;

            float powerTarget = data != null ? MathHelper.Clamp(data.UEvalue / data.MaxUEValue, 0f, 1f) : 0f;
            powerVel = powerVel * 0.80f + (powerTarget - powerDisplay) * 0.05f;
            powerDisplay += powerVel;
        }

        /// <summary>余烬与灰烬都从炉门缝里冒,有来源感</summary>
        private void UpdateParticles() {
            if (uiFadeAlpha < 0.3f || ThermalData == null) {
                return;
            }

            bool burning = ThermalData.IsBurning;
            bool warm = ThermalData.Temperature > 0;

            //余烬:燃烧时密,余温时疏
            emberSpawnTimer++;
            int emberInterval = burning ? 3 : 14;
            if (warm && emberSpawnTimer >= emberInterval && embers.Count < 36) {
                emberSpawnTimer = 0;
                float xPos = Main.rand.NextFloat(doorRect.X + 8, doorRect.Right - 8);
                embers.Add(new EmberPRT(new Vector2(xPos, doorRect.Bottom - 6)));
            }
            for (int i = embers.Count - 1; i >= 0; i--) {
                if (embers[i].Update()) {
                    embers.RemoveAt(i);
                }
            }

            //灰烬:只在燃烧时从门楣飘出
            ashSpawnTimer++;
            if (burning && ashSpawnTimer >= 10 && ashes.Count < 20) {
                ashSpawnTimer = 0;
                float xPos = Main.rand.NextFloat(doorRect.X + 6, doorRect.Right - 6);
                ashes.Add(new AshPRT(new Vector2(xPos, doorRect.Y + 10)));
            }
            for (int i = ashes.Count - 1; i >= 0; i--) {
                if (ashes[i].Update()) {
                    ashes.RemoveAt(i);
                }
            }
        }

        public override void OnEnterWorld() => IsActive = false;

        public override void SaveUIData(TagCompound tag) {
            tag["ThermalGeneratorUI_DrawPos_X"] = DrawPosition.X;
            tag["ThermalGeneratorUI_DrawPos_Y"] = DrawPosition.Y;
        }

        public override void LoadUIData(TagCompound tag) {
            if (tag.TryGet("ThermalGeneratorUI_DrawPos_X", out float x)) {
                DrawPosition.X = x;
            }
            else {
                DrawPosition.X = Main.screenWidth / 2;
            }

            if (tag.TryGet("ThermalGeneratorUI_DrawPos_Y", out float y)) {
                DrawPosition.Y = y;
            }
            else {
                DrawPosition.Y = Main.screenHeight / 2;
            }
        }

        public override void RightClickByTile(bool newTP) {
            Item item = Main.LocalPlayer.GetItem();
            if ((!item.IsAir) && FuelItems.FuelItemToCombustion.ContainsKey(item.type)) {
                return;
            }

            if (!Main.keyState.PressingShift()) {
                if (!newTP) {
                    IsActive = !IsActive;
                }
                else {
                    IsActive = true;
                }
            }

            SoundEngine.PlaySound(CWRSound.ButtonZero with { Volume = 0.3f, Pitch = -0.5f });
        }

        #region 绘制
        public override void Draw(SpriteBatch spriteBatch) {
            if (uiFadeAlpha < 0.01f) {
                return;
            }
            if (ThermalData == null) {
                return;
            }

            DrawShell(spriteBatch);
            DrawFurnaceDoor(spriteBatch);

            //粒子夹在炉门与表盘之间:灰烬沉底,余烬压上
            foreach (AshPRT ash in ashes) {
                ash.Draw(spriteBatch, uiFadeAlpha * 0.6f);
            }
            foreach (EmberPRT ember in embers) {
                ember.Draw(spriteBatch, uiFadeAlpha * 0.95f);
            }

            DrawGauges(spriteBatch);
            DrawStatusRow(spriteBatch);
            DrawSockets(spriteBatch);
            DrawHoverTips(spriteBatch);
        }

        /// <summary>模块插座行:状态行下方左侧</summary>
        private void DrawSockets(SpriteBatch sb) {
            if (GeneratorTP == null || GeneratorTP.ModuleSlotCount <= 0) {
                return;
            }
            float alpha = uiFadeAlpha;
            Utils.DrawBorderString(sb, MachineModuleText.SlotLabel.Value,
                new Vector2(panelRect.X + 34, panelRect.Y + 254), TextDim * alpha, 0.6f);
            socketStrip.Draw(sb, GeneratorTP.ModuleRack, GeneratorTP.ModuleSlotCount,
                alpha, MousePosition.ToPoint());
        }

        /// <summary>钢壳 + 铆钉 + 黄铜铭牌 + 闩钮;机壳随炉温沁暖</summary>
        private void DrawShell(SpriteBatch sb) {
            float alpha = uiFadeAlpha;
            float tempRatio = MathHelper.Clamp(ThermalData.Temperature / ThermalData.MaxTemperature, 0f, 1f);

            IndustrialTerminalRenderer.ShaderPanel(sb, panelRect, alpha, mode: 0, heat: tempRatio);

            int inset = IndustrialTerminalRenderer.Chamfer + 2;
            IndustrialTerminalRenderer.DrawRivet(sb, new Vector2(panelRect.X + inset, panelRect.Y + inset), alpha);
            IndustrialTerminalRenderer.DrawRivet(sb, new Vector2(panelRect.Right - inset, panelRect.Y + inset), alpha);
            IndustrialTerminalRenderer.DrawRivet(sb, new Vector2(panelRect.X + inset, panelRect.Bottom - inset), alpha);
            IndustrialTerminalRenderer.DrawRivet(sb, new Vector2(panelRect.Right - inset, panelRect.Bottom - inset), alpha);

            //黄铜铭牌标题(亮暖填漆字)
            string title = TitleText.Value;
            Vector2 titleSize = FontAssets.MouseText.Value.MeasureString(title) * 0.86f;
            Rectangle plate = new(panelRect.X + 22, panelRect.Y + 9, (int)titleSize.X + 30, 27);
            IndustrialTerminalRenderer.DrawNameplate(sb, plate, alpha);
            IndustrialTerminalRenderer.DrawPlateTitle(sb, plate, title, alpha, 0.86f);

            //标题栏下的蚀刻分隔
            IndustrialTerminalRenderer.DrawEtchedLine(sb, panelRect.X + 14, panelRect.Width - 28, panelRect.Y + 44, alpha, 0.8f);

            //闩钮
            IndustrialTerminalRenderer.DrawLatch(sb, closeRect.Center.ToVector2(), alpha, latchHover);
        }

        /// <summary>炉门:凹槽床 + 黄铜门框 + 观火窗,燃烧时膛内透出琥珀火光</summary>
        private void DrawFurnaceDoor(SpriteBatch sb) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float alpha = uiFadeAlpha;
            Rectangle src = new(0, 0, 1, 1);
            bool burning = ThermalData.IsBurning;

            //门上标签
            Vector2 labelSize = FontAssets.MouseText.Value.MeasureString(FuelLabel.Value) * 0.62f;
            Utils.DrawBorderString(sb, FuelLabel.Value,
                new Vector2(doorRect.Center.X - labelSize.X * 0.5f, doorRect.Y - 20), TextDim * alpha, 0.62f);

            //凹槽膛体
            IndustrialTerminalRenderer.DrawRecess(sb, doorRect, alpha, 0.8f);

            //膛内火光:燃烧时的琥珀辉,随燃烧进度沉降
            if (burning) {
                float burnLife = 1f - ThermalData.BurnProgress * 0.35f;
                float flicker = MathF.Sin(animTimer * 9f) * 0.5f + 0.5f;
                float glow = burnLife * (0.55f + flicker * 0.25f);
                Vector2 hearth = new(doorRect.Center.X, doorRect.Bottom - 18);
                SvgPathPen.SoftDot(sb, hearth, 34f, new Color(255, 130, 45), alpha * glow * 0.5f);
                SvgPathPen.SoftDot(sb, hearth, 16f, new Color(255, 190, 90), alpha * glow * 0.4f);
                //膛底一线炭红
                sb.Draw(px, new Rectangle(doorRect.X + 4, doorRect.Bottom - 5, doorRect.Width - 8, 2), src,
                    new Color(200, 80, 30) * (alpha * glow * 0.8f));
            }

            //燃料本体
            if (ThermalData.FuelItem != null && ThermalData.FuelItem.type != ItemID.None) {
                Main.instance.LoadItem(ThermalData.FuelItem.type);
                VaultUtils.SimpleDrawItem(sb, ThermalData.FuelItem.type, doorRect.Center.ToVector2(), 52, 1f, 0,
                    Color.White * alpha);

                if (ThermalData.FuelItem.stack > 1) {
                    string stackText = ThermalData.FuelItem.stack.ToString();
                    Vector2 stackSize = FontAssets.ItemStack.Value.MeasureString(stackText);
                    Utils.DrawBorderStringFourWay(sb, FontAssets.ItemStack.Value, stackText,
                        doorRect.Right - stackSize.X * 0.8f - 8, doorRect.Bottom - stackSize.Y * 0.8f - 8,
                        Color.White * alpha, Color.Black * alpha, new Vector2(0.3f), 0.8f);
                }
            }

            //黄铜门框:四边框线 + 左侧两枚铰链记号 + 右侧门把
            float hoverGlow = hoveringFuelSlot ? 0.5f : 0f;
            Color frame = Color.Lerp(Brass, BrassBright, hoverGlow) * (alpha * 0.9f);
            sb.Draw(px, new Rectangle(doorRect.X - 2, doorRect.Y - 2, doorRect.Width + 4, 2), src, frame);
            sb.Draw(px, new Rectangle(doorRect.X - 2, doorRect.Bottom, doorRect.Width + 4, 2), src, frame * 0.8f);
            sb.Draw(px, new Rectangle(doorRect.X - 2, doorRect.Y, 2, doorRect.Height), src, frame * 0.9f);
            sb.Draw(px, new Rectangle(doorRect.Right, doorRect.Y, 2, doorRect.Height), src, frame * 0.9f);
            sb.Draw(px, new Rectangle(doorRect.X - 4, doorRect.Y + 12, 4, 10), src, Brass * (alpha * 0.85f));
            sb.Draw(px, new Rectangle(doorRect.X - 4, doorRect.Bottom - 22, 4, 10), src, Brass * (alpha * 0.85f));
            sb.Draw(px, new Rectangle(doorRect.Right, doorRect.Center.Y - 7, 3, 14), src, BrassBright * (alpha * 0.8f));

            //门下炉条:燃烧进度刻度条
            if (burning) {
                IndustrialTerminalRenderer.DrawTickBar(sb, burnBarRect, 1f - ThermalData.BurnProgress,
                    Color.Lerp(Amber, new Color(255, 120, 50), 0.4f), alpha);
            }
        }

        /// <summary>双表盘:温度表(危险区红弧)与电力表</summary>
        private void DrawGauges(SpriteBatch sb) {
            float alpha = uiFadeAlpha;
            ThermalData data = ThermalData;
            float tempRatio = MathHelper.Clamp(data.Temperature / data.MaxTemperature, 0f, 1f);

            //燃烧时指针微颤
            float jitter = data.IsBurning ? MathF.Sin(animTimer * 34f) * 0.006f : 0f;

            Color tempAccent = Color.Lerp(Amber, WarnRed, MathF.Max(0f, tempRatio - 0.55f) / 0.45f);
            IndustrialTerminalRenderer.DrawGauge(sb, tempGaugeCenter, 36f, tempDisplay + jitter,
                tempAccent, alpha, TemperatureLabel.Value, $"{(int)data.Temperature}{TemperatureUnit.Value}",
                dangerFrom: 0.8f);

            IndustrialTerminalRenderer.DrawGauge(sb, powerGaugeCenter, 36f, powerDisplay + jitter,
                Amber, alpha, PowerLabel.Value, $"{(int)(MathHelper.Clamp(data.UEvalue / data.MaxUEValue, 0f, 1f) * 100f)}%");
        }

        /// <summary>状态灯 + 效率蚀刻读数 + 投料提示</summary>
        private void DrawStatusRow(SpriteBatch sb) {
            float alpha = uiFadeAlpha;
            ThermalData data = ThermalData;
            float x = panelRect.X + 34;
            float y = panelRect.Y + 232;

            bool isRunning = data.IsBurning || data.Temperature > 0;
            Color lampColor = data.IsBurning ? Amber : isRunning ? Color.Lerp(Amber, TextDim, 0.5f) : TextDim;
            float lampBright = data.IsBurning
                ? MathF.Sin(animTimer * 2.6f) * 0.2f + 0.72f
                : isRunning ? 0.4f : 0.15f;
            IndustrialTerminalRenderer.DrawLamp(sb, new Vector2(x + 7, y + 9), lampColor, alpha, lampBright);

            string state = isRunning ? ActiveText.Value : IdleText.Value;
            Utils.DrawBorderString(sb, state, new Vector2(x + 21, y + 1),
                Color.Lerp(TextMain, lampColor, 0.35f) * alpha, 0.66f);

            //效率读数:随炉温呈色
            if (data.Temperature > 0) {
                float efficiency = data.CurrentEfficiency;
                string effText = string.Format(EfficiencyText.Value, (int)(efficiency * 100));
                Color effColor = Color.Lerp(TextDim, Amber, efficiency);
                Utils.DrawBorderString(sb, effText, new Vector2(panelRect.X + 158, y + 1), effColor * alpha, 0.64f);
            }

            //投料提示:悬停炉门时在底缘右侧呼吸(左侧让给插座行)
            if (hoveringFuelSlot) {
                string hint = InsertFuelHint.Value;
                Vector2 hintSize = FontAssets.MouseText.Value.MeasureString(hint) * 0.62f;
                float blink = MathF.Sin(animTimer * 6f) * 0.3f + 0.7f;
                Utils.DrawBorderString(sb, hint,
                    new Vector2(panelRect.Right - hintSize.X - 34, panelRect.Bottom - 26),
                    Color.Lerp(TextDim, Amber, 0.5f) * (alpha * blink), 0.62f);
            }
        }

        private void DrawHoverTips(SpriteBatch sb) {
            if (isDragging) {
                return;
            }
            if (GeneratorTP != null && GeneratorTP.ModuleSlotCount > 0
                && socketStrip.DrawHoverTip(sb, GeneratorTP.ModuleRack, GeneratorTP.ModuleSlotCount,
                    MousePosition.ToPoint(), (text, color) => ShowTip(sb, text, color))) {
                return;
            }
            if (hoveringTempGauge) {
                ShowTip(sb, $"{(int)ThermalData.Temperature}/{(int)ThermalData.MaxTemperature}{TemperatureUnit.Value}");
            }
            else if (hoveringPowerGauge) {
                ShowTip(sb, $"{(int)ThermalData.UEvalue}/{(int)ThermalData.MaxUEValue} {PowerUnit.Value}");
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
