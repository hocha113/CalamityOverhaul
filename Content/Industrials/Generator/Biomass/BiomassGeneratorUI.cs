using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Industrials.MachineModules;
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

namespace CalamityOverhaul.Content.Industrials.Generator.Biomass
{
    /// <summary>
    /// 生物质发电机面板:料仓 + 电力表盘 + 燃烧进度 + 状态灯 + 模块插座行。
    /// 结构镜像热电面板但没有温度模型,笔刷与材质在 <see cref="IndustrialTerminalRenderer"/>
    /// </summary>
    internal class BiomassGeneratorUI : BaseGeneratorUI, ILocalizedModType
    {
        public string LocalizationCategory => "UI.Generator";

        #region 布局与状态
        private const float PanelWidth = 400f;
        private const float PanelHeight = 300f;

        private static Color TextMain => IndustrialTerminalRenderer.TextMain;
        private static Color TextDim => IndustrialTerminalRenderer.TextDim;
        private static Color Amber => IndustrialTerminalRenderer.Amber;
        private static Color Brass => IndustrialTerminalRenderer.Brass;
        private static Color BrassBright => IndustrialTerminalRenderer.BrassBright;
        private static Color Accent => BiomassGenerator.Tint;

        private static float UIScreenW => PlayerInput.RealScreenWidth / Main.UIScale;
        private static float UIScreenH => PlayerInput.RealScreenHeight / Main.UIScale;

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
        private Rectangle doorRect;
        private Rectangle burnBarRect;
        private Rectangle closeRect;
        private Vector2 powerGaugeCenter;
        private Rectangle powerGaugeRect;
        private bool hoveringFuelSlot;
        private bool hoveringPowerGauge;
        private bool hoveringSockets;

        //模块插座行(点击/校验/红闪/绘制在共享件里)
        private readonly ModuleSocketStrip socketStrip = new();

        private BiomassData BiomassData => GeneratorTP?.MachineData as BiomassData;
        #endregion

        #region 本地化
        protected static LocalizedText TitleText;
        protected static LocalizedText FuelLabel;
        protected static LocalizedText PowerLabel;
        protected static LocalizedText ActiveText;
        protected static LocalizedText IdleText;
        protected static LocalizedText InsertFuelHint;
        protected static LocalizedText PowerUnit;

        public override Texture2D Texture => VaultAsset.placeholder2.Value;

        public override void SetStaticDefaults() {
            TitleText = this.GetLocalization(nameof(TitleText), () => "生物质发电机");
            FuelLabel = this.GetLocalization(nameof(FuelLabel), () => "料仓");
            PowerLabel = this.GetLocalization(nameof(PowerLabel), () => "电力");
            ActiveText = this.GetLocalization(nameof(ActiveText), () => "运行中");
            IdleText = this.GetLocalization(nameof(IdleText), () => "待机");
            InsertFuelHint = this.GetLocalization(nameof(InsertFuelHint), () => "点击放入/取出生物质燃料");
            PowerUnit = this.GetLocalization(nameof(PowerUnit), () => "UE");
        }
        #endregion

        public override void UpdateElement() {
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

            //电力指针的欠阻尼弹簧
            BiomassData data = BiomassData;
            float powerTarget = data != null ? MathHelper.Clamp(data.UEvalue / data.MaxUEValue, 0f, 1f) : 0f;
            powerVel = powerVel * 0.80f + (powerTarget - powerDisplay) * 0.05f;
            powerDisplay += powerVel;

            socketStrip.Update();

            Point mouse = MousePosition.ToPoint();
            hoveringFuelSlot = doorRect.Contains(mouse) && !isDragging;
            hoveringPowerGauge = powerGaugeRect.Contains(mouse) && !isDragging;
            hoveringSockets = socketStrip.Contains(mouse) && !isDragging;
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

            //模块插座行点击(先于拖拽捕获)
            if (keyLeftPressState == KeyPressState.Pressed && hoveringSockets && GeneratorTP != null) {
                socketStrip.HandleClick(mouse, GeneratorTP.ModuleRack, GeneratorTP.ModuleSlotCount,
                    player, () => GeneratorTP.SendData());
            }

            //背景区拖拽,避开料仓/表盘/插座/闩钮
            if (keyLeftPressState == KeyPressState.Pressed && hoverInMainPage
                && !hoveringFuelSlot && !hoveringPowerGauge && !hoveringSockets
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

            if (hoveringFuelSlot && BiomassData != null) {
                if (!BiomassData.FuelItem.IsAir) {
                    Main.HoverItem = BiomassData.FuelItem.Clone();
                    Main.hoverItemName = BiomassData.FuelItem.Name;
                }

                if (keyLeftPressState == KeyPressState.Pressed) {
                    if (GeneratorTP is BiomassGeneratorTP generator) {
                        generator.HandlerItem();
                    }
                }
            }
        }

        private void ComputeLayout() {
            Vector2 topLeft = DrawPosition - new Vector2(PanelWidth / 2, PanelHeight / 2);
            panelRect = new Rectangle((int)topLeft.X, (int)topLeft.Y, (int)PanelWidth, (int)PanelHeight);
            closeRect = new Rectangle(panelRect.Right - 40, panelRect.Y + 9, 26, 26);
            doorRect = new Rectangle(panelRect.X + 30, panelRect.Y + 80, 92, 92);
            burnBarRect = new Rectangle(panelRect.X + 30, panelRect.Y + 180, 92, 8);
            powerGaugeCenter = new Vector2(panelRect.X + 280, panelRect.Y + 126);
            powerGaugeRect = new Rectangle((int)powerGaugeCenter.X - 36, (int)powerGaugeCenter.Y - 36, 72, 72);
            //插座行:状态行下方左侧
            socketStrip.Layout(panelRect.X + 34, panelRect.Y + 240,
                GeneratorTP?.ModuleSlotCount ?? 0, 44, 10);
        }

        public override void OnEnterWorld() => IsActive = false;

        public override void SaveUIData(TagCompound tag) {
            tag["BiomassGeneratorUI_DrawPos_X"] = DrawPosition.X;
            tag["BiomassGeneratorUI_DrawPos_Y"] = DrawPosition.Y;
        }

        public override void LoadUIData(TagCompound tag) {
            if (tag.TryGet("BiomassGeneratorUI_DrawPos_X", out float x)) {
                DrawPosition.X = x;
            }
            if (tag.TryGet("BiomassGeneratorUI_DrawPos_Y", out float y)) {
                DrawPosition.Y = y;
            }
        }

        public override void RightClickByTile(bool newTP) {
            //手持生物质时右键是直投燃料,不开关面板
            Item item = Main.LocalPlayer.GetItem();
            if (!item.IsAir && BiomassFuel.IsBiomass(item.type)) {
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
            if (uiFadeAlpha < 0.01f || BiomassData == null) {
                return;
            }

            DrawShell(spriteBatch);
            DrawFuelDoor(spriteBatch);
            DrawGaugeAndStatus(spriteBatch);
            DrawSockets(spriteBatch);
            DrawHoverTips(spriteBatch);
        }

        /// <summary>钢壳 + 铆钉 + 铭牌 + 闩钮</summary>
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

        /// <summary>料仓:凹槽床 + 黄铜门框,燃烧时膛内透出苔绿荧光</summary>
        private void DrawFuelDoor(SpriteBatch sb) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float alpha = uiFadeAlpha;
            Rectangle src = new(0, 0, 1, 1);
            bool burning = BiomassData.IsBurning;

            Vector2 labelSize = FontAssets.MouseText.Value.MeasureString(FuelLabel.Value) * 0.62f;
            Utils.DrawBorderString(sb, FuelLabel.Value,
                new Vector2(doorRect.Center.X - labelSize.X * 0.5f, doorRect.Y - 20), TextDim * alpha, 0.62f);

            IndustrialTerminalRenderer.DrawRecess(sb, doorRect, alpha, 0.8f);

            //膛内绿荧:发酵燃烧的生物质光,随进度沉降
            if (burning) {
                float burnLife = 1f - BiomassData.BurnProgress * 0.35f;
                float flicker = MathF.Sin(animTimer * 7f) * 0.5f + 0.5f;
                float glow = burnLife * (0.5f + flicker * 0.25f);
                sb.Draw(px, new Rectangle(doorRect.X + 4, doorRect.Bottom - 5, doorRect.Width - 8, 2), src,
                    new Color(110, 200, 70) * (alpha * glow * 0.8f));
            }

            //燃料本体
            if (BiomassData.FuelItem != null && BiomassData.FuelItem.type != ItemID.None) {
                Main.instance.LoadItem(BiomassData.FuelItem.type);
                VaultUtils.SimpleDrawItem(sb, BiomassData.FuelItem.type, doorRect.Center.ToVector2(), 52, 1f, 0,
                    Color.White * alpha);

                if (BiomassData.FuelItem.stack > 1) {
                    string stackText = BiomassData.FuelItem.stack.ToString();
                    Vector2 stackSize = FontAssets.ItemStack.Value.MeasureString(stackText);
                    Utils.DrawBorderStringFourWay(sb, FontAssets.ItemStack.Value, stackText,
                        doorRect.Right - stackSize.X * 0.8f - 8, doorRect.Bottom - stackSize.Y * 0.8f - 8,
                        Color.White * alpha, Color.Black * alpha, new Vector2(0.3f), 0.8f);
                }
            }

            //黄铜门框
            float hoverGlow = hoveringFuelSlot ? 0.5f : 0f;
            Color frame = Color.Lerp(Brass, BrassBright, hoverGlow) * (alpha * 0.9f);
            sb.Draw(px, new Rectangle(doorRect.X - 2, doorRect.Y - 2, doorRect.Width + 4, 2), src, frame);
            sb.Draw(px, new Rectangle(doorRect.X - 2, doorRect.Bottom, doorRect.Width + 4, 2), src, frame * 0.8f);
            sb.Draw(px, new Rectangle(doorRect.X - 2, doorRect.Y, 2, doorRect.Height), src, frame * 0.9f);
            sb.Draw(px, new Rectangle(doorRect.Right, doorRect.Y, 2, doorRect.Height), src, frame * 0.9f);

            //门下炉条:燃烧进度刻度条
            if (burning) {
                IndustrialTerminalRenderer.DrawTickBar(sb, burnBarRect, 1f - BiomassData.BurnProgress,
                    Color.Lerp(Accent, Amber, 0.3f), alpha);
            }
        }

        /// <summary>电力表盘 + 状态灯 + 投料提示</summary>
        private void DrawGaugeAndStatus(SpriteBatch sb) {
            float alpha = uiFadeAlpha;
            BiomassData data = BiomassData;

            float jitter = data.IsBurning ? MathF.Sin(animTimer * 30f) * 0.005f : 0f;
            IndustrialTerminalRenderer.DrawGauge(sb, powerGaugeCenter, 36f, powerDisplay + jitter,
                Amber, alpha, PowerLabel.Value, $"{(int)(MathHelper.Clamp(data.UEvalue / data.MaxUEValue, 0f, 1f) * 100f)}%");

            //状态灯
            float x = panelRect.X + 34;
            float y = panelRect.Y + 210;
            Color lampColor = data.IsBurning ? Accent : TextDim;
            float lampBright = data.IsBurning
                ? MathF.Sin(animTimer * 2.6f) * 0.2f + 0.72f
                : 0.2f;
            IndustrialTerminalRenderer.DrawLamp(sb, new Vector2(x + 7, y + 9), lampColor, alpha, lampBright);

            string state = data.IsBurning ? ActiveText.Value : IdleText.Value;
            Utils.DrawBorderString(sb, state, new Vector2(x + 21, y + 1),
                Color.Lerp(TextMain, lampColor, 0.35f) * alpha, 0.66f);

            //投料提示
            if (hoveringFuelSlot) {
                string hint = InsertFuelHint.Value;
                Vector2 hintSize = FontAssets.MouseText.Value.MeasureString(hint) * 0.62f;
                float blink = MathF.Sin(animTimer * 6f) * 0.3f + 0.7f;
                Utils.DrawBorderString(sb, hint,
                    new Vector2(panelRect.Right - hintSize.X - 34, panelRect.Bottom - 26),
                    Color.Lerp(TextDim, Accent, 0.5f) * (alpha * blink), 0.62f);
            }
        }

        /// <summary>模块插座行</summary>
        private void DrawSockets(SpriteBatch sb) {
            if (GeneratorTP == null || GeneratorTP.ModuleSlotCount <= 0) {
                return;
            }
            float alpha = uiFadeAlpha;
            Utils.DrawBorderString(sb, MachineModuleText.SlotLabel.Value,
                new Vector2(panelRect.X + 34, panelRect.Y + 240 - 18), TextDim * alpha, 0.6f);
            socketStrip.Draw(sb, GeneratorTP.ModuleRack, GeneratorTP.ModuleSlotCount,
                alpha, MousePosition.ToPoint());
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
            if (hoveringPowerGauge) {
                ShowTip(sb, $"{(int)BiomassData.UEvalue}/{(int)BiomassData.MaxUEValue} {PowerUnit.Value}");
            }
        }

        private static void ShowTip(SpriteBatch sb, string text) => ShowTip(sb, text, TextMain);

        private static void ShowTip(SpriteBatch sb, string text, Color color) {
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
            Utils.DrawBorderString(sb, text, pos, color, 0.75f);
        }
        #endregion
    }
}
