using CalamityOverhaul.Common;
using CalamityOverhaul.Content.ADV.UIEffect;
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
        
        //动画变量
        private float scanLineTimer = 0f;
        private float emberGlow = 0f;
        private float heatPulse = 0f;
        private float dataStream = 0f;
        private float rustGridPhase = 0f;
        private float powerFlowTimer = 0f;
        private float sparkTimer = 0f;
        
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
        
        //拖拽功能
        private bool isDragging = false;
        private Vector2 dragOffset = Vector2.Zero;
        private Rectangle titleBarRect;
        
        //鼠标交互
        private Rectangle panelRect;
        private Rectangle fuelSlotRect;
        private Rectangle temperatureBarRect;
        private Rectangle powerBarRect;
        private bool hoveringFuelSlot = false;
        private bool hoveringTempBar = false;
        private bool hoveringPowerBar = false;
        private bool hoveringTitleBar = false;
        
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
            //处理拖拽
            HandleDragging();
            
            //限制面板位置在屏幕内
            DrawPosition.X = MathHelper.Clamp(DrawPosition.X, PanelWidth / 2 + 10, Main.screenWidth - PanelWidth / 2 - 10);
            DrawPosition.Y = MathHelper.Clamp(DrawPosition.Y, PanelHeight / 2 + 10, Main.screenHeight - PanelHeight / 2 - 10);

            //更新动画计时器
            scanLineTimer += 0.035f;
            emberGlow += 0.045f;
            heatPulse += 0.018f;
            dataStream += 0.042f;
            rustGridPhase += 0.012f;
            powerFlowTimer += 0.06f;
            sparkTimer += 0.095f;
            
            if (scanLineTimer > MathHelper.TwoPi) scanLineTimer -= MathHelper.TwoPi;
            if (emberGlow > MathHelper.TwoPi) emberGlow -= MathHelper.TwoPi;
            if (heatPulse > MathHelper.TwoPi) heatPulse -= MathHelper.TwoPi;
            if (dataStream > MathHelper.TwoPi) dataStream -= MathHelper.TwoPi;
            if (rustGridPhase > MathHelper.TwoPi) rustGridPhase -= MathHelper.TwoPi;
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
            
            //计算标题栏区域（用于拖拽）
            titleBarRect = new Rectangle(panelRect.X, panelRect.Y, panelRect.Width, 60);
            
            //计算子区域
            fuelSlotRect = new Rectangle((int)(topLeft.X + 45), (int)(topLeft.Y + 90), 90, 90);
            temperatureBarRect = new Rectangle((int)(topLeft.X + 180), (int)(topLeft.Y + 70), 45, 190);
            powerBarRect = new Rectangle((int)(topLeft.X + 355), (int)(topLeft.Y + 70), 45, 190);

            //鼠标交互检测
            hoveringFuelSlot = fuelSlotRect.Contains(MouseHitBox) && !isDragging;
            hoveringTempBar = temperatureBarRect.Contains(MouseHitBox) && !isDragging;
            hoveringPowerBar = powerBarRect.Contains(MouseHitBox) && !isDragging;
            hoveringTitleBar = titleBarRect.Contains(MouseHitBox) && !isDragging;
            hoverInMainPage = panelRect.Contains(MouseHitBox);
            if (hoverInMainPage) {
                player.mouseInterface = true;
            }

            //燃料槽交互
            if (hoveringFuelSlot && ThermalData != null) {
                if (!ThermalData.FuelItem.IsAir) {
                    Main.HoverItem = ThermalData.FuelItem.Clone();
                    Main.hoverItemName = ThermalData.FuelItem.Name;
                }

                if (keyLeftPressState == KeyPressState.Pressed) {
                    if (FuelItems.FuelItemToCombustion.ContainsKey(Main.mouseItem.type) || Main.mouseItem.type == ItemID.None) {
                        if (GeneratorTP is ThermalGeneratorTP thermal) {
                            thermal.HandlerItem();
                            SoundEngine.PlaySound(SoundID.Grab with { Volume = 0.4f, Pitch = -0.1f });
                        }
                    }
                }
            }

            //更新粒子
            UpdateParticles();
        }

        private void HandleDragging() {
            Vector2 mousePos = new Vector2(Main.mouseX, Main.mouseY);
            
            //开始拖拽
            if (panelRect.Contains(mousePos.ToPoint()) && !hoveringFuelSlot && !hoveringTempBar && !hoveringPowerBar
                && keyLeftPressState == KeyPressState.Pressed && !isDragging) {
                isDragging = true;
                dragOffset = DrawPosition - mousePos;
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.3f });
            }
            
            //执行拖拽
            if (isDragging) {
                DrawPosition = mousePos + dragOffset;
                if (keyLeftPressState == KeyPressState.Released) {
                    //结束拖拽
                    isDragging = false;
                    SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.3f });
                }
            }
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

            //如果发电机正在工作，在燃料槽生成更多火花
            if (ThermalData != null && ThermalData.TemperatureTransfer > 0) {
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
        }

        private void DrawMainPanel(SpriteBatch sb) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float alpha = uiFadeAlpha;

            //主背景渐变，废土深色调
            int segments = 45;
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                float t2 = (i + 1) / (float)segments;
                int y1 = panelRect.Y + (int)(t * panelRect.Height);
                int y2 = panelRect.Y + (int)(t2 * panelRect.Height);
                Rectangle r = new(panelRect.X, y1, panelRect.Width, Math.Max(1, y2 - y1));

                //废土色调：深灰、暗红、锈橙
                Color wastelandDark = new Color(12, 8, 8);
                Color rustMid = new Color(25, 15, 10);
                Color emberGlow_color = new Color(45, 22, 15);

                float pulse = (float)Math.Sin(heatPulse * 0.5f + t * 1.8f) * 0.5f + 0.5f;
                Color baseColor = Color.Lerp(wastelandDark, rustMid, pulse);
                Color finalColor = Color.Lerp(baseColor, emberGlow_color, t * 0.4f);
                finalColor *= alpha * 0.88f;

                sb.Draw(px, r, new Rectangle(0, 0, 1, 1), finalColor);
            }

            //热量闪烁覆盖层
            float flicker = (float)Math.Sin(emberGlow * 1.2f) * 0.5f + 0.5f;
            Color heatOverlay = new Color(40, 18, 10) * (alpha * 0.3f * flicker);
            sb.Draw(px, panelRect, new Rectangle(0, 0, 1, 1), heatOverlay);

            //锈蚀网格纹理
            DrawRustGrid(sb, panelRect, alpha * 0.7f);

            //扫描线效果，比嘉登的更粗糙一点
            DrawWastelandScanLines(sb, panelRect, alpha * 0.8f);

            //热量脉冲内发光
            float innerPulse = (float)Math.Sin(heatPulse * 1.1f) * 0.5f + 0.5f;
            Rectangle inner = panelRect;
            inner.Inflate(-10, -10);
            sb.Draw(px, inner, new Rectangle(0, 0, 1, 1), new Color(140, 60, 30) * (alpha * 0.08f * innerPulse));

            //废土边框
            DrawWastelandFrame(sb, panelRect, alpha, innerPulse);

            //标题文字
            string title = TitleText.Value;
            Vector2 titlePos = new Vector2(panelRect.Center.X, panelRect.Y + 30);
            Vector2 titleSize = FontAssets.MouseText.Value.MeasureString(title) * 0.95f;
            
            //发光描边
            Color glowColor = new Color(255, 140, 80) * (alpha * 0.6f);
            for (int i = 0; i < 4; i++) {
                float angle = MathHelper.TwoPi * i / 4f;
                Vector2 offset = angle.ToRotationVector2() * 2.5f;
                Utils.DrawBorderString(sb, title, titlePos - titleSize / 2 + offset, glowColor, 0.95f);
            }
            
            Utils.DrawBorderString(sb, title, titlePos - titleSize / 2, new Color(220, 180, 160) * alpha, 0.95f);
            
            //拖拽提示
            if (hoverInMainPage && !isDragging) {
                Color hintColor = new Color(200, 180, 140) * (alpha * 0.6f);
                string dragHint = "◈"; //拖拽图标
                Vector2 dragHintPos = new Vector2(panelRect.Right - 25, panelRect.Y + 15);
                Utils.DrawBorderString(sb, dragHint, dragHintPos, hintColor, 0.8f);
            }
        }

        private void DrawRustGrid(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            int gridRows = 12;
            float rowHeight = rect.Height / (float)gridRows;

            for (int row = 0; row < gridRows; row++) {
                float t = row / (float)gridRows;
                float y = rect.Y + row * rowHeight;
                float phase = rustGridPhase + t * MathHelper.Pi * 0.7f;
                float brightness = (float)Math.Sin(phase) * 0.5f + 0.5f;

                //锈色网格线
                Color gridColor = new Color(80, 40, 25) * (alpha * 0.05f * brightness);
                sb.Draw(px, new Rectangle(rect.X + 20, (int)y, rect.Width - 40, 1), new Rectangle(0, 0, 1, 1), gridColor);
                
                //随机杂点
                if (Main.rand.NextBool(5)) {
                    int spotX = rect.X + Main.rand.Next(20, rect.Width - 20);
                    sb.Draw(px, new Rectangle(spotX, (int)y, 2, 1), new Rectangle(0, 0, 1, 1), gridColor * 1.5f);
                }
            }
        }

        private void DrawWastelandScanLines(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float scanY = rect.Y + (float)Math.Sin(scanLineTimer) * 0.5f * rect.Height + rect.Height * 0.5f;

            //粗糙的扫描线
            for (int i = -3; i <= 3; i++) {
                float offsetY = scanY + i * 5f;
                if (offsetY < rect.Y || offsetY > rect.Bottom) continue;

                float intensity = 1f - Math.Abs(i) * 0.25f;
                Color scanColor = new Color(180, 90, 50) * (alpha * 0.12f * intensity);
                int thickness = i == 0 ? 3 : 2;
                sb.Draw(px, new Rectangle(rect.X + 15, (int)offsetY, rect.Width - 30, thickness), new Rectangle(0, 0, 1, 1), scanColor);
            }
        }

        private void DrawWastelandFrame(SpriteBatch sb, Rectangle rect, float alpha, float pulse) {
            Texture2D px = VaultAsset.placeholder2.Value;
            
            //外框，锈橙色
            Color rustEdge = Color.Lerp(new Color(140, 70, 40), new Color(200, 110, 60), pulse) * (alpha * 0.75f);
            sb.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 5), new Rectangle(0, 0, 1, 1), rustEdge);
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 5, rect.Width, 5), new Rectangle(0, 0, 1, 1), rustEdge * 0.7f);
            sb.Draw(px, new Rectangle(rect.X, rect.Y, 5, rect.Height), new Rectangle(0, 0, 1, 1), rustEdge * 0.85f);
            sb.Draw(px, new Rectangle(rect.Right - 5, rect.Y, 5, rect.Height), new Rectangle(0, 0, 1, 1), rustEdge * 0.85f);

            //内框发光
            Rectangle inner = rect;
            inner.Inflate(-10, -10);
            Color innerGlow = new Color(200, 100, 50) * (alpha * 0.18f * pulse);
            sb.Draw(px, new Rectangle(inner.X, inner.Y, inner.Width, 2), new Rectangle(0, 0, 1, 1), innerGlow);
            sb.Draw(px, new Rectangle(inner.X, inner.Bottom - 2, inner.Width, 2), new Rectangle(0, 0, 1, 1), innerGlow * 0.6f);
            sb.Draw(px, new Rectangle(inner.X, inner.Y, 2, inner.Height), new Rectangle(0, 0, 1, 1), innerGlow * 0.8f);
            sb.Draw(px, new Rectangle(inner.Right - 2, inner.Y, 2, inner.Height), new Rectangle(0, 0, 1, 1), innerGlow * 0.8f);

            //角落标记，废土警告标志
            DrawWastelandMark(sb, new Vector2(rect.X + 18, rect.Y + 18), alpha * 0.9f);
            DrawWastelandMark(sb, new Vector2(rect.Right - 18, rect.Y + 18), alpha * 0.9f);
            DrawWastelandMark(sb, new Vector2(rect.X + 18, rect.Bottom - 18), alpha * 0.6f);
            DrawWastelandMark(sb, new Vector2(rect.Right - 18, rect.Bottom - 18), alpha * 0.6f);
        }

        private static void DrawWastelandMark(SpriteBatch sb, Vector2 pos, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float size = 8f;
            Color markColor = new Color(200, 100, 50) * alpha;

            //警告三角形样式
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), markColor, 0f, new Vector2(0.5f, 0.5f), new Vector2(size * 1.3f, size * 0.25f), SpriteEffects.None, 0f);
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), markColor * 0.8f, MathHelper.PiOver2, new Vector2(0.5f, 0.5f), new Vector2(size * 1.3f, size * 0.25f), SpriteEffects.None, 0f);
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), markColor * 0.5f, MathHelper.PiOver4, new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.2f), SpriteEffects.None, 0f);
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
            if (ThermalData.TemperatureTransfer > 0) {
                float burnProgress = ThermalData.TemperatureTransfer / ThermalData.MaxTemperatureTransfer;
                Color fireGlow = Color.Lerp(new Color(255, 100, 30), new Color(255, 180, 80), (float)Math.Sin(powerFlowTimer * 2.5f) * 0.5f + 0.5f);
                
                //多层火焰光晕
                for (int i = 0; i < 4; i++) {
                    float glowSize = 0.08f + i * 0.02f;
                    float layerAlpha = alpha * 0.2f * burnProgress / (i + 1);
                    sb.Draw(CWRAsset.SoftGlow.Value, fuelSlotRect.Center.ToVector2(), null, 
                        fireGlow with { A = 0 } * layerAlpha, 
                        0f, CWRAsset.SoftGlow.Size() / 2, new Vector2(fuelSlotRect.Width * glowSize, fuelSlotRect.Height * glowSize), SpriteEffects.None, 0f);
                }
                
                //边缘火花
                float sparkIntensity = (float)Math.Sin(sparkTimer * 3f) * 0.5f + 0.5f;
                Color sparkColor = new Color(255, 200, 100) * (alpha * burnProgress * sparkIntensity * 0.3f);
                sb.Draw(px, new Rectangle(fuelSlotRect.X - 2, fuelSlotRect.Y - 2, fuelSlotRect.Width + 4, fuelSlotRect.Height + 4), 
                    new Rectangle(0, 0, 1, 1), sparkColor);
            }
        }

        private void DrawTemperatureBar(SpriteBatch sb) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float alpha = uiFadeAlpha;
            float hoverGlow = hoveringTempBar ? 0.4f : 0f;

            //背景
            Color barBg = new Color(18, 12, 10) * (alpha * 0.9f);
            sb.Draw(px, temperatureBarRect, new Rectangle(0, 0, 1, 1), barBg);

            //温度填充，添加边界检查
            float tempRatio = MathHelper.Clamp(ThermalData.Temperature / ThermalData.MaxTemperature, 0f, 1f);
            int maxFillHeight = temperatureBarRect.Height - 14; //减去上下边距（7*2）
            int fillHeight = (int)(maxFillHeight * tempRatio);
            
            //确保填充区域不超出边界
            fillHeight = Math.Min(fillHeight, maxFillHeight);
            fillHeight = Math.Max(fillHeight, 0);
            
            Rectangle fillRect = new Rectangle(
                temperatureBarRect.X + 7,
                temperatureBarRect.Bottom - fillHeight - 7,
                temperatureBarRect.Width - 14,
                fillHeight
            );

            if (fillHeight > 0) {
                //渐变色，从深橙到明黄
                int fillSegments = Math.Max(1, fillHeight / 4);
                for (int i = 0; i < fillSegments; i++) {
                    float t = i / (float)fillSegments;
                    float t2 = (i + 1) / (float)fillSegments;
                    
                    int y1 = fillRect.Y + (int)(t * fillRect.Height);
                    int y2 = fillRect.Y + (int)(t2 * fillRect.Height);
                    Rectangle segRect = new(fillRect.X, y1, fillRect.Width, Math.Max(1, y2 - y1));

                    Color lowTemp = new Color(80, 50, 30);
                    Color midTemp = new Color(180, 80, 40);
                    Color highTemp = new Color(255, 140, 60);
                    
                    Color color1 = Color.Lerp(lowTemp, midTemp, (1f - t) * tempRatio);
                    Color color2 = tempRatio > 0.6f ? Color.Lerp(color1, highTemp, (tempRatio - 0.6f) / 0.4f) : color1;
                    
                    float pulse = (float)Math.Sin(powerFlowTimer * 1.8f + t * 4f) * 0.25f + 0.75f;
                    sb.Draw(px, segRect, new Rectangle(0, 0, 1, 1), color2 * (alpha * pulse));
                }

                //热量波动效果
                if (tempRatio > 0.4f && fillHeight > 10) {
                    float waveY = fillRect.Bottom - (float)Math.Sin(powerFlowTimer * 2.2f) * Math.Min(8f, fillHeight * 0.1f);
                    if (waveY > fillRect.Y && waveY < fillRect.Bottom) {
                        Color heatWave = new Color(255, 180, 100) * (alpha * (tempRatio - 0.4f) * 0.5f);
                        sb.Draw(px, new Rectangle(fillRect.X, (int)waveY, fillRect.Width, 4), new Rectangle(0, 0, 1, 1), heatWave);
                    }
                }
            }

            //边框
            Color edgeColor = Color.Lerp(new Color(120, 70, 40), new Color(180, 110, 60), (float)Math.Sin(heatPulse * 1.3f) * 0.5f + 0.5f) * (alpha * (0.75f + hoverGlow));
            sb.Draw(px, new Rectangle(temperatureBarRect.X, temperatureBarRect.Y, temperatureBarRect.Width, 4), new Rectangle(0, 0, 1, 1), edgeColor);
            sb.Draw(px, new Rectangle(temperatureBarRect.X, temperatureBarRect.Bottom - 4, temperatureBarRect.Width, 4), new Rectangle(0, 0, 1, 1), edgeColor);
            sb.Draw(px, new Rectangle(temperatureBarRect.X, temperatureBarRect.Y, 4, temperatureBarRect.Height), new Rectangle(0, 0, 1, 1), edgeColor);
            sb.Draw(px, new Rectangle(temperatureBarRect.Right - 4, temperatureBarRect.Y, 4, temperatureBarRect.Height), new Rectangle(0, 0, 1, 1), edgeColor);

            //标签
            string label = TemperatureLabel.Value;
            Vector2 labelSize = FontAssets.MouseText.Value.MeasureString(label) * 0.65f;
            Vector2 labelPos = new Vector2(temperatureBarRect.Center.X - labelSize.X / 2, temperatureBarRect.Y - 26);
            
            Color labelGlow = new Color(220, 140, 80) * (alpha * 0.5f);
            Utils.DrawBorderString(sb, label, labelPos + new Vector2(1.5f, 1.5f), labelGlow, 0.65f);
            Utils.DrawBorderString(sb, label, labelPos, new Color(240, 200, 160) * alpha, 0.65f);

            //悬停时显示详细信息
            if (hoveringTempBar) {
                string tempText = $"{(int)ThermalData.Temperature}/{(int)ThermalData.MaxTemperature}{TemperatureUnit.Value}";
                Vector2 textSize = FontAssets.MouseText.Value.MeasureString(tempText) * 0.8f;
                Vector2 textPos = new Vector2(Main.mouseX + 18, Main.mouseY + 18);
                
                //工业风格提示框
                Rectangle tooltipBg = new Rectangle((int)textPos.X - 10, (int)textPos.Y - 6, (int)textSize.X + 20, (int)textSize.Y + 12);
                sb.Draw(px, tooltipBg, new Rectangle(0, 0, 1, 1), new Color(15, 10, 8) * 0.95f);
                sb.Draw(px, new Rectangle(tooltipBg.X, tooltipBg.Y, tooltipBg.Width, 3), new Rectangle(0, 0, 1, 1), new Color(180, 100, 50) * 0.8f);
                sb.Draw(px, new Rectangle(tooltipBg.X, tooltipBg.Y, 3, tooltipBg.Height), new Rectangle(0, 0, 1, 1), new Color(180, 100, 50) * 0.8f);
                
                Utils.DrawBorderString(sb, tempText, textPos, new Color(255, 220, 180), 0.8f);
            }
        }

        private void DrawPowerBar(SpriteBatch sb) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float alpha = uiFadeAlpha;
            float hoverGlow = hoveringPowerBar ? 0.4f : 0f;

            //背景
            Color barBg = new Color(18, 12, 10) * (alpha * 0.9f);
            sb.Draw(px, powerBarRect, new Rectangle(0, 0, 1, 1), barBg);

            //电力填充
            float powerRatio = MathHelper.Clamp(ThermalData.UEvalue / ThermalData.MaxUEValue, 0f, 1f);
            int maxFillHeight = powerBarRect.Height - 14; //减去上下边距（7*2）
            int fillHeight = (int)(maxFillHeight * powerRatio);
            
            //确保填充区域不超出边界
            fillHeight = Math.Min(fillHeight, maxFillHeight);
            fillHeight = Math.Max(fillHeight, 0);
            
            Rectangle fillRect = new Rectangle(
                powerBarRect.X + 7,
                powerBarRect.Bottom - fillHeight - 7,
                powerBarRect.Width - 14,
                fillHeight
            );

            if (fillHeight > 0) {
                //电力渐变色，琥珀色到橙黄色
                int fillSegments = Math.Max(1, fillHeight / 4);
                for (int i = 0; i < fillSegments; i++) {
                    float t = i / (float)fillSegments;
                    float t2 = (i + 1) / (float)fillSegments;
                    
                    int y1 = fillRect.Y + (int)(t * fillRect.Height);
                    int y2 = fillRect.Y + (int)(t2 * fillRect.Height);
                    Rectangle segRect = new(fillRect.X, y1, fillRect.Width, Math.Max(1, y2 - y1));

                    Color powerLow = new Color(120, 80, 40);
                    Color powerHigh = new Color(220, 160, 80);
                    Color color = Color.Lerp(powerLow, powerHigh, 1f - t);
                    
                    float pulse = (float)Math.Sin(powerFlowTimer * 2.5f - t * 5f) * 0.3f + 0.7f;
                    sb.Draw(px, segRect, new Rectangle(0, 0, 1, 1), color * (alpha * pulse));
                }

                //电力流动效果
                if (fillHeight > 10) {
                    float flowY = powerBarRect.Bottom - 7 - (float)Math.Sin(powerFlowTimer * 3.5f) * Math.Min(fillHeight * 0.8f, maxFillHeight * 0.8f);
                    for (int i = 0; i < 4; i++) {
                        float offsetY = flowY + i * 18f - 45f;
                        if (offsetY > fillRect.Y && offsetY < fillRect.Bottom) {
                            Color flowColor = new Color(255, 200, 120) * (alpha * 0.7f * (1f - i * 0.2f));
                            sb.Draw(px, new Rectangle(fillRect.X, (int)offsetY, fillRect.Width, 4), new Rectangle(0, 0, 1, 1), flowColor);
                        }
                    }
                }
            }

            //边框
            Color edgeColor = Color.Lerp(new Color(120, 70, 40), new Color(180, 110, 60), (float)Math.Sin(heatPulse * 1.3f) * 0.5f + 0.5f) * (alpha * (0.75f + hoverGlow));
            sb.Draw(px, new Rectangle(powerBarRect.X, powerBarRect.Y, powerBarRect.Width, 4), new Rectangle(0, 0, 1, 1), edgeColor);
            sb.Draw(px, new Rectangle(powerBarRect.X, powerBarRect.Bottom - 4, powerBarRect.Width, 4), new Rectangle(0, 0, 1, 1), edgeColor);
            sb.Draw(px, new Rectangle(powerBarRect.X, powerBarRect.Y, 4, powerBarRect.Height), new Rectangle(0, 0, 1, 1), edgeColor);
            sb.Draw(px, new Rectangle(powerBarRect.Right - 4, powerBarRect.Y, 4, powerBarRect.Height), new Rectangle(0, 0, 1, 1), edgeColor);

            //标签
            string label = PowerLabel.Value;
            Vector2 labelSize = FontAssets.MouseText.Value.MeasureString(label) * 0.75f;
            Vector2 labelPos = new Vector2(powerBarRect.Center.X - labelSize.X / 2, powerBarRect.Y - 26);
            
            Color labelGlow = new Color(220, 140, 80) * (alpha * 0.5f);
            Utils.DrawBorderString(sb, label, labelPos + new Vector2(1.5f, 1.5f), labelGlow, 0.75f);
            Utils.DrawBorderString(sb, label, labelPos, new Color(240, 200, 160) * alpha, 0.75f);

            //悬停时显示详细信息
            if (hoveringPowerBar) {
                string powerText = $"{(int)ThermalData.UEvalue}/{(int)ThermalData.MaxUEValue} {PowerUnit.Value}";
                Vector2 textSize = FontAssets.MouseText.Value.MeasureString(powerText) * 0.8f;
                Vector2 textPos = new Vector2(Main.mouseX + 18, Main.mouseY + 18);
                
                //工业风格提示框
                Rectangle tooltipBg = new Rectangle((int)textPos.X - 10, (int)textPos.Y - 6, (int)textSize.X + 20, (int)textSize.Y + 12);
                sb.Draw(px, tooltipBg, new Rectangle(0, 0, 1, 1), new Color(15, 10, 8) * 0.95f);
                sb.Draw(px, new Rectangle(tooltipBg.X, tooltipBg.Y, tooltipBg.Width, 3), new Rectangle(0, 0, 1, 1), new Color(180, 100, 50) * 0.8f);
                sb.Draw(px, new Rectangle(tooltipBg.X, tooltipBg.Y, 3, tooltipBg.Height), new Rectangle(0, 0, 1, 1), new Color(180, 100, 50) * 0.8f);
                
                Utils.DrawBorderString(sb, powerText, textPos, new Color(255, 220, 180), 0.8f);
            }
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

            bool isActive = ThermalData.TemperatureTransfer > 0;
            string statusText = isActive ? ActiveText.Value : IdleText.Value;
            Color statusColor = isActive ? new Color(255, 180, 100) : new Color(160, 140, 120);
            Vector2 statusTextPos = new Vector2(infoCenter.X + 15, infoCenter.Y);
            
            if (isActive) {
                float blink = (float)Math.Sin(powerFlowTimer * 5f) * 0.35f + 0.65f;
                statusColor *= blink;
                
                //活跃状态发光
                for (int i = 0; i < 3; i++) {
                    float glowAngle = MathHelper.TwoPi * i / 3f + powerFlowTimer;
                    Vector2 glowOffset = glowAngle.ToRotationVector2() * 2f;
                    Utils.DrawBorderString(sb, statusText, statusTextPos + glowOffset, statusColor * (alpha * 0.3f), 0.75f);
                }
            }
            
            Utils.DrawBorderString(sb, statusText, statusTextPos, statusColor * alpha, 0.75f);

            //效率指示
            if (ThermalData.Temperature > 0) {
                float efficiency = Math.Min(ThermalData.Temperature / ThermalData.MaxTemperature, 1f);
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
