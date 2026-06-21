using CalamityOverhaul.Common;
using CalamityOverhaul.Content.UIs.UIEffect;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Industrials.Generator.Thermal
{
    internal class ThermalGeneratorUI : BaseGeneratorUI, ILocalizedModType
    {
        public string LocalizationCategory => "UI.Generator";

        //面板尺寸
        private const float PanelWidth = 440f;
        private const float PanelHeight = 300f;
        private const int EdgePad = 8;

        //着色器动画时间
        private float shaderTime = 0f;

        //动画变量
        private float powerFlowTimer = 0f;
        private float sparkTimer = 0f;
        private float heatPulse = 0f;

        //粒子系统
        private readonly List<EmberPRT> embers = new();
        private int emberSpawnTimer = 0;
        private readonly List<AshPRT> ashes = new();
        private int ashSpawnTimer = 0;
        private readonly List<DraedonDataPRT> dataParticles = new();
        private int dataParticleTimer = 0;

        //UI淡入淡出
        private float uiFadeAlpha = 0f;
        private float targetAlpha = 0f;

        //拖拽
        private bool isDragging = false;
        private Vector2 dragOffset;
        private bool positionInitialized = false;

        //鼠标交互
        private Rectangle panelRect;
        private Rectangle fuelSlotRect;
        private Rectangle temperatureBarRect;
        private Rectangle powerBarRect;
        private bool hoveringFuelSlot = false;
        private bool hoveringTempBar = false;
        private bool hoveringPowerBar = false;

        private ThermalData ThermalData => GeneratorTP?.MachineData as ThermalData;

        //本地化文本
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

        public override void UpdateElement() {
            //首次使用时确保位置在屏幕内（防止LoadUIData在屏幕初始化前执行导致坐标为0）
            if (!positionInitialized && Main.screenWidth > 0) {
                positionInitialized = true;
                if (DrawPosition.X < PanelWidth / 2 + 10 && DrawPosition.Y < PanelHeight / 2 + 10) {
                    DrawPosition = new Vector2(Main.screenWidth / 2f, Main.screenHeight / 2f);
                }
            }

            //限制面板位置在屏幕内
            DrawPosition.X = MathHelper.Clamp(DrawPosition.X, PanelWidth / 2 + 10, Main.screenWidth - PanelWidth / 2 - 10);
            DrawPosition.Y = MathHelper.Clamp(DrawPosition.Y, PanelHeight / 2 + 10, Main.screenHeight - PanelHeight / 2 - 10);

            //更新动画计时器
            shaderTime += 0.016f;
            heatPulse += 0.018f;
            powerFlowTimer += 0.06f;
            sparkTimer += 0.095f;

            if (heatPulse > MathHelper.TwoPi) heatPulse -= MathHelper.TwoPi;
            if (powerFlowTimer > MathHelper.TwoPi) powerFlowTimer -= MathHelper.TwoPi;
            if (sparkTimer > MathHelper.TwoPi) sparkTimer -= MathHelper.TwoPi;

            //更新UI透明度
            targetAlpha = IsActive ? 1f : 0f;
            uiFadeAlpha = MathHelper.Lerp(uiFadeAlpha, targetAlpha, 0.15f);

            if (uiFadeAlpha < 0.01f && !IsActive) {
                return;
            }

            //计算面板区域
            Vector2 topLeft = DrawPosition - new Vector2(PanelWidth / 2, PanelHeight / 2);
            panelRect = new Rectangle((int)topLeft.X, (int)topLeft.Y, (int)PanelWidth, (int)PanelHeight);

            //计算子区域
            fuelSlotRect = new Rectangle((int)(topLeft.X + 45), (int)(topLeft.Y + 90), 90, 90);
            temperatureBarRect = new Rectangle((int)(topLeft.X + 180), (int)(topLeft.Y + 70), 45, 190);
            powerBarRect = new Rectangle((int)(topLeft.X + 355), (int)(topLeft.Y + 70), 45, 190);

            //鼠标交互检测
            hoveringFuelSlot = fuelSlotRect.Contains(MouseHitBox);
            hoveringTempBar = temperatureBarRect.Contains(MouseHitBox);
            hoveringPowerBar = powerBarRect.Contains(MouseHitBox);
            hoverInMainPage = panelRect.Contains(MouseHitBox);
            if (hoverInMainPage) {
                player.mouseInterface = true;
            }

            //左键拖拽（仅在面板背景区域生效，不与燃料槽等交互区域冲突）
            if (keyLeftPressState == KeyPressState.Pressed && hoverInMainPage
                && !hoveringFuelSlot && !hoveringTempBar && !hoveringPowerBar && !isDragging) {
                isDragging = true;
                dragOffset = new Vector2(Main.mouseX, Main.mouseY) - DrawPosition;
            }
            if (isDragging) {
                DrawPosition = new Vector2(Main.mouseX, Main.mouseY) - dragOffset;
                if (keyLeftPressState == KeyPressState.Released) {
                    isDragging = false;
                }
            }

            //燃料槽交互
            if (hoveringFuelSlot && !isDragging && ThermalData != null) {
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

            //更新粒子
            UpdateParticles();
        }

        private void UpdateParticles() {
            if (uiFadeAlpha < 0.3f) return;

            Vector2 panelCenter = DrawPosition;

            //余烬粒子
            emberSpawnTimer++;
            if (emberSpawnTimer >= 4 && embers.Count < 40) {
                emberSpawnTimer = 0;
                float xPos = Main.rand.NextFloat(panelCenter.X - PanelWidth / 2 + 50, panelCenter.X + PanelWidth / 2 - 50);
                Vector2 startPos = new(xPos, panelCenter.Y + PanelHeight / 2 - 30);
                embers.Add(new EmberPRT(startPos));
            }
            for (int i = embers.Count - 1; i >= 0; i--) {
                if (embers[i].Update()) {
                    embers.RemoveAt(i);
                }
            }

            //灰烬粒子
            ashSpawnTimer++;
            if (ashSpawnTimer >= 8 && ashes.Count < 30) {
                ashSpawnTimer = 0;
                float xPos = Main.rand.NextFloat(panelCenter.X - PanelWidth / 2 + 50, panelCenter.X + PanelWidth / 2 - 50);
                Vector2 startPos = new(xPos, panelCenter.Y + PanelHeight / 2 - 20);
                ashes.Add(new AshPRT(startPos));
            }
            for (int i = ashes.Count - 1; i >= 0; i--) {
                if (ashes[i].Update()) {
                    ashes.RemoveAt(i);
                }
            }

            //数据流粒子，这个...应该算废土风格的杂乱数据？
            dataParticleTimer++;
            if (dataParticleTimer >= 18 && dataParticles.Count < 15) {
                dataParticleTimer = 0;
                float xPos = Main.rand.NextFloat(panelCenter.X - PanelWidth / 2 + 40, panelCenter.X + PanelWidth / 2 - 40);
                Vector2 startPos = new(xPos, panelCenter.Y + Main.rand.NextFloat(-PanelHeight / 2 + 40, PanelHeight / 2 - 40));
                dataParticles.Add(new DraedonDataPRT(startPos));
            }
            for (int i = dataParticles.Count - 1; i >= 0; i--) {
                if (dataParticles[i].Update(panelCenter)) {
                    dataParticles.RemoveAt(i);
                }
            }

            //如果发电机正在燃烧，在燃料槽生成更多火花
            if (ThermalData != null && ThermalData.IsBurning) {
                if (Main.rand.NextBool(2)) {
                    float xPos = fuelSlotRect.Center.X + Main.rand.NextFloat(-30f, 30f);
                    Vector2 startPos = new(xPos, fuelSlotRect.Bottom - 10);
                    embers.Add(new EmberPRT(startPos));
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

        public override void Draw(SpriteBatch spriteBatch) {
            if (uiFadeAlpha < 0.01f) return;
            if (ThermalData == null) return;

            //绘制主面板
            DrawMainPanel(spriteBatch);

            //绘制粒子，灰烬在最底层
            foreach (var ash in ashes) {
                ash.Draw(spriteBatch, uiFadeAlpha * 0.6f);
            }
            foreach (var particle in dataParticles) {
                particle.Draw(spriteBatch, uiFadeAlpha * 0.5f);
            }
            foreach (var ember in embers) {
                ember.Draw(spriteBatch, uiFadeAlpha * 0.95f);
            }

            //绘制UI元素
            DrawFuelSlot(spriteBatch);
            DrawTemperatureBar(spriteBatch);
            DrawPowerBar(spriteBatch);
            DrawStatusText(spriteBatch);

            //提示框最后绘制，确保在最上层
            if (hoveringTempBar) {
                DrawBarTooltip(spriteBatch, $"{(int)ThermalData.Temperature}/{(int)ThermalData.MaxTemperature}{TemperatureUnit.Value}");
            }
            else if (hoveringPowerBar) {
                DrawBarTooltip(spriteBatch, $"{(int)ThermalData.UEvalue}/{(int)ThermalData.MaxUEValue} {PowerUnit.Value}");
            }
        }

        private void DrawMainPanel(SpriteBatch sb) {
            float alpha = uiFadeAlpha;
            if (EffectLoader.ThermalPanel?.Value != null) {
                DrawShaderMainPanel(sb, alpha);
            }
            else {
                DrawFallbackMainPanel(sb, alpha);
            }
            DrawTitleText(sb, alpha);
        }

        private void DrawShaderMainPanel(SpriteBatch sb, float alpha) {
            Effect effect = EffectLoader.ThermalPanel.Value;
            Texture2D px = VaultAsset.placeholder2.Value;

            Rectangle extRect = panelRect;
            extRect.Inflate(EdgePad, EdgePad);

            float tempRatio = ThermalData != null ? MathHelper.Clamp(ThermalData.Temperature / ThermalData.MaxTemperature, 0f, 1f) : 0f;
            float burnIntensity = ThermalData != null && ThermalData.IsBurning ? 1f - ThermalData.BurnProgress * 0.3f : 0f;

            effect.Parameters["uTime"]?.SetValue(shaderTime);
            effect.Parameters["uAlpha"]?.SetValue(alpha * 0.97f);
            effect.Parameters["uResolution"]?.SetValue(new Vector2(extRect.Width, extRect.Height));
            effect.Parameters["uEdgePad"]?.SetValue((float)EdgePad);
            effect.Parameters["uTemperature"]?.SetValue(tempRatio);
            effect.Parameters["uBurnIntensity"]?.SetValue(burnIntensity);

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.AnisotropicClamp, DepthStencilState.None,
                RasterizerState.CullNone, effect, Main.UIScaleMatrix);

            sb.Draw(px, extRect, Color.White);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.AnisotropicClamp, DepthStencilState.None,
                RasterizerState.CullNone, null, Main.UIScaleMatrix);
        }

        private void DrawFallbackMainPanel(SpriteBatch sb, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;

            //简化的渐变背景
            int segments = 20;
            float tempRatio = ThermalData != null ? MathHelper.Clamp(ThermalData.Temperature / ThermalData.MaxTemperature, 0f, 1f) : 0f;

            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                float t2 = (i + 1) / (float)segments;
                int y1 = panelRect.Y + (int)(t * panelRect.Height);
                int y2 = panelRect.Y + (int)(t2 * panelRect.Height);
                Rectangle r = new(panelRect.X, y1, panelRect.Width, Math.Max(1, y2 - y1));

                Color coldColor = new Color(12, 8, 8);
                Color hotColor = new Color(45, 22, 15);
                Color c = Color.Lerp(coldColor, hotColor, t * 0.4f + tempRatio * 0.3f);
                c *= alpha * 0.88f;
                sb.Draw(px, r, new Rectangle(0, 0, 1, 1), c);
            }

            //边框
            float pulse = (float)Math.Sin(heatPulse * 1.1f) * 0.5f + 0.5f;
            Color rustEdge = Color.Lerp(new Color(140, 70, 40), new Color(200, 110, 60), pulse) * (alpha * 0.75f);
            sb.Draw(px, new Rectangle(panelRect.X, panelRect.Y, panelRect.Width, 4), new Rectangle(0, 0, 1, 1), rustEdge);
            sb.Draw(px, new Rectangle(panelRect.X, panelRect.Bottom - 4, panelRect.Width, 4), new Rectangle(0, 0, 1, 1), rustEdge * 0.7f);
            sb.Draw(px, new Rectangle(panelRect.X, panelRect.Y, 4, panelRect.Height), new Rectangle(0, 0, 1, 1), rustEdge * 0.85f);
            sb.Draw(px, new Rectangle(panelRect.Right - 4, panelRect.Y, 4, panelRect.Height), new Rectangle(0, 0, 1, 1), rustEdge * 0.85f);

            //暗角
            for (int v = 0; v < 25; v += 3) {
                float fade = 1f - v / 25f;
                fade *= fade;
                Color vc = Color.Black * (alpha * 0.2f * fade);
                sb.Draw(px, new Rectangle(panelRect.X + v, panelRect.Y, 2, panelRect.Height), new Rectangle(0, 0, 1, 1), vc);
                sb.Draw(px, new Rectangle(panelRect.Right - v - 2, panelRect.Y, 2, panelRect.Height), new Rectangle(0, 0, 1, 1), vc);
            }
        }

        private void DrawTitleText(SpriteBatch sb, float alpha) {
            string title = TitleText.Value;
            Vector2 titlePos = new Vector2(panelRect.Center.X, panelRect.Y + 30);
            Vector2 titleSize = FontAssets.MouseText.Value.MeasureString(title) * 0.95f;

            Color glowColor = new Color(255, 140, 80) * (alpha * 0.6f);
            for (int i = 0; i < 4; i++) {
                float angle = MathHelper.TwoPi * i / 4f;
                Vector2 offset = angle.ToRotationVector2() * 2.5f;
                Utils.DrawBorderString(sb, title, titlePos - titleSize / 2 + offset, glowColor, 0.95f);
            }
            Utils.DrawBorderString(sb, title, titlePos - titleSize / 2, new Color(220, 180, 160) * alpha, 0.95f);
        }

        private void DrawFuelSlot(SpriteBatch sb) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float alpha = uiFadeAlpha;
            float hoverGlow = hoveringFuelSlot ? 0.4f : 0f;

            //背景，深色金属质感
            Color slotBg = new Color(18, 12, 10) * (alpha * 0.9f);
            sb.Draw(px, fuelSlotRect, new Rectangle(0, 0, 1, 1), slotBg);

            //边框，锈蚀金属
            Color edgeColor = Color.Lerp(new Color(120, 70, 40), new Color(180, 110, 60), (float)Math.Sin(heatPulse * 1.3f) * 0.5f + 0.5f) * (alpha * (0.75f + hoverGlow));
            sb.Draw(px, new Rectangle(fuelSlotRect.X, fuelSlotRect.Y, fuelSlotRect.Width, 4), new Rectangle(0, 0, 1, 1), edgeColor);
            sb.Draw(px, new Rectangle(fuelSlotRect.X, fuelSlotRect.Bottom - 4, fuelSlotRect.Width, 4), new Rectangle(0, 0, 1, 1), edgeColor);
            sb.Draw(px, new Rectangle(fuelSlotRect.X, fuelSlotRect.Y, 4, fuelSlotRect.Height), new Rectangle(0, 0, 1, 1), edgeColor);
            sb.Draw(px, new Rectangle(fuelSlotRect.Right - 4, fuelSlotRect.Y, 4, fuelSlotRect.Height), new Rectangle(0, 0, 1, 1), edgeColor);

            //标签
            string label = FuelLabel.Value;
            Vector2 labelSize = FontAssets.MouseText.Value.MeasureString(label) * 0.75f;
            Vector2 labelPos = new Vector2(fuelSlotRect.Center.X - labelSize.X / 2, fuelSlotRect.Y - 26);

            Color labelGlow = new Color(220, 140, 80) * (alpha * 0.5f);
            Utils.DrawBorderString(sb, label, labelPos + new Vector2(1.5f, 1.5f), labelGlow, 0.75f);
            Utils.DrawBorderString(sb, label, labelPos, new Color(240, 200, 160) * alpha, 0.75f);

            //绘制燃料物品
            if (ThermalData.FuelItem != null && ThermalData.FuelItem.type != ItemID.None) {
                Main.instance.LoadItem(ThermalData.FuelItem.type);
                VaultUtils.SimpleDrawItem(sb, ThermalData.FuelItem.type, fuelSlotRect.Center.ToVector2(), 55, 1f, 0, Color.White * alpha);

                if (ThermalData.FuelItem.stack > 1) {
                    string stackText = ThermalData.FuelItem.stack.ToString();
                    Vector2 stackSize = FontAssets.ItemStack.Value.MeasureString(stackText);
                    Utils.DrawBorderStringFourWay(sb, FontAssets.ItemStack.Value, stackText,
                        fuelSlotRect.Right - stackSize.X * 0.85f - 10, fuelSlotRect.Bottom - stackSize.Y * 0.85f - 10,
                        Color.White * alpha, Color.Black * alpha, new Vector2(0.3f), 0.85f);
                }
            }

            //燃烧指示器，火焰效果
            if (ThermalData.IsBurning) {
                float burnIntensity = 1f - ThermalData.BurnProgress * 0.3f;
                Color fireGlow = Color.Lerp(new Color(255, 100, 30), new Color(255, 180, 80), (float)Math.Sin(powerFlowTimer * 2.5f) * 0.5f + 0.5f);

                //多层火焰光晕
                for (int i = 0; i < 4; i++) {
                    float glowSize = 0.08f + i * 0.02f;
                    float layerAlpha = alpha * 0.2f * burnIntensity / (i + 1);
                    sb.Draw(CWRAsset.SoftGlow.Value, fuelSlotRect.Center.ToVector2(), null,
                        fireGlow with { A = 0 } * layerAlpha,
                        0f, CWRAsset.SoftGlow.Size() / 2, new Vector2(fuelSlotRect.Width * glowSize, fuelSlotRect.Height * glowSize), SpriteEffects.None, 0f);
                }

                //边缘火花
                float sparkIntensity = (float)Math.Sin(sparkTimer * 3f) * 0.5f + 0.5f;
                Color sparkColor = new Color(255, 200, 100) * (alpha * burnIntensity * sparkIntensity * 0.3f);
                sb.Draw(px, new Rectangle(fuelSlotRect.X - 2, fuelSlotRect.Y - 2, fuelSlotRect.Width + 4, fuelSlotRect.Height + 4),
                    new Rectangle(0, 0, 1, 1), sparkColor);

                //燃烧进度条（槽底部）
                int progressWidth = (int)(fuelSlotRect.Width * (1f - ThermalData.BurnProgress));
                if (progressWidth > 0) {
                    Color progressColor = Color.Lerp(new Color(255, 160, 60), new Color(180, 80, 30), ThermalData.BurnProgress) * (alpha * 0.7f);
                    sb.Draw(px, new Rectangle(fuelSlotRect.X, fuelSlotRect.Bottom - 6, progressWidth, 4),
                        new Rectangle(0, 0, 1, 1), progressColor);
                }
            }
        }

        private void DrawTemperatureBar(SpriteBatch sb) {
            float alpha = uiFadeAlpha;
            float tempRatio = MathHelper.Clamp(ThermalData.Temperature / ThermalData.MaxTemperature, 0f, 1f);

            if (EffectLoader.ThermalBar?.Value != null) {
                DrawShaderBar(sb, temperatureBarRect, tempRatio, 0f, alpha);
            }
            else {
                DrawFallbackBar(sb, temperatureBarRect, tempRatio, false, alpha);
            }

            //标签
            string label = TemperatureLabel.Value;
            Vector2 labelSize = FontAssets.MouseText.Value.MeasureString(label) * 0.65f;
            Vector2 labelPos = new Vector2(temperatureBarRect.Center.X - labelSize.X / 2, temperatureBarRect.Y - 26);

            Color labelGlow = new Color(220, 140, 80) * (alpha * 0.5f);
            Utils.DrawBorderString(sb, label, labelPos + new Vector2(1.5f, 1.5f), labelGlow, 0.65f);
            Utils.DrawBorderString(sb, label, labelPos, new Color(240, 200, 160) * alpha, 0.65f);

        }

        private void DrawPowerBar(SpriteBatch sb) {
            float alpha = uiFadeAlpha;
            float powerRatio = MathHelper.Clamp(ThermalData.UEvalue / ThermalData.MaxUEValue, 0f, 1f);

            if (EffectLoader.ThermalBar?.Value != null) {
                DrawShaderBar(sb, powerBarRect, powerRatio, 1f, alpha);
            }
            else {
                DrawFallbackBar(sb, powerBarRect, powerRatio, true, alpha);
            }

            //标签
            string label = PowerLabel.Value;
            Vector2 labelSize = FontAssets.MouseText.Value.MeasureString(label) * 0.75f;
            Vector2 labelPos = new Vector2(powerBarRect.Center.X - labelSize.X / 2, powerBarRect.Y - 26);

            Color labelGlow = new Color(220, 140, 80) * (alpha * 0.5f);
            Utils.DrawBorderString(sb, label, labelPos + new Vector2(1.5f, 1.5f), labelGlow, 0.75f);
            Utils.DrawBorderString(sb, label, labelPos, new Color(240, 200, 160) * alpha, 0.75f);

        }

        private void DrawShaderBar(SpriteBatch sb, Rectangle barRect, float fillRatio, float barMode, float alpha) {
            Effect effect = EffectLoader.ThermalBar.Value;
            Texture2D px = VaultAsset.placeholder2.Value;

            Rectangle extRect = barRect;
            extRect.Inflate(EdgePad / 2, EdgePad / 2);

            effect.Parameters["uTime"]?.SetValue(shaderTime);
            effect.Parameters["uAlpha"]?.SetValue(alpha);
            effect.Parameters["uResolution"]?.SetValue(new Vector2(extRect.Width, extRect.Height));
            effect.Parameters["uFillRatio"]?.SetValue(fillRatio);
            effect.Parameters["uBarMode"]?.SetValue(barMode);

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.AnisotropicClamp, DepthStencilState.None,
                RasterizerState.CullNone, effect, Main.UIScaleMatrix);

            sb.Draw(px, extRect, Color.White);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.AnisotropicClamp, DepthStencilState.None,
                RasterizerState.CullNone, null, Main.UIScaleMatrix);
        }

        private void DrawFallbackBar(SpriteBatch sb, Rectangle barRect, float fillRatio, bool isPower, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;

            //背景
            sb.Draw(px, barRect, new Rectangle(0, 0, 1, 1), new Color(18, 12, 10) * (alpha * 0.9f));

            //填充
            int maxFillHeight = barRect.Height - 14;
            int fillHeight = Math.Clamp((int)(maxFillHeight * fillRatio), 0, maxFillHeight);

            if (fillHeight > 0) {
                Rectangle fillRect = new Rectangle(barRect.X + 7, barRect.Bottom - fillHeight - 7, barRect.Width - 14, fillHeight);
                int segments = Math.Max(1, fillHeight / 4);

                Color low = isPower ? new Color(120, 80, 40) : new Color(80, 50, 30);
                Color high = isPower ? new Color(220, 160, 80) : new Color(255, 140, 60);

                for (int i = 0; i < segments; i++) {
                    float t = i / (float)segments;
                    float t2 = (i + 1) / (float)segments;
                    int y1 = fillRect.Y + (int)(t * fillRect.Height);
                    int y2 = fillRect.Y + (int)(t2 * fillRect.Height);
                    Rectangle segRect = new(fillRect.X, y1, fillRect.Width, Math.Max(1, y2 - y1));

                    Color c = Color.Lerp(low, high, 1f - t);
                    float pulse = (float)Math.Sin(powerFlowTimer * 2f + t * 4f) * 0.25f + 0.75f;
                    sb.Draw(px, segRect, new Rectangle(0, 0, 1, 1), c * (alpha * pulse));
                }
            }

            //边框
            float hoverGlow = 0f;
            Color edgeColor = new Color(140, 80, 45) * (alpha * (0.75f + hoverGlow));
            sb.Draw(px, new Rectangle(barRect.X, barRect.Y, barRect.Width, 3), new Rectangle(0, 0, 1, 1), edgeColor);
            sb.Draw(px, new Rectangle(barRect.X, barRect.Bottom - 3, barRect.Width, 3), new Rectangle(0, 0, 1, 1), edgeColor);
            sb.Draw(px, new Rectangle(barRect.X, barRect.Y, 3, barRect.Height), new Rectangle(0, 0, 1, 1), edgeColor);
            sb.Draw(px, new Rectangle(barRect.Right - 3, barRect.Y, 3, barRect.Height), new Rectangle(0, 0, 1, 1), edgeColor);
        }

        private static void DrawBarTooltip(SpriteBatch sb, string text) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float scale = 0.8f;
            Vector2 textSize = FontAssets.MouseText.Value.MeasureString(text) * scale;
            int padH = 16, padV = 12;
            int bw = (int)textSize.X + padH * 2;
            int bh = (int)textSize.Y + padV * 2;

            //确保不超出屏幕
            int tx = Main.mouseX + 20;
            int ty = Main.mouseY - bh - 8;
            if (tx + bw > Main.screenWidth - 4) tx = Main.screenWidth - bw - 4;
            if (ty < 4) ty = Main.mouseY + 22;

            Rectangle box = new Rectangle(tx, ty, bw, bh);
            float pulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3.5f) * 0.5f + 0.5f;

            //多层渐变背景，模拟金属深度
            for (int i = 0; i < 8; i++) {
                float t = i / 7f;
                Rectangle layer = box;
                layer.Inflate(-i, -i);
                if (layer.Width <= 0 || layer.Height <= 0) break;
                Color bg = Color.Lerp(new Color(35, 22, 16), new Color(12, 8, 6), t * t);
                sb.Draw(px, layer, new Rectangle(0, 0, 1, 1), bg * 0.95f);
            }

            //内侧暗角（上下渐暗）
            for (int v = 0; v < 12; v++) {
                float fade = 1f - v / 12f;
                fade *= fade * fade;
                Color vc = Color.Black * (0.18f * fade);
                Rectangle inner = new Rectangle(box.X + 4, box.Y + 4 + v, box.Width - 8, 1);
                sb.Draw(px, inner, new Rectangle(0, 0, 1, 1), vc);
                inner.Y = box.Bottom - 5 - v;
                sb.Draw(px, inner, new Rectangle(0, 0, 1, 1), vc);
            }

            //四面边框，锈蚀金属质感
            Color edgeBase = Color.Lerp(new Color(140, 75, 38), new Color(190, 110, 55), pulse * 0.3f);
            Color edgeDark = edgeBase * 0.6f;
            //顶边（亮）
            sb.Draw(px, new Rectangle(box.X + 1, box.Y, box.Width - 2, 2), new Rectangle(0, 0, 1, 1), edgeBase * 0.85f);
            //底边（暗）
            sb.Draw(px, new Rectangle(box.X + 1, box.Bottom - 2, box.Width - 2, 2), new Rectangle(0, 0, 1, 1), edgeDark * 0.7f);
            //左边
            sb.Draw(px, new Rectangle(box.X, box.Y + 1, 2, box.Height - 2), new Rectangle(0, 0, 1, 1), edgeBase * 0.75f);
            //右边
            sb.Draw(px, new Rectangle(box.Right - 2, box.Y + 1, 2, box.Height - 2), new Rectangle(0, 0, 1, 1), edgeDark * 0.65f);

            //内侧高光线（顶部和左侧各一条细线，模拟金属反光）
            Color innerHighlight = new Color(200, 140, 80) * 0.15f;
            sb.Draw(px, new Rectangle(box.X + 3, box.Y + 3, box.Width - 6, 1), new Rectangle(0, 0, 1, 1), innerHighlight);
            sb.Draw(px, new Rectangle(box.X + 3, box.Y + 4, 1, box.Height - 8), new Rectangle(0, 0, 1, 1), innerHighlight * 0.7f);

            //四角铆钉装饰
            Color rivetColor = Color.Lerp(new Color(160, 100, 55), new Color(200, 130, 70), pulse * 0.4f) * 0.9f;
            int rs = 3;
            sb.Draw(px, new Rectangle(box.X + 3, box.Y + 3, rs, rs), new Rectangle(0, 0, 1, 1), rivetColor);
            sb.Draw(px, new Rectangle(box.Right - 3 - rs, box.Y + 3, rs, rs), new Rectangle(0, 0, 1, 1), rivetColor);
            sb.Draw(px, new Rectangle(box.X + 3, box.Bottom - 3 - rs, rs, rs), new Rectangle(0, 0, 1, 1), rivetColor * 0.7f);
            sb.Draw(px, new Rectangle(box.Right - 3 - rs, box.Bottom - 3 - rs, rs, rs), new Rectangle(0, 0, 1, 1), rivetColor * 0.7f);

            //文字绘制：发光层 + 主体
            Vector2 textPos = new Vector2(box.X + padH, box.Y + padV);
            Color textGlow = new Color(255, 160, 80) * 0.35f;
            for (int i = 0; i < 4; i++) {
                float angle = MathHelper.TwoPi * i / 4f;
                Vector2 off = angle.ToRotationVector2() * 1.5f;
                Utils.DrawBorderString(sb, text, textPos + off, textGlow, scale);
            }
            Utils.DrawBorderString(sb, text, textPos, new Color(245, 215, 175), scale);
        }

        private void DrawStatusText(SpriteBatch sb) {
            float alpha = uiFadeAlpha;

            //中央信息面板
            Vector2 infoCenter = new Vector2(panelRect.Center.X, panelRect.Y + 175);

            //运行状态
            string statusLabel = StatusLabel.Value;
            Vector2 statusLabelPos = new Vector2(infoCenter.X - 95, infoCenter.Y);

            Color labelGlow = new Color(200, 120, 70) * (alpha * 0.5f);
            Utils.DrawBorderString(sb, statusLabel, statusLabelPos + new Vector2(1.2f, 1.2f), labelGlow, 0.75f);
            Utils.DrawBorderString(sb, statusLabel, statusLabelPos, new Color(220, 180, 140) * alpha, 0.75f);

            bool isRunning = ThermalData.IsBurning || ThermalData.Temperature > 0;
            string statusText = isRunning ? ActiveText.Value : IdleText.Value;
            Color statusColor = isRunning ? new Color(255, 180, 100) : new Color(160, 140, 120);
            Vector2 statusTextPos = new Vector2(infoCenter.X + 15, infoCenter.Y);

            if (isRunning) {
                float blink = (float)Math.Sin(powerFlowTimer * 5f) * 0.35f + 0.65f;
                statusColor *= blink;

                for (int i = 0; i < 3; i++) {
                    float glowAngle = MathHelper.TwoPi * i / 3f + powerFlowTimer;
                    Vector2 glowOffset = glowAngle.ToRotationVector2() * 2f;
                    Utils.DrawBorderString(sb, statusText, statusTextPos + glowOffset, statusColor * (alpha * 0.3f), 0.75f);
                }
            }

            Utils.DrawBorderString(sb, statusText, statusTextPos, statusColor * alpha, 0.75f);

            //效率指示（使用新的效率曲线）
            if (ThermalData.Temperature > 0) {
                float efficiency = ThermalData.CurrentEfficiency;
                string effText = string.Format(EfficiencyText.Value, (int)(efficiency * 100));
                Vector2 effPos = new Vector2(infoCenter.X, infoCenter.Y + 30);
                Vector2 effSize = FontAssets.MouseText.Value.MeasureString(effText) * 0.7f;

                Color effColor = Color.Lerp(new Color(180, 140, 100), new Color(255, 200, 120), efficiency);
                Color effGlow = effColor * (alpha * 0.4f);

                Utils.DrawBorderString(sb, effText, effPos - effSize / 2 + new Vector2(1.2f, 1.2f), effGlow, 0.7f);
                Utils.DrawBorderString(sb, effText, effPos - effSize / 2, effColor * alpha, 0.7f);
            }

            //操作提示
            if (hoveringFuelSlot) {
                string hint = InsertFuelHint.Value;
                Vector2 hintPos = new Vector2(panelRect.Center.X, panelRect.Bottom - 25);
                Vector2 hintSize = FontAssets.MouseText.Value.MeasureString(hint) * 0.65f;

                float blink = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 9f) * 0.4f + 0.6f;
                Color hintColor = new Color(240, 180, 120) * (alpha * blink);
                Color hintGlow = new Color(200, 140, 80) * (alpha * blink * 0.5f);

                Utils.DrawBorderString(sb, hint, hintPos - hintSize / 2 + new Vector2(1.5f, 1.5f), hintGlow, 0.65f);
                Utils.DrawBorderString(sb, hint, hintPos - hintSize / 2, hintColor, 0.65f);
            }
        }
    }
}
