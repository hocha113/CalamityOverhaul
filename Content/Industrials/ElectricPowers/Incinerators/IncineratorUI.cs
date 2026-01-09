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

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Incinerators
{
    /// <summary>
    /// 焚烧炉交互UI
    /// </summary>
    internal class IncineratorUI : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "UI.Incinerator";

        //面板尺寸
        private const float PanelWidth = 380f;
        private const float PanelHeight = 320f;

        //动画变量
        private float scanLineTimer = 0f;
        private float heatGlow = 0f;
        private float heatPulse = 0f;
        private float dataStream = 0f;
        private float rustGridPhase = 0f;
        private float powerFlowTimer = 0f;

        //粒子系统
        private readonly List<EmberPRT> embers = new();
        private int emberSpawnTimer = 0;
        private readonly List<AshPRT> ashes = new();
        private int ashSpawnTimer = 0;

        //UI淡入淡出
        private float uiFadeAlpha = 0f;
        private float targetAlpha = 0f;

        //拖拽功能
        private bool isDragging = false;
        private Vector2 dragOffset = Vector2.Zero;

        //鼠标交互
        private Rectangle panelRect;
        private Rectangle inputSlotRect;
        private Rectangle outputSlotRect;
        private Rectangle progressBarRect;
        private Rectangle powerBarRect;
        private bool hoveringInputSlot = false;
        private bool hoveringOutputSlot = false;
        private bool hoveringProgressBar = false;
        private bool hoveringPowerBar = false;

        //关联的TP
        internal IncineratorTP CurrentTP;
        internal bool IsActive;
        public override bool Active => IsActive;

        private IncineratorData IncData => CurrentTP?.IncData;

        //本地化文本
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

        /// <summary>
        /// 与焚烧炉交互
        /// </summary>
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
            SoundEngine.PlaySound(Common.CWRSound.ButtonZero with { Volume = 0.3f, Pitch = -0.5f });
        }

        public override void Update() {
            //处理拖拽
            HandleDragging();

            //限制面板位置在屏幕内
            DrawPosition.X = MathHelper.Clamp(DrawPosition.X, PanelWidth / 2 + 10, Main.screenWidth - PanelWidth / 2 - 10);
            DrawPosition.Y = MathHelper.Clamp(DrawPosition.Y, PanelHeight / 2 + 10, Main.screenHeight - PanelHeight / 2 - 10);

            //更新动画计时器
            scanLineTimer += 0.03f;
            heatGlow += 0.04f;
            heatPulse += 0.015f;
            dataStream += 0.035f;
            rustGridPhase += 0.01f;
            powerFlowTimer += 0.05f;

            if (scanLineTimer > MathHelper.TwoPi) scanLineTimer -= MathHelper.TwoPi;
            if (heatGlow > MathHelper.TwoPi) heatGlow -= MathHelper.TwoPi;
            if (heatPulse > MathHelper.TwoPi) heatPulse -= MathHelper.TwoPi;
            if (dataStream > MathHelper.TwoPi) dataStream -= MathHelper.TwoPi;
            if (rustGridPhase > MathHelper.TwoPi) rustGridPhase -= MathHelper.TwoPi;
            if (powerFlowTimer > MathHelper.TwoPi) powerFlowTimer -= MathHelper.TwoPi;

            //更新UI透明度
            targetAlpha = IsActive ? 1f : 0f;
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

            //计算面板区域
            Vector2 topLeft = DrawPosition - new Vector2(PanelWidth / 2, PanelHeight / 2);
            panelRect = new Rectangle((int)topLeft.X, (int)topLeft.Y, (int)PanelWidth, (int)PanelHeight);

            //计算子区域
            inputSlotRect = new Rectangle((int)(topLeft.X + 50), (int)(topLeft.Y + 85), 70, 70);
            outputSlotRect = new Rectangle((int)(topLeft.X + 260), (int)(topLeft.Y + 85), 70, 70);
            progressBarRect = new Rectangle((int)(topLeft.X + 140), (int)(topLeft.Y + 95), 100, 50);
            powerBarRect = new Rectangle((int)(topLeft.X + 50), (int)(topLeft.Y + 195), 280, 35);

            //鼠标交互检测
            hoveringInputSlot = inputSlotRect.Contains(MouseHitBox) && !isDragging;
            hoveringOutputSlot = outputSlotRect.Contains(MouseHitBox) && !isDragging;
            hoveringProgressBar = progressBarRect.Contains(MouseHitBox) && !isDragging;
            hoveringPowerBar = powerBarRect.Contains(MouseHitBox) && !isDragging;
            hoverInMainPage = panelRect.Contains(MouseHitBox);

            if (hoverInMainPage) {
                player.mouseInterface = true;
            }

            //输入槽交互
            if (hoveringInputSlot && IncData != null) {
                if (IncData.InputItem != null && !IncData.InputItem.IsAir) {
                    Main.HoverItem = IncData.InputItem.Clone();
                    Main.hoverItemName = IncData.InputItem.Name;
                }

                if (keyLeftPressState == KeyPressState.Pressed) {
                    CurrentTP.HandleInputItem();
                }
            }

            //输出槽交互
            if (hoveringOutputSlot && IncData != null) {
                if (IncData.OutputItem != null && !IncData.OutputItem.IsAir) {
                    Main.HoverItem = IncData.OutputItem.Clone();
                    Main.hoverItemName = IncData.OutputItem.Name;
                }

                if (keyLeftPressState == KeyPressState.Pressed) {
                    CurrentTP.HandleOutputItem();
                }
            }

            //更新粒子
            UpdateParticles();
        }

        private void HandleDragging() {
            Vector2 mousePos = new Vector2(Main.mouseX, Main.mouseY);

            if (panelRect.Contains(mousePos.ToPoint()) && !hoveringInputSlot && !hoveringOutputSlot
                && !hoveringProgressBar && !hoveringPowerBar
                && keyLeftPressState == KeyPressState.Pressed && !isDragging) {
                isDragging = true;
                dragOffset = DrawPosition - mousePos;
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.3f });
            }

            if (isDragging) {
                DrawPosition = mousePos + dragOffset;
                if (keyLeftPressState == KeyPressState.Released) {
                    isDragging = false;
                    SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.3f });
                }
            }
        }

        private void UpdateParticles() {
            if (uiFadeAlpha < 0.3f) {
                return;
            }

            Vector2 panelCenter = DrawPosition;

            //余烬粒子(焚烧时更多)
            emberSpawnTimer++;
            int spawnRate = IncData?.IsWorking == true ? 3 : 8;
            if (emberSpawnTimer >= spawnRate && embers.Count < 30) {
                emberSpawnTimer = 0;
                float xPos = Main.rand.NextFloat(panelCenter.X - PanelWidth / 2 + 40, panelCenter.X + PanelWidth / 2 - 40);
                Vector2 startPos = new(xPos, panelCenter.Y + PanelHeight / 2 - 25);
                embers.Add(new EmberPRT(startPos));
            }
            for (int i = embers.Count - 1; i >= 0; i--) {
                if (embers[i].Update()) {
                    embers.RemoveAt(i);
                }
            }

            //灰烬粒子
            ashSpawnTimer++;
            if (ashSpawnTimer >= 10 && ashes.Count < 20) {
                ashSpawnTimer = 0;
                float xPos = Main.rand.NextFloat(panelCenter.X - PanelWidth / 2 + 40, panelCenter.X + PanelWidth / 2 - 40);
                Vector2 startPos = new(xPos, panelCenter.Y + PanelHeight / 2 - 20);
                ashes.Add(new AshPRT(startPos));
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

        public override void Draw(SpriteBatch spriteBatch) {
            if (uiFadeAlpha < 0.01f) {
                return;
            }
            if (IncData == null) {
                return;
            }

            //绘制主面板
            DrawMainPanel(spriteBatch);

            //绘制粒子
            foreach (var ash in ashes) {
                ash.Draw(spriteBatch, uiFadeAlpha * 0.5f);
            }
            foreach (var ember in embers) {
                ember.Draw(spriteBatch, uiFadeAlpha * 0.9f);
            }

            //绘制UI元素
            DrawInputSlot(spriteBatch);
            DrawOutputSlot(spriteBatch);
            DrawProgressBar(spriteBatch);
            DrawPowerBar(spriteBatch);
            DrawStatusText(spriteBatch);
        }

        private void DrawMainPanel(SpriteBatch sb) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float alpha = uiFadeAlpha;

            //主背景渐变
            int segments = 40;
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                float t2 = (i + 1) / (float)segments;
                int y1 = panelRect.Y + (int)(t * panelRect.Height);
                int y2 = panelRect.Y + (int)(t2 * panelRect.Height);
                Rectangle r = new(panelRect.X, y1, panelRect.Width, Math.Max(1, y2 - y1));

                //焚化炉色调：深灰、暗橙、锈红
                Color darkBase = new Color(15, 10, 8);
                Color rustMid = new Color(30, 18, 12);
                Color heatGlow_color = new Color(50, 28, 18);

                float pulse = (float)Math.Sin(heatPulse * 0.5f + t * 1.5f) * 0.5f + 0.5f;
                Color baseColor = Color.Lerp(darkBase, rustMid, pulse);
                Color finalColor = Color.Lerp(baseColor, heatGlow_color, t * 0.3f);
                finalColor *= alpha * 0.9f;

                sb.Draw(px, r, new Rectangle(0, 0, 1, 1), finalColor);
            }

            //热量覆盖层
            if (IncData.IsWorking) {
                float flicker = (float)Math.Sin(heatGlow * 1.5f) * 0.5f + 0.5f;
                Color heatOverlay = new Color(60, 25, 15) * (alpha * 0.25f * flicker);
                sb.Draw(px, panelRect, new Rectangle(0, 0, 1, 1), heatOverlay);
            }

            //锈蚀网格
            DrawRustGrid(sb, panelRect, alpha * 0.6f);

            //扫描线
            DrawScanLines(sb, panelRect, alpha * 0.7f);

            //边框
            float innerPulse = (float)Math.Sin(heatPulse * 1.1f) * 0.5f + 0.5f;
            DrawFrame(sb, panelRect, alpha, innerPulse);

            //标题
            string title = TitleText.Value;
            Vector2 titlePos = new Vector2(panelRect.Center.X, panelRect.Y + 28);
            Vector2 titleSize = FontAssets.MouseText.Value.MeasureString(title) * 0.9f;

            Color glowColor = new Color(255, 120, 60) * (alpha * 0.5f);
            for (int i = 0; i < 4; i++) {
                float angle = MathHelper.TwoPi * i / 4f;
                Vector2 offset = angle.ToRotationVector2() * 2f;
                Utils.DrawBorderString(sb, title, titlePos - titleSize / 2 + offset, glowColor, 0.9f);
            }
            Utils.DrawBorderString(sb, title, titlePos - titleSize / 2, new Color(230, 190, 150) * alpha, 0.9f);
        }

        private void DrawRustGrid(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            int gridRows = 10;
            float rowHeight = rect.Height / (float)gridRows;

            for (int row = 0; row < gridRows; row++) {
                float t = row / (float)gridRows;
                float y = rect.Y + row * rowHeight;
                float phase = rustGridPhase + t * MathHelper.Pi * 0.6f;
                float brightness = (float)Math.Sin(phase) * 0.5f + 0.5f;

                Color gridColor = new Color(70, 35, 20) * (alpha * 0.04f * brightness);
                sb.Draw(px, new Rectangle(rect.X + 15, (int)y, rect.Width - 30, 1), new Rectangle(0, 0, 1, 1), gridColor);
            }
        }

        private void DrawScanLines(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float scanY = rect.Y + (float)Math.Sin(scanLineTimer) * 0.5f * rect.Height + rect.Height * 0.5f;

            for (int i = -2; i <= 2; i++) {
                float offsetY = scanY + i * 4f;
                if (offsetY < rect.Y || offsetY > rect.Bottom) {
                    continue;
                }

                float intensity = 1f - Math.Abs(i) * 0.3f;
                Color scanColor = new Color(160, 80, 45) * (alpha * 0.1f * intensity);
                int thickness = i == 0 ? 2 : 1;
                sb.Draw(px, new Rectangle(rect.X + 12, (int)offsetY, rect.Width - 24, thickness), new Rectangle(0, 0, 1, 1), scanColor);
            }
        }

        private void DrawFrame(SpriteBatch sb, Rectangle rect, float alpha, float pulse) {
            Texture2D px = VaultAsset.placeholder2.Value;

            Color rustEdge = Color.Lerp(new Color(130, 65, 35), new Color(190, 100, 55), pulse) * (alpha * 0.7f);
            sb.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 4), new Rectangle(0, 0, 1, 1), rustEdge);
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 4, rect.Width, 4), new Rectangle(0, 0, 1, 1), rustEdge * 0.7f);
            sb.Draw(px, new Rectangle(rect.X, rect.Y, 4, rect.Height), new Rectangle(0, 0, 1, 1), rustEdge * 0.8f);
            sb.Draw(px, new Rectangle(rect.Right - 4, rect.Y, 4, rect.Height), new Rectangle(0, 0, 1, 1), rustEdge * 0.8f);

            Rectangle inner = rect;
            inner.Inflate(-8, -8);
            Color innerGlow = new Color(180, 90, 45) * (alpha * 0.15f * pulse);
            sb.Draw(px, new Rectangle(inner.X, inner.Y, inner.Width, 2), new Rectangle(0, 0, 1, 1), innerGlow);
            sb.Draw(px, new Rectangle(inner.X, inner.Bottom - 2, inner.Width, 2), new Rectangle(0, 0, 1, 1), innerGlow * 0.6f);
            sb.Draw(px, new Rectangle(inner.X, inner.Y, 2, inner.Height), new Rectangle(0, 0, 1, 1), innerGlow * 0.75f);
            sb.Draw(px, new Rectangle(inner.Right - 2, inner.Y, 2, inner.Height), new Rectangle(0, 0, 1, 1), innerGlow * 0.75f);
        }

        private void DrawInputSlot(SpriteBatch sb) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float alpha = uiFadeAlpha;
            float hoverGlow = hoveringInputSlot ? 0.35f : 0f;

            //背景
            Color slotBg = new Color(20, 14, 10) * (alpha * 0.9f);
            sb.Draw(px, inputSlotRect, new Rectangle(0, 0, 1, 1), slotBg);

            //边框
            Color edgeColor = Color.Lerp(new Color(110, 60, 35), new Color(170, 100, 55), (float)Math.Sin(heatPulse * 1.2f) * 0.5f + 0.5f) * (alpha * (0.7f + hoverGlow));
            sb.Draw(px, new Rectangle(inputSlotRect.X, inputSlotRect.Y, inputSlotRect.Width, 3), new Rectangle(0, 0, 1, 1), edgeColor);
            sb.Draw(px, new Rectangle(inputSlotRect.X, inputSlotRect.Bottom - 3, inputSlotRect.Width, 3), new Rectangle(0, 0, 1, 1), edgeColor);
            sb.Draw(px, new Rectangle(inputSlotRect.X, inputSlotRect.Y, 3, inputSlotRect.Height), new Rectangle(0, 0, 1, 1), edgeColor);
            sb.Draw(px, new Rectangle(inputSlotRect.Right - 3, inputSlotRect.Y, 3, inputSlotRect.Height), new Rectangle(0, 0, 1, 1), edgeColor);

            //标签
            string label = InputLabel.Value;
            Vector2 labelSize = FontAssets.MouseText.Value.MeasureString(label) * 0.7f;
            Vector2 labelPos = new Vector2(inputSlotRect.Center.X - labelSize.X / 2, inputSlotRect.Y - 22);
            Utils.DrawBorderString(sb, label, labelPos, new Color(230, 190, 150) * alpha, 0.7f);

            //绘制物品
            if (IncData.InputItem != null && !IncData.InputItem.IsAir) {
                Main.instance.LoadItem(IncData.InputItem.type);
                VaultUtils.SimpleDrawItem(sb, IncData.InputItem.type, inputSlotRect.Center.ToVector2(), 45, 1f, 0, Color.White * alpha);

                if (IncData.InputItem.stack > 1) {
                    string stackText = IncData.InputItem.stack.ToString();
                    Vector2 stackSize = FontAssets.ItemStack.Value.MeasureString(stackText);
                    Utils.DrawBorderStringFourWay(sb, FontAssets.ItemStack.Value, stackText,
                        inputSlotRect.Right - stackSize.X * 0.8f - 8, inputSlotRect.Bottom - stackSize.Y * 0.8f - 8,
                        Color.White * alpha, Color.Black * alpha, new Vector2(0.3f), 0.8f);
                }
            }
        }

        private void DrawOutputSlot(SpriteBatch sb) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float alpha = uiFadeAlpha;
            float hoverGlow = hoveringOutputSlot ? 0.35f : 0f;

            //背景
            Color slotBg = new Color(20, 14, 10) * (alpha * 0.9f);
            sb.Draw(px, outputSlotRect, new Rectangle(0, 0, 1, 1), slotBg);

            //边框(输出槽用金色调)
            Color edgeColor = Color.Lerp(new Color(130, 100, 40), new Color(200, 160, 70), (float)Math.Sin(heatPulse * 1.2f) * 0.5f + 0.5f) * (alpha * (0.7f + hoverGlow));
            sb.Draw(px, new Rectangle(outputSlotRect.X, outputSlotRect.Y, outputSlotRect.Width, 3), new Rectangle(0, 0, 1, 1), edgeColor);
            sb.Draw(px, new Rectangle(outputSlotRect.X, outputSlotRect.Bottom - 3, outputSlotRect.Width, 3), new Rectangle(0, 0, 1, 1), edgeColor);
            sb.Draw(px, new Rectangle(outputSlotRect.X, outputSlotRect.Y, 3, outputSlotRect.Height), new Rectangle(0, 0, 1, 1), edgeColor);
            sb.Draw(px, new Rectangle(outputSlotRect.Right - 3, outputSlotRect.Y, 3, outputSlotRect.Height), new Rectangle(0, 0, 1, 1), edgeColor);

            //标签
            string label = OutputLabel.Value;
            Vector2 labelSize = FontAssets.MouseText.Value.MeasureString(label) * 0.7f;
            Vector2 labelPos = new Vector2(outputSlotRect.Center.X - labelSize.X / 2, outputSlotRect.Y - 22);
            Utils.DrawBorderString(sb, label, labelPos, new Color(230, 210, 150) * alpha, 0.7f);

            //绘制物品
            if (IncData.OutputItem != null && !IncData.OutputItem.IsAir) {
                Main.instance.LoadItem(IncData.OutputItem.type);
                VaultUtils.SimpleDrawItem(sb, IncData.OutputItem.type, outputSlotRect.Center.ToVector2(), 45, 1f, 0, Color.White * alpha);

                if (IncData.OutputItem.stack > 1) {
                    string stackText = IncData.OutputItem.stack.ToString();
                    Vector2 stackSize = FontAssets.ItemStack.Value.MeasureString(stackText);
                    Utils.DrawBorderStringFourWay(sb, FontAssets.ItemStack.Value, stackText,
                        outputSlotRect.Right - stackSize.X * 0.8f - 8, outputSlotRect.Bottom - stackSize.Y * 0.8f - 8,
                        Color.White * alpha, Color.Black * alpha, new Vector2(0.3f), 0.8f);
                }
            }
        }

        private void DrawProgressBar(SpriteBatch sb) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float alpha = uiFadeAlpha;

            //箭头区域背景
            Color bgColor = new Color(15, 10, 8) * (alpha * 0.7f);
            sb.Draw(px, progressBarRect, new Rectangle(0, 0, 1, 1), bgColor);

            //进度条
            float progress = IncData.SmeltingProgress / (float)IncData.MaxSmeltingProgress;
            progress = MathHelper.Clamp(progress, 0f, 1f);

            int barWidth = progressBarRect.Width - 10;
            int fillWidth = (int)(barWidth * progress);
            Rectangle barBg = new Rectangle(progressBarRect.X + 5, progressBarRect.Center.Y - 8, barWidth, 16);
            Rectangle barFill = new Rectangle(barBg.X, barBg.Y, fillWidth, 16);

            //背景条
            sb.Draw(px, barBg, new Rectangle(0, 0, 1, 1), new Color(30, 20, 15) * alpha);

            //填充条
            if (fillWidth > 0) {
                Color fillColor = Color.Lerp(new Color(180, 80, 40), new Color(255, 140, 60), (float)Math.Sin(powerFlowTimer * 2f) * 0.5f + 0.5f);
                sb.Draw(px, barFill, new Rectangle(0, 0, 1, 1), fillColor * alpha);

                //发光效果
                Color glowColor = new Color(255, 180, 100) * (alpha * 0.3f);
                sb.Draw(px, new Rectangle(barFill.Right - 4, barFill.Y, 4, barFill.Height), new Rectangle(0, 0, 1, 1), glowColor);
            }

            //边框
            Color borderColor = new Color(100, 55, 30) * (alpha * 0.8f);
            sb.Draw(px, new Rectangle(barBg.X, barBg.Y, barBg.Width, 2), new Rectangle(0, 0, 1, 1), borderColor);
            sb.Draw(px, new Rectangle(barBg.X, barBg.Bottom - 2, barBg.Width, 2), new Rectangle(0, 0, 1, 1), borderColor);
            sb.Draw(px, new Rectangle(barBg.X, barBg.Y, 2, barBg.Height), new Rectangle(0, 0, 1, 1), borderColor);
            sb.Draw(px, new Rectangle(barBg.Right - 2, barBg.Y, 2, barBg.Height), new Rectangle(0, 0, 1, 1), borderColor);
        }

        private void DrawPowerBar(SpriteBatch sb) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float alpha = uiFadeAlpha;
            float hoverGlow = hoveringPowerBar ? 0.3f : 0f;

            //背景
            Color barBg = new Color(18, 12, 10) * (alpha * 0.9f);
            sb.Draw(px, powerBarRect, new Rectangle(0, 0, 1, 1), barBg);

            //电量填充
            float powerRatio = MathHelper.Clamp(IncData.UEvalue / IncData.MaxUE, 0f, 1f);
            int fillWidth = (int)((powerBarRect.Width - 10) * powerRatio);
            Rectangle fillRect = new Rectangle(powerBarRect.X + 5, powerBarRect.Y + 5, fillWidth, powerBarRect.Height - 10);

            if (fillWidth > 0) {
                //渐变填充
                int fillSegments = Math.Max(1, fillWidth / 6);
                for (int i = 0; i < fillSegments; i++) {
                    float t = i / (float)fillSegments;
                    float t2 = (i + 1) / (float)fillSegments;

                    int x1 = fillRect.X + (int)(t * fillRect.Width);
                    int x2 = fillRect.X + (int)(t2 * fillRect.Width);
                    Rectangle segRect = new(x1, fillRect.Y, Math.Max(1, x2 - x1), fillRect.Height);

                    Color powerLow = new Color(100, 70, 35);
                    Color powerHigh = new Color(200, 150, 70);
                    Color color = Color.Lerp(powerLow, powerHigh, t);

                    float pulse = (float)Math.Sin(powerFlowTimer * 2f + t * 4f) * 0.25f + 0.75f;
                    sb.Draw(px, segRect, new Rectangle(0, 0, 1, 1), color * (alpha * pulse));
                }
            }

            //边框
            Color edgeColor = Color.Lerp(new Color(110, 60, 35), new Color(160, 95, 50), (float)Math.Sin(heatPulse * 1.2f) * 0.5f + 0.5f) * (alpha * (0.7f + hoverGlow));
            sb.Draw(px, new Rectangle(powerBarRect.X, powerBarRect.Y, powerBarRect.Width, 3), new Rectangle(0, 0, 1, 1), edgeColor);
            sb.Draw(px, new Rectangle(powerBarRect.X, powerBarRect.Bottom - 3, powerBarRect.Width, 3), new Rectangle(0, 0, 1, 1), edgeColor);
            sb.Draw(px, new Rectangle(powerBarRect.X, powerBarRect.Y, 3, powerBarRect.Height), new Rectangle(0, 0, 1, 1), edgeColor);
            sb.Draw(px, new Rectangle(powerBarRect.Right - 3, powerBarRect.Y, 3, powerBarRect.Height), new Rectangle(0, 0, 1, 1), edgeColor);

            //标签和数值
            string label = PowerLabel.Value;
            Vector2 labelPos = new Vector2(powerBarRect.X, powerBarRect.Y - 20);
            Utils.DrawBorderString(sb, label, labelPos, new Color(230, 190, 150) * alpha, 0.65f);

            string powerText = $"{(int)IncData.UEvalue}/{(int)IncData.MaxUE} {PowerUnit.Value}";
            Vector2 powerTextSize = FontAssets.MouseText.Value.MeasureString(powerText) * 0.65f;
            Vector2 powerTextPos = new Vector2(powerBarRect.Right - powerTextSize.X, powerBarRect.Y - 20);
            Utils.DrawBorderString(sb, powerText, powerTextPos, new Color(200, 180, 140) * alpha, 0.65f);
        }

        private void DrawStatusText(SpriteBatch sb) {
            float alpha = uiFadeAlpha;

            //状态区域
            Vector2 statusPos = new Vector2(panelRect.X + 50, panelRect.Y + 250);

            //状态标签
            string statusLabel = StatusLabel.Value;
            Utils.DrawBorderString(sb, statusLabel, statusPos, new Color(200, 170, 130) * alpha, 0.7f);

            //状态值
            string statusText;
            Color statusColor;
            if (IncData.UEvalue < IncData.UEPerTick) {
                statusText = NoPowerText.Value;
                statusColor = new Color(180, 100, 80);
            }
            else if (IncData.IsWorking) {
                statusText = SmeltingText.Value;
                statusColor = Color.Lerp(new Color(255, 160, 80), new Color(255, 200, 120), (float)Math.Sin(powerFlowTimer * 3f) * 0.5f + 0.5f);
            }
            else {
                statusText = IdleText.Value;
                statusColor = new Color(150, 140, 120);
            }

            Vector2 statusValuePos = statusPos + new Vector2(FontAssets.MouseText.Value.MeasureString(statusLabel).X * 0.7f + 10, 0);
            Utils.DrawBorderString(sb, statusText, statusValuePos, statusColor * alpha, 0.7f);

            //操作提示
            string hint = "";
            if (hoveringInputSlot) {
                hint = InputHint.Value;
            }
            else if (hoveringOutputSlot && IncData.OutputItem != null && !IncData.OutputItem.IsAir) {
                hint = OutputHint.Value;
            }

            if (!string.IsNullOrEmpty(hint)) {
                Vector2 hintPos = new Vector2(panelRect.Center.X, panelRect.Bottom - 25);
                Vector2 hintSize = FontAssets.MouseText.Value.MeasureString(hint) * 0.6f;
                float blink = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 8f) * 0.35f + 0.65f;
                Color hintColor = new Color(230, 170, 110) * (alpha * blink);
                Utils.DrawBorderString(sb, hint, hintPos - hintSize / 2, hintColor, 0.6f);
            }
        }
    }
}
