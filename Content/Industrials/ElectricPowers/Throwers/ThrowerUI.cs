using CalamityOverhaul.Common;
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

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Throwers
{
    /// <summary>
    /// 投掷者UI
    /// 废土科技风格的控制面板
    /// </summary>
    internal class ThrowerUI : UIHandle, ILocalizedModType
    {
        //面板尺寸
        private const float PanelWidth = 420f;
        private const float PanelHeight = 380f;

        //动画变量
        private float scanLineTimer;
        private float pulseTimer;
        private float glowTimer;
        private float dataFlowTimer;
        private float warningFlashTimer;

        //粒子系统
        private readonly List<ThrowerSparkPRT> sparks = [];
        private int sparkSpawnTimer;
        private readonly List<ThrowerDataPRT> dataParticles = [];
        private int dataParticleTimer;

        public static ThrowerUI Instance => UIHandleLoader.GetUIHandleOfType<ThrowerUI>();
        public bool Open;
        public override bool Active => Open || uiFadeAlpha > 0;

        //UI淡入淡出
        private float uiFadeAlpha;

        //拖拽功能
        private bool isDragging;
        private Vector2 dragOffset;

        //面板区域
        private Rectangle panelRect;
        private Rectangle inventoryRect;
        private Rectangle controlRect;
        private Rectangle statusRect;

        //按钮区域
        private Rectangle toggleButton;
        private Rectangle ammoModeButton;
        private Rectangle speedUpButton;
        private Rectangle speedDownButton;
        private Rectangle angleUpButton;
        private Rectangle angleDownButton;
        private Rectangle intervalUpButton;
        private Rectangle intervalDownButton;
        private Rectangle dirLeftButton;
        private Rectangle dirRightButton;

        //悬停状态
        private bool hoveringPanel;
        private bool hoveringToggle;
        private bool hoveringAmmoMode;
        private bool hoveringSpeedUp;
        private bool hoveringSpeedDown;
        private bool hoveringAngleUp;
        private bool hoveringAngleDown;
        private bool hoveringIntervalUp;
        private bool hoveringIntervalDown;
        private bool hoveringDirLeft;
        private bool hoveringDirRight;
        private int hoveringSlot = -1;

        //本地化文本
        protected static LocalizedText TitleText;
        protected static LocalizedText InventoryLabel;
        protected static LocalizedText ControlLabel;
        protected static LocalizedText SpeedLabel;
        protected static LocalizedText AngleLabel;
        protected static LocalizedText IntervalLabel;
        protected static LocalizedText DirectionLabel;
        protected static LocalizedText StartText;
        protected static LocalizedText StopText;
        protected static LocalizedText StatusRunning;
        protected static LocalizedText StatusIdle;
        protected static LocalizedText StatusNoEnergy;
        protected static LocalizedText StatusNoItems;
        protected static LocalizedText EnergyLabel;
        protected static LocalizedText DropHint;
        protected static LocalizedText AmmoModeOnText;
        protected static LocalizedText AmmoModeOffText;
        protected static LocalizedText AmmoModeHint;

        internal ThrowerTP Station;

        public string LocalizationCategory => "UI";

        public override void SetStaticDefaults() {
            TitleText = this.GetLocalization(nameof(TitleText), () => "物品投掷器");
            InventoryLabel = this.GetLocalization(nameof(InventoryLabel), () => "存储仓");
            ControlLabel = this.GetLocalization(nameof(ControlLabel), () => "控制面板");
            SpeedLabel = this.GetLocalization(nameof(SpeedLabel), () => "投掷力度");
            AngleLabel = this.GetLocalization(nameof(AngleLabel), () => "散布角度");
            IntervalLabel = this.GetLocalization(nameof(IntervalLabel), () => "投掷间隔");
            DirectionLabel = this.GetLocalization(nameof(DirectionLabel), () => "投掷方向");
            StartText = this.GetLocalization(nameof(StartText), () => "启动");
            StopText = this.GetLocalization(nameof(StopText), () => "停止");
            StatusRunning = this.GetLocalization(nameof(StatusRunning), () => "运行中");
            StatusIdle = this.GetLocalization(nameof(StatusIdle), () => "待机");
            StatusNoEnergy = this.GetLocalization(nameof(StatusNoEnergy), () => "能量不足");
            StatusNoItems = this.GetLocalization(nameof(StatusNoItems), () => "无物品");
            EnergyLabel = this.GetLocalization(nameof(EnergyLabel), () => "能量");
            DropHint = this.GetLocalization(nameof(DropHint), () => "放入物品");
            AmmoModeOnText = this.GetLocalization(nameof(AmmoModeOnText), () => "弹药发射");
            AmmoModeOffText = this.GetLocalization(nameof(AmmoModeOffText), () => "物品投掷");
            AmmoModeHint = this.GetLocalization(nameof(AmmoModeHint), () => "弹药模式: 放入弹药时发射对应弹幕");
        }

        public void Initialize(ThrowerTP throwerTP) {
            if (Station != throwerTP) {
                Station = throwerTP;
                Open = true;
                DrawPosition = new Vector2(Main.screenWidth / 2, Main.screenHeight / 2);
            }
            else {
                Open = !Open;
            }
        }

        public override void Update() {
            //淡入淡出
            if (Open) {
                uiFadeAlpha = Math.Min(1f, uiFadeAlpha + 0.12f);
            }
            else {
                uiFadeAlpha = Math.Max(0f, uiFadeAlpha - 0.12f);
            }

            if (uiFadeAlpha < 0.01f) {
                return;
            }

            //检查有效性
            if (Open && (Station == null || !Station.Active || Station.PosInWorld.To(player.Center).Length() > 200)) {
                Open = false;
                SoundEngine.PlaySound(CWRSound.ButtonZero with { Pitch = -0.3f, Volume = 0.5f });
                return;
            }

            //处理拖拽
            HandleDragging();

            //限制面板位置
            DrawPosition.X = MathHelper.Clamp(DrawPosition.X, PanelWidth / 2 + 10, Main.screenWidth - PanelWidth / 2 - 10);
            DrawPosition.Y = MathHelper.Clamp(DrawPosition.Y, PanelHeight / 2 + 10, Main.screenHeight - PanelHeight / 2 - 10);

            //更新动画
            scanLineTimer += 0.035f;
            pulseTimer += 0.025f;
            glowTimer += 0.04f;
            dataFlowTimer += 0.05f;
            warningFlashTimer += 0.08f;

            if (scanLineTimer > MathHelper.TwoPi) scanLineTimer -= MathHelper.TwoPi;
            if (pulseTimer > MathHelper.TwoPi) pulseTimer -= MathHelper.TwoPi;
            if (glowTimer > MathHelper.TwoPi) glowTimer -= MathHelper.TwoPi;
            if (dataFlowTimer > MathHelper.TwoPi) dataFlowTimer -= MathHelper.TwoPi;
            if (warningFlashTimer > MathHelper.TwoPi) warningFlashTimer -= MathHelper.TwoPi;

            //计算面板区域
            Vector2 topLeft = DrawPosition - new Vector2(PanelWidth / 2, PanelHeight / 2);
            panelRect = new Rectangle((int)topLeft.X, (int)topLeft.Y, (int)PanelWidth, (int)PanelHeight);

            //计算子区域
            inventoryRect = new Rectangle(panelRect.X + 15, panelRect.Y + 55, 200, 170);
            controlRect = new Rectangle(panelRect.X + 225, panelRect.Y + 55, 180, 170);
            statusRect = new Rectangle(panelRect.X + 15, panelRect.Y + 245, panelRect.Width - 30, 120);

            //计算按钮区域
            int btnY = controlRect.Y + 25;
            int btnHeight = 22;
            int btnSpacing = 32;

            toggleButton = new Rectangle(statusRect.X + statusRect.Width - 90, statusRect.Y + 10, 80, 30);
            ammoModeButton = new Rectangle(statusRect.X + 120, statusRect.Y + 80, 100, 24);

            speedUpButton = new Rectangle(controlRect.Right - 55, btnY, 22, btnHeight);
            speedDownButton = new Rectangle(controlRect.Right - 30, btnY, 22, btnHeight);
            btnY += btnSpacing;

            angleUpButton = new Rectangle(controlRect.Right - 55, btnY, 22, btnHeight);
            angleDownButton = new Rectangle(controlRect.Right - 30, btnY, 22, btnHeight);
            btnY += btnSpacing;

            intervalUpButton = new Rectangle(controlRect.Right - 55, btnY, 22, btnHeight);
            intervalDownButton = new Rectangle(controlRect.Right - 30, btnY, 22, btnHeight);
            btnY += btnSpacing;

            dirLeftButton = new Rectangle(controlRect.Right - 55, btnY, 22, btnHeight);
            dirRightButton = new Rectangle(controlRect.Right - 30, btnY, 22, btnHeight);

            //鼠标交互检测
            Vector2 mousePos = new Vector2(Main.mouseX, Main.mouseY);
            hoveringPanel = panelRect.Contains(mousePos.ToPoint());
            hoveringToggle = toggleButton.Contains(mousePos.ToPoint()) && !isDragging;
            hoveringAmmoMode = ammoModeButton.Contains(mousePos.ToPoint()) && !isDragging;
            hoveringSpeedUp = speedUpButton.Contains(mousePos.ToPoint()) && !isDragging;
            hoveringSpeedDown = speedDownButton.Contains(mousePos.ToPoint()) && !isDragging;
            hoveringAngleUp = angleUpButton.Contains(mousePos.ToPoint()) && !isDragging;
            hoveringAngleDown = angleDownButton.Contains(mousePos.ToPoint()) && !isDragging;
            hoveringIntervalUp = intervalUpButton.Contains(mousePos.ToPoint()) && !isDragging;
            hoveringIntervalDown = intervalDownButton.Contains(mousePos.ToPoint()) && !isDragging;
            hoveringDirLeft = dirLeftButton.Contains(mousePos.ToPoint()) && !isDragging;
            hoveringDirRight = dirRightButton.Contains(mousePos.ToPoint()) && !isDragging;

            //检测悬停物品槽
            hoveringSlot = -1;
            if (!isDragging && inventoryRect.Contains(mousePos.ToPoint())) {
                int slotSize = 36;
                int cols = 5;
                int startX = inventoryRect.X + 10;
                int startY = inventoryRect.Y + 25;

                for (int i = 0; i < ThrowerTP.MaxSlots; i++) {
                    int col = i % cols;
                    int row = i / cols;
                    Rectangle slotRect = new Rectangle(startX + col * slotSize, startY + row * slotSize, slotSize - 2, slotSize - 2);
                    if (slotRect.Contains(mousePos.ToPoint())) {
                        hoveringSlot = i;
                        break;
                    }
                }
            }

            if (hoveringPanel) {
                player.mouseInterface = true;
            }

            //处理按钮点击
            HandleButtonClicks();

            //处理物品槽交互
            HandleSlotInteraction();

            //更新粒子
            UpdateParticles();
        }

        private void HandleDragging() {
            Vector2 mousePos = new Vector2(Main.mouseX, Main.mouseY);

            if (hoveringPanel && !hoveringToggle && !hoveringAmmoMode && hoveringSlot < 0 &&
                !hoveringSpeedUp && !hoveringSpeedDown && !hoveringAngleUp && !hoveringAngleDown &&
                !hoveringIntervalUp && !hoveringIntervalDown && !hoveringDirLeft && !hoveringDirRight &&
                UIHandleLoader.keyLeftPressState == KeyPressState.Pressed && !isDragging) {
                isDragging = true;
                dragOffset = DrawPosition - mousePos;
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.3f });
            }

            if (isDragging) {
                DrawPosition = mousePos + dragOffset;
                if (UIHandleLoader.keyLeftPressState == KeyPressState.Released) {
                    isDragging = false;
                    SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.3f });
                }
            }
        }

        private void HandleButtonClicks() {
            if (Station == null || UIHandleLoader.keyLeftPressState != KeyPressState.Pressed) {
                return;
            }

            if (hoveringToggle) {
                Station.IsThrowing = !Station.IsThrowing;
                Station.SendData();
                SoundEngine.PlaySound(Station.IsThrowing ? SoundID.MenuOpen : SoundID.MenuClose);
            }
            else if (hoveringAmmoMode) {
                Station.AmmoShootMode = !Station.AmmoShootMode;
                Station.SendData();
                SoundEngine.PlaySound(SoundID.MenuTick);
            }
            else if (hoveringSpeedUp) {
                Station.ThrowSpeed = Math.Min(20f, Station.ThrowSpeed + 1f);
                Station.SendData();
                SoundEngine.PlaySound(SoundID.MenuTick);
            }
            else if (hoveringSpeedDown) {
                Station.ThrowSpeed = Math.Max(1f, Station.ThrowSpeed - 1f);
                Station.SendData();
                SoundEngine.PlaySound(SoundID.MenuTick);
            }
            else if (hoveringAngleUp) {
                Station.ThrowAngle = Math.Min(45f, Station.ThrowAngle + 5f);
                Station.SendData();
                SoundEngine.PlaySound(SoundID.MenuTick);
            }
            else if (hoveringAngleDown) {
                Station.ThrowAngle = Math.Max(0f, Station.ThrowAngle - 5f);
                Station.SendData();
                SoundEngine.PlaySound(SoundID.MenuTick);
            }
            else if (hoveringIntervalUp) {
                Station.ThrowInterval = Math.Min(300, Station.ThrowInterval + 10);
                Station.SendData();
                SoundEngine.PlaySound(SoundID.MenuTick);
            }
            else if (hoveringIntervalDown) {
                Station.ThrowInterval = Math.Max(10, Station.ThrowInterval - 10);
                Station.SendData();
                SoundEngine.PlaySound(SoundID.MenuTick);
            }
            else if (hoveringDirLeft) {
                Station.ThrowDirection -= 15f;
                if (Station.ThrowDirection < -180f) Station.ThrowDirection += 360f;
                Station.SendData();
                SoundEngine.PlaySound(SoundID.MenuTick);
            }
            else if (hoveringDirRight) {
                Station.ThrowDirection += 15f;
                if (Station.ThrowDirection > 180f) Station.ThrowDirection -= 360f;
                Station.SendData();
                SoundEngine.PlaySound(SoundID.MenuTick);
            }
        }

        private void HandleSlotInteraction() {
            if (Station == null || hoveringSlot < 0) {
                return;
            }

            if (UIHandleLoader.keyLeftPressState == KeyPressState.Pressed) {
                //左键交互
                if (hoveringSlot < Station.StoredItems.Count) {
                    //取出物品
                    Item slotItem = Station.StoredItems[hoveringSlot];
                    if (Main.mouseItem.IsAir) {
                        Main.mouseItem = slotItem.Clone();
                        Station.StoredItems.RemoveAt(hoveringSlot);
                        SoundEngine.PlaySound(SoundID.Grab);
                    }
                    else if (Main.mouseItem.type == slotItem.type && slotItem.stack < slotItem.maxStack) {
                        //堆叠
                        int add = Math.Min(Main.mouseItem.stack, slotItem.maxStack - slotItem.stack);
                        slotItem.stack += add;
                        Main.mouseItem.stack -= add;
                        if (Main.mouseItem.stack <= 0) Main.mouseItem.TurnToAir();
                        SoundEngine.PlaySound(SoundID.Grab);
                    }
                    else {
                        //交换
                        Item temp = slotItem.Clone();
                        Station.StoredItems[hoveringSlot] = Main.mouseItem.Clone();
                        Main.mouseItem = temp;
                        SoundEngine.PlaySound(SoundID.Grab);
                    }
                    Station.SendData();
                }
                else if (!Main.mouseItem.IsAir && Station.StoredItems.Count < ThrowerTP.MaxSlots) {
                    //放入物品
                    Station.StoredItems.Add(Main.mouseItem.Clone());
                    Main.mouseItem.TurnToAir();
                    SoundEngine.PlaySound(SoundID.Grab);
                    Station.SendData();
                }
            }
            else if (UIHandleLoader.keyRightPressState == KeyPressState.Pressed) {
                //右键取一半
                if (hoveringSlot < Station.StoredItems.Count) {
                    Item slotItem = Station.StoredItems[hoveringSlot];
                    if (Main.mouseItem.IsAir && slotItem.stack > 1) {
                        int half = slotItem.stack / 2;
                        Main.mouseItem = slotItem.Clone();
                        Main.mouseItem.stack = half;
                        slotItem.stack -= half;
                        SoundEngine.PlaySound(SoundID.Grab);
                        Station.SendData();
                    }
                    else if (Main.mouseItem.IsAir) {
                        Main.mouseItem = slotItem.Clone();
                        Station.StoredItems.RemoveAt(hoveringSlot);
                        SoundEngine.PlaySound(SoundID.Grab);
                        Station.SendData();
                    }
                }
            }
        }

        private void UpdateParticles() {
            if (uiFadeAlpha < 0.3f) return;

            Vector2 panelCenter = DrawPosition;

            //电火花粒子
            sparkSpawnTimer++;
            if (sparkSpawnTimer >= 8 && sparks.Count < 20) {
                sparkSpawnTimer = 0;
                if (Station != null && Station.IsWorking) {
                    float xPos = Main.rand.NextFloat(panelCenter.X - PanelWidth / 2 + 30, panelCenter.X + PanelWidth / 2 - 30);
                    Vector2 startPos = new(xPos, panelCenter.Y + Main.rand.NextFloat(-PanelHeight / 2 + 30, PanelHeight / 2 - 30));
                    sparks.Add(new ThrowerSparkPRT(startPos));
                }
            }
            for (int i = sparks.Count - 1; i >= 0; i--) {
                if (sparks[i].Update()) {
                    sparks.RemoveAt(i);
                }
            }

            //数据流粒子
            dataParticleTimer++;
            if (dataParticleTimer >= 25 && dataParticles.Count < 8) {
                dataParticleTimer = 0;
                float xPos = Main.rand.NextFloat(panelCenter.X - PanelWidth / 2 + 30, panelCenter.X + PanelWidth / 2 - 30);
                Vector2 startPos = new(xPos, panelCenter.Y + Main.rand.NextFloat(-PanelHeight / 2 + 30, PanelHeight / 2 - 30));
                dataParticles.Add(new ThrowerDataPRT(startPos));
            }
            for (int i = dataParticles.Count - 1; i >= 0; i--) {
                if (dataParticles[i].Update(panelCenter)) {
                    dataParticles.RemoveAt(i);
                }
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (uiFadeAlpha < 0.01f || Station == null) return;

            //绘制主面板
            DrawMainPanel(spriteBatch);

            //绘制粒子
            foreach (var particle in dataParticles) {
                particle.Draw(spriteBatch, uiFadeAlpha * 0.4f);
            }
            foreach (var spark in sparks) {
                spark.Draw(spriteBatch, uiFadeAlpha * 0.7f);
            }

            //绘制UI元素
            DrawInventory(spriteBatch);
            DrawControlPanel(spriteBatch);
            DrawStatusPanel(spriteBatch);
            DrawHoverInfo(spriteBatch);
        }

        private void DrawMainPanel(SpriteBatch sb) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float alpha = uiFadeAlpha;

            //主背景，废土深色调渐变
            int segments = 50;
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                float t2 = (i + 1) / (float)segments;
                int y1 = panelRect.Y + (int)(t * panelRect.Height);
                int y2 = panelRect.Y + (int)(t2 * panelRect.Height);
                Rectangle r = new(panelRect.X, y1, panelRect.Width, Math.Max(1, y2 - y1));

                //废土色调
                Color darkBase = new Color(8, 6, 6);
                Color rustMid = new Color(22, 14, 10);
                Color warmEdge = new Color(35, 20, 14);

                float pulse = (float)Math.Sin(pulseTimer * 0.8f + t * 2.5f) * 0.5f + 0.5f;
                Color baseColor = Color.Lerp(darkBase, rustMid, pulse * 0.6f);
                Color finalColor = Color.Lerp(baseColor, warmEdge, t * 0.3f);
                finalColor *= alpha * 0.92f;

                sb.Draw(px, r, new Rectangle(0, 0, 1, 1), finalColor);
            }

            //热量闪烁层
            if (Station.IsWorking) {
                float flicker = (float)Math.Sin(glowTimer * 2f) * 0.4f + 0.6f;
                Color heatOverlay = new Color(50, 25, 15) * (alpha * 0.25f * flicker);
                sb.Draw(px, panelRect, new Rectangle(0, 0, 1, 1), heatOverlay);
            }

            //锈蚀网格
            DrawRustGrid(sb, panelRect, alpha * 0.6f);

            //扫描线
            DrawScanLines(sb, panelRect, alpha * 0.7f);

            //内发光
            float innerPulse = (float)Math.Sin(pulseTimer * 1.5f) * 0.5f + 0.5f;
            Rectangle inner = panelRect;
            inner.Inflate(-8, -8);
            sb.Draw(px, inner, new Rectangle(0, 0, 1, 1), new Color(120, 50, 25) * (alpha * 0.06f * innerPulse));

            //边框
            DrawIndustrialFrame(sb, panelRect, alpha, innerPulse);

            //标题
            string title = TitleText.Value;
            Vector2 titlePos = new Vector2(panelRect.Center.X, panelRect.Y + 24);
            Vector2 titleSize = FontAssets.MouseText.Value.MeasureString(title) * 0.85f;

            //标题发光
            Color glowColor = new Color(255, 150, 90) * (alpha * 0.55f);
            for (int i = 0; i < 4; i++) {
                float angle = MathHelper.TwoPi * i / 4f;
                Vector2 offset = angle.ToRotationVector2() * 2f;
                Utils.DrawBorderString(sb, title, titlePos - titleSize / 2 + offset, glowColor, 0.85f);
            }
            Utils.DrawBorderString(sb, title, titlePos - titleSize / 2, new Color(230, 200, 170) * alpha, 0.85f);

            //机器图标
            DrawMachineIcon(sb, new Vector2(panelRect.X + 28, panelRect.Y + 24), alpha);
        }

        private void DrawRustGrid(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            int gridRows = 12;
            float rowHeight = rect.Height / (float)gridRows;

            for (int row = 0; row < gridRows; row++) {
                float t = row / (float)gridRows;
                float y = rect.Y + row * rowHeight;
                float phase = dataFlowTimer + t * MathHelper.Pi * 0.8f;
                float brightness = (float)Math.Sin(phase) * 0.5f + 0.5f;

                Color gridColor = new Color(70, 35, 22) * (alpha * 0.04f * brightness);
                sb.Draw(px, new Rectangle(rect.X + 15, (int)y, rect.Width - 30, 1), new Rectangle(0, 0, 1, 1), gridColor);

                //随机锈斑
                if (Main.rand.NextBool(12)) {
                    int spotX = rect.X + Main.rand.Next(15, rect.Width - 15);
                    sb.Draw(px, new Rectangle(spotX, (int)y, 2, 1), new Rectangle(0, 0, 1, 1), gridColor * 2f);
                }
            }
        }

        private void DrawScanLines(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float scanY = rect.Y + (float)Math.Sin(scanLineTimer) * 0.5f * rect.Height + rect.Height * 0.5f;

            for (int i = -2; i <= 2; i++) {
                float offsetY = scanY + i * 3f;
                if (offsetY < rect.Y || offsetY > rect.Bottom) continue;

                float intensity = 1f - Math.Abs(i) * 0.3f;
                Color scanColor = new Color(200, 100, 60) * (alpha * 0.1f * intensity);
                int thickness = i == 0 ? 2 : 1;
                sb.Draw(px, new Rectangle(rect.X + 12, (int)offsetY, rect.Width - 24, thickness), new Rectangle(0, 0, 1, 1), scanColor);
            }
        }

        private void DrawIndustrialFrame(SpriteBatch sb, Rectangle rect, float alpha, float pulse) {
            Texture2D px = VaultAsset.placeholder2.Value;

            //外框
            Color rustEdge = Color.Lerp(new Color(130, 65, 35), new Color(190, 100, 55), pulse) * (alpha * 0.8f);
            sb.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 3), new Rectangle(0, 0, 1, 1), rustEdge);
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 3, rect.Width, 3), new Rectangle(0, 0, 1, 1), rustEdge * 0.65f);
            sb.Draw(px, new Rectangle(rect.X, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), rustEdge * 0.8f);
            sb.Draw(px, new Rectangle(rect.Right - 3, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), rustEdge * 0.8f);

            //内框
            Rectangle inner = rect;
            inner.Inflate(-8, -8);
            Color innerGlow = new Color(180, 90, 45) * (alpha * 0.15f * pulse);
            sb.Draw(px, new Rectangle(inner.X, inner.Y, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerGlow);
            sb.Draw(px, new Rectangle(inner.X, inner.Bottom - 1, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerGlow * 0.5f);
            sb.Draw(px, new Rectangle(inner.X, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerGlow * 0.7f);
            sb.Draw(px, new Rectangle(inner.Right - 1, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerGlow * 0.7f);

            //角落装饰
            DrawCornerMark(sb, new Vector2(rect.X + 14, rect.Y + 14), alpha * 0.85f);
            DrawCornerMark(sb, new Vector2(rect.Right - 14, rect.Y + 14), alpha * 0.85f);
            DrawCornerMark(sb, new Vector2(rect.X + 14, rect.Bottom - 14), alpha * 0.6f);
            DrawCornerMark(sb, new Vector2(rect.Right - 14, rect.Bottom - 14), alpha * 0.6f);
        }

        private static void DrawCornerMark(SpriteBatch sb, Vector2 pos, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float size = 6f;
            Color markColor = new Color(190, 95, 50) * alpha;

            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), markColor, 0f, new Vector2(0.5f), new Vector2(size, size * 0.15f), SpriteEffects.None, 0f);
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), markColor * 0.75f, MathHelper.PiOver2, new Vector2(0.5f), new Vector2(size, size * 0.15f), SpriteEffects.None, 0f);
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), markColor * 0.4f, 0f, new Vector2(0.5f), new Vector2(size * 0.35f, size * 0.35f), SpriteEffects.None, 0f);
        }

        private static void DrawMachineIcon(SpriteBatch sb, Vector2 pos, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            Color iconColor = new Color(220, 140, 90) * alpha;

            //简单的齿轮图标
            for (int i = 0; i < 6; i++) {
                float angle = MathHelper.TwoPi * i / 6f;
                Vector2 offset = angle.ToRotationVector2() * 8f;
                sb.Draw(px, pos + offset, new Rectangle(0, 0, 1, 1), iconColor, angle, new Vector2(0.5f), new Vector2(6f, 2f), SpriteEffects.None, 0f);
            }
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), iconColor * 0.8f, 0f, new Vector2(0.5f), new Vector2(5f, 5f), SpriteEffects.None, 0f);
        }

        private void DrawInventory(SpriteBatch sb) {
            float alpha = uiFadeAlpha;
            Texture2D px = VaultAsset.placeholder2.Value;

            //背景
            sb.Draw(px, inventoryRect, new Rectangle(0, 0, 1, 1), new Color(12, 8, 7) * (alpha * 0.85f));

            //边框
            Color borderColor = new Color(100, 55, 35) * (alpha * 0.7f);
            sb.Draw(px, new Rectangle(inventoryRect.X, inventoryRect.Y, inventoryRect.Width, 2), new Rectangle(0, 0, 1, 1), borderColor);
            sb.Draw(px, new Rectangle(inventoryRect.X, inventoryRect.Bottom - 2, inventoryRect.Width, 2), new Rectangle(0, 0, 1, 1), borderColor * 0.6f);
            sb.Draw(px, new Rectangle(inventoryRect.X, inventoryRect.Y, 2, inventoryRect.Height), new Rectangle(0, 0, 1, 1), borderColor * 0.8f);
            sb.Draw(px, new Rectangle(inventoryRect.Right - 2, inventoryRect.Y, 2, inventoryRect.Height), new Rectangle(0, 0, 1, 1), borderColor * 0.8f);

            //标签
            Utils.DrawBorderString(sb, InventoryLabel.Value, new Vector2(inventoryRect.X + 8, inventoryRect.Y + 5), new Color(200, 160, 130) * alpha, 0.6f);

            //物品槽
            int slotSize = 36;
            int cols = 5;
            int startX = inventoryRect.X + 10;
            int startY = inventoryRect.Y + 25;

            for (int i = 0; i < ThrowerTP.MaxSlots; i++) {
                int col = i % cols;
                int row = i / cols;
                Rectangle slotRect = new Rectangle(startX + col * slotSize, startY + row * slotSize, slotSize - 2, slotSize - 2);

                //槽位背景
                Color slotBg = hoveringSlot == i ? new Color(30, 20, 15) : new Color(18, 12, 10);
                sb.Draw(px, slotRect, new Rectangle(0, 0, 1, 1), slotBg * (alpha * 0.9f));

                //槽位边框
                Color slotBorder = hoveringSlot == i ? new Color(180, 100, 60) : new Color(80, 45, 30);
                sb.Draw(px, new Rectangle(slotRect.X, slotRect.Y, slotRect.Width, 1), new Rectangle(0, 0, 1, 1), slotBorder * (alpha * 0.7f));
                sb.Draw(px, new Rectangle(slotRect.X, slotRect.Bottom - 1, slotRect.Width, 1), new Rectangle(0, 0, 1, 1), slotBorder * (alpha * 0.5f));
                sb.Draw(px, new Rectangle(slotRect.X, slotRect.Y, 1, slotRect.Height), new Rectangle(0, 0, 1, 1), slotBorder * (alpha * 0.6f));
                sb.Draw(px, new Rectangle(slotRect.Right - 1, slotRect.Y, 1, slotRect.Height), new Rectangle(0, 0, 1, 1), slotBorder * (alpha * 0.6f));

                //绘制物品
                if (i < Station.StoredItems.Count && Station.StoredItems[i] != null && !Station.StoredItems[i].IsAir) {
                    Item item = Station.StoredItems[i];
                    VaultUtils.SimpleDrawItem(sb, item.type, slotRect.Center.ToVector2(), slotSize - 8, 1f, 0, Color.White * alpha);

                    //堆叠数
                    if (item.stack > 1) {
                        string stackText = item.stack.ToString();
                        Vector2 stackSize = FontAssets.ItemStack.Value.MeasureString(stackText) * 0.7f;
                        Utils.DrawBorderStringFourWay(sb, FontAssets.ItemStack.Value, stackText,
                            slotRect.Right - stackSize.X - 2, slotRect.Bottom - stackSize.Y - 2,
                            Color.White * alpha, Color.Black * alpha, Vector2.Zero, 0.7f);
                    }
                }
            }
        }

        private void DrawControlPanel(SpriteBatch sb) {
            float alpha = uiFadeAlpha;
            Texture2D px = VaultAsset.placeholder2.Value;

            //背景
            sb.Draw(px, controlRect, new Rectangle(0, 0, 1, 1), new Color(12, 8, 7) * (alpha * 0.85f));

            //边框
            Color borderColor = new Color(100, 55, 35) * (alpha * 0.7f);
            sb.Draw(px, new Rectangle(controlRect.X, controlRect.Y, controlRect.Width, 2), new Rectangle(0, 0, 1, 1), borderColor);
            sb.Draw(px, new Rectangle(controlRect.X, controlRect.Bottom - 2, controlRect.Width, 2), new Rectangle(0, 0, 1, 1), borderColor * 0.6f);
            sb.Draw(px, new Rectangle(controlRect.X, controlRect.Y, 2, controlRect.Height), new Rectangle(0, 0, 1, 1), borderColor * 0.8f);
            sb.Draw(px, new Rectangle(controlRect.Right - 2, controlRect.Y, 2, controlRect.Height), new Rectangle(0, 0, 1, 1), borderColor * 0.8f);

            //标签
            Utils.DrawBorderString(sb, ControlLabel.Value, new Vector2(controlRect.X + 8, controlRect.Y + 5), new Color(200, 160, 130) * alpha, 0.6f);

            //控制项
            int y = controlRect.Y + 28;
            int spacing = 32;

            //投掷力度
            DrawControlRow(sb, SpeedLabel.Value, $"{Station.ThrowSpeed:F0}", y, speedUpButton, speedDownButton, hoveringSpeedUp, hoveringSpeedDown, alpha);
            y += spacing;

            //散布角度
            DrawControlRow(sb, AngleLabel.Value, $"{Station.ThrowAngle:F0}°", y, angleUpButton, angleDownButton, hoveringAngleUp, hoveringAngleDown, alpha);
            y += spacing;

            //投掷间隔
            DrawControlRow(sb, IntervalLabel.Value, $"{Station.ThrowInterval / 60f:F1}s", y, intervalUpButton, intervalDownButton, hoveringIntervalUp, hoveringIntervalDown, alpha);
            y += spacing;

            //投掷方向
            DrawControlRow(sb, DirectionLabel.Value, $"{Station.ThrowDirection:F0}°", y, dirLeftButton, dirRightButton, hoveringDirLeft, hoveringDirRight, alpha);

            //方向指示器
            DrawDirectionPreview(sb, new Vector2(controlRect.X + 90, controlRect.Y + 155), alpha);
        }

        private void DrawControlRow(SpriteBatch sb, string label, string value, int y, Rectangle btnUp, Rectangle btnDown, bool hoverUp, bool hoverDown, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;

            //标签
            Utils.DrawBorderString(sb, label, new Vector2(controlRect.X + 10, y), new Color(180, 150, 120) * alpha, 0.55f);

            //数值
            Utils.DrawBorderString(sb, value, new Vector2(controlRect.X + 80, y), new Color(255, 200, 150) * alpha, 0.55f);

            //按钮
            DrawButton(sb, btnUp, "+", hoverUp, alpha);
            DrawButton(sb, btnDown, "-", hoverDown, alpha);
        }

        private static void DrawButton(SpriteBatch sb, Rectangle rect, string text, bool hovering, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;

            Color bgColor = hovering ? new Color(50, 30, 20) : new Color(25, 16, 12);
            Color borderColor = hovering ? new Color(200, 120, 70) : new Color(100, 60, 40);

            sb.Draw(px, rect, new Rectangle(0, 0, 1, 1), bgColor * (alpha * 0.9f));

            sb.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 1), new Rectangle(0, 0, 1, 1), borderColor * (alpha * 0.8f));
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), new Rectangle(0, 0, 1, 1), borderColor * (alpha * 0.5f));
            sb.Draw(px, new Rectangle(rect.X, rect.Y, 1, rect.Height), new Rectangle(0, 0, 1, 1), borderColor * (alpha * 0.7f));
            sb.Draw(px, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), new Rectangle(0, 0, 1, 1), borderColor * (alpha * 0.7f));

            Color textColor = hovering ? new Color(255, 200, 150) : new Color(180, 140, 110);
            Vector2 textSize = FontAssets.MouseText.Value.MeasureString(text) * 0.6f;
            Utils.DrawBorderString(sb, text, rect.Center.ToVector2() - textSize / 2, textColor * alpha, 0.6f);
        }

        private void DrawDirectionPreview(SpriteBatch sb, Vector2 center, float alpha) {
            float radians = MathHelper.ToRadians(Station.ThrowDirection);

            //背景圆
            Texture2D px = VaultAsset.placeholder2.Value;
            for (int i = 0; i < 24; i++) {
                float angle = MathHelper.TwoPi * i / 24f;
                Vector2 pos = center + angle.ToRotationVector2() * 18f;
                sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), new Color(80, 50, 35) * (alpha * 0.4f), 0f, new Vector2(0.5f), 2f, SpriteEffects.None, 0f);
            }

            //使用InputArrow纹理绘制方向箭头
            if (Thrower.InputArrow != null) {
                var arrowTex = Thrower.InputArrow.Value;
                Color arrowColor = Station.IsWorking ? new Color(255, 180, 100) : new Color(200, 150, 100);
                Vector2 arrowPos = center + radians.ToRotationVector2() * 10f;

                sb.Draw(
                    arrowTex,
                    arrowPos,
                    null,
                    arrowColor * alpha,
                    radians,
                    arrowTex.Size() / 2f,
                    0.5f,
                    SpriteEffects.None,
                    0f
                );
            }
        }

        private void DrawStatusPanel(SpriteBatch sb) {
            float alpha = uiFadeAlpha;
            Texture2D px = VaultAsset.placeholder2.Value;

            //背景
            sb.Draw(px, statusRect, new Rectangle(0, 0, 1, 1), new Color(12, 8, 7) * (alpha * 0.85f));

            //边框
            Color borderColor = new Color(100, 55, 35) * (alpha * 0.7f);
            sb.Draw(px, new Rectangle(statusRect.X, statusRect.Y, statusRect.Width, 2), new Rectangle(0, 0, 1, 1), borderColor);
            sb.Draw(px, new Rectangle(statusRect.X, statusRect.Bottom - 2, statusRect.Width, 2), new Rectangle(0, 0, 1, 1), borderColor * 0.6f);
            sb.Draw(px, new Rectangle(statusRect.X, statusRect.Y, 2, statusRect.Height), new Rectangle(0, 0, 1, 1), borderColor * 0.8f);
            sb.Draw(px, new Rectangle(statusRect.Right - 2, statusRect.Y, 2, statusRect.Height), new Rectangle(0, 0, 1, 1), borderColor * 0.8f);

            //状态指示灯
            Vector2 ledPos = new Vector2(statusRect.X + 15, statusRect.Y + 18);
            Color ledColor;
            string statusText;

            if (Station.MachineData.UEvalue < ThrowerTP.ConsumeUE) {
                float flash = (float)Math.Sin(warningFlashTimer * 3f) * 0.5f + 0.5f;
                ledColor = new Color(255, 80, 50) * flash;
                statusText = StatusNoEnergy.Value;
            }
            else if (Station.StoredItems.Count == 0) {
                ledColor = new Color(255, 200, 80);
                statusText = StatusNoItems.Value;
            }
            else if (Station.IsWorking) {
                float pulse = (float)Math.Sin(glowTimer * 2f) * 0.3f + 0.7f;
                ledColor = new Color(100, 255, 100) * pulse;
                statusText = StatusRunning.Value;
            }
            else {
                ledColor = new Color(150, 150, 150);
                statusText = StatusIdle.Value;
            }

            //LED灯
            sb.Draw(px, ledPos, new Rectangle(0, 0, 1, 1), ledColor * alpha, 0f, new Vector2(0.5f), 8f, SpriteEffects.None, 0f);
            sb.Draw(px, ledPos, new Rectangle(0, 0, 1, 1), Color.White * (alpha * 0.3f), 0f, new Vector2(0.5f), 4f, SpriteEffects.None, 0f);

            //状态文字
            Utils.DrawBorderString(sb, statusText, new Vector2(statusRect.X + 30, statusRect.Y + 12), new Color(200, 170, 140) * alpha, 0.6f);

            //启动/停止按钮
            DrawToggleButton(sb, alpha);

            //能量条
            DrawEnergyBar(sb, alpha);

            //物品统计
            int totalItems = 0;
            foreach (var item in Station.StoredItems) {
                if (item != null && !item.IsAir) totalItems += item.stack;
            }
            string itemCountText = $"物品: {totalItems}";
            Utils.DrawBorderString(sb, itemCountText, new Vector2(statusRect.X + 15, statusRect.Y + 85), new Color(180, 150, 120) * alpha, 0.55f);

            //弹药模式按钮
            DrawAmmoModeButton(sb, alpha);
        }

        private void DrawAmmoModeButton(SpriteBatch sb, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;

            Color bgColor = hoveringAmmoMode ? new Color(40, 28, 18) : new Color(25, 18, 12);
            Color borderColor;
            Color textColor;
            string btnText;

            if (Station.AmmoShootMode) {
                borderColor = new Color(120, 180, 255);
                textColor = new Color(150, 200, 255);
                btnText = AmmoModeOnText.Value;
            }
            else {
                borderColor = new Color(180, 140, 100);
                textColor = new Color(200, 170, 130);
                btnText = AmmoModeOffText.Value;
            }

            if (hoveringAmmoMode) {
                borderColor = Color.Lerp(borderColor, Color.White, 0.25f);
            }

            sb.Draw(px, ammoModeButton, new Rectangle(0, 0, 1, 1), bgColor * (alpha * 0.9f));

            sb.Draw(px, new Rectangle(ammoModeButton.X, ammoModeButton.Y, ammoModeButton.Width, 1), new Rectangle(0, 0, 1, 1), borderColor * (alpha * 0.8f));
            sb.Draw(px, new Rectangle(ammoModeButton.X, ammoModeButton.Bottom - 1, ammoModeButton.Width, 1), new Rectangle(0, 0, 1, 1), borderColor * (alpha * 0.5f));
            sb.Draw(px, new Rectangle(ammoModeButton.X, ammoModeButton.Y, 1, ammoModeButton.Height), new Rectangle(0, 0, 1, 1), borderColor * (alpha * 0.7f));
            sb.Draw(px, new Rectangle(ammoModeButton.Right - 1, ammoModeButton.Y, 1, ammoModeButton.Height), new Rectangle(0, 0, 1, 1), borderColor * (alpha * 0.7f));

            Vector2 textSize = FontAssets.MouseText.Value.MeasureString(btnText) * 0.5f;
            Utils.DrawBorderString(sb, btnText, ammoModeButton.Center.ToVector2() - textSize / 2, textColor * alpha, 0.5f);
        }

        private void DrawToggleButton(SpriteBatch sb, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;

            Color bgColor = hoveringToggle ? new Color(50, 30, 20) : new Color(30, 20, 15);
            Color borderColor;
            Color textColor;
            string btnText;

            if (Station.IsThrowing) {
                borderColor = new Color(255, 100, 80);
                textColor = new Color(255, 150, 120);
                btnText = StopText.Value;
            }
            else {
                borderColor = new Color(100, 200, 100);
                textColor = new Color(150, 255, 150);
                btnText = StartText.Value;
            }

            if (hoveringToggle) {
                borderColor = Color.Lerp(borderColor, Color.White, 0.3f);
            }

            sb.Draw(px, toggleButton, new Rectangle(0, 0, 1, 1), bgColor * (alpha * 0.9f));

            sb.Draw(px, new Rectangle(toggleButton.X, toggleButton.Y, toggleButton.Width, 2), new Rectangle(0, 0, 1, 1), borderColor * (alpha * 0.9f));
            sb.Draw(px, new Rectangle(toggleButton.X, toggleButton.Bottom - 2, toggleButton.Width, 2), new Rectangle(0, 0, 1, 1), borderColor * (alpha * 0.6f));
            sb.Draw(px, new Rectangle(toggleButton.X, toggleButton.Y, 2, toggleButton.Height), new Rectangle(0, 0, 1, 1), borderColor * (alpha * 0.8f));
            sb.Draw(px, new Rectangle(toggleButton.Right - 2, toggleButton.Y, 2, toggleButton.Height), new Rectangle(0, 0, 1, 1), borderColor * (alpha * 0.8f));

            Vector2 textSize = FontAssets.MouseText.Value.MeasureString(btnText) * 0.6f;
            Utils.DrawBorderString(sb, btnText, toggleButton.Center.ToVector2() - textSize / 2, textColor * alpha, 0.6f);
        }

        private void DrawEnergyBar(SpriteBatch sb, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;

            //能量条背景
            Rectangle barBg = new Rectangle(statusRect.X + 15, statusRect.Y + 50, statusRect.Width - 120, 18);
            sb.Draw(px, barBg, new Rectangle(0, 0, 1, 1), new Color(15, 10, 8) * (alpha * 0.9f));

            //能量条填充
            float ratio = MathHelper.Clamp(Station.MachineData.UEvalue / Station.MaxUEValue, 0f, 1f);
            int fillWidth = (int)((barBg.Width - 4) * ratio);
            Rectangle fillRect = new Rectangle(barBg.X + 2, barBg.Y + 2, fillWidth, barBg.Height - 4);

            if (fillWidth > 0) {
                Color lowColor = new Color(200, 80, 40);
                Color highColor = new Color(255, 200, 100);
                Color fillColor = Color.Lerp(lowColor, highColor, ratio);
                sb.Draw(px, fillRect, new Rectangle(0, 0, 1, 1), fillColor * (alpha * 0.85f));
            }

            //边框
            Color borderColor = new Color(100, 60, 40) * (alpha * 0.7f);
            sb.Draw(px, new Rectangle(barBg.X, barBg.Y, barBg.Width, 1), new Rectangle(0, 0, 1, 1), borderColor);
            sb.Draw(px, new Rectangle(barBg.X, barBg.Bottom - 1, barBg.Width, 1), new Rectangle(0, 0, 1, 1), borderColor * 0.6f);
            sb.Draw(px, new Rectangle(barBg.X, barBg.Y, 1, barBg.Height), new Rectangle(0, 0, 1, 1), borderColor * 0.8f);
            sb.Draw(px, new Rectangle(barBg.Right - 1, barBg.Y, 1, barBg.Height), new Rectangle(0, 0, 1, 1), borderColor * 0.8f);

            //能量标签
            string energyText = $"{EnergyLabel.Value}: {(int)Station.MachineData.UEvalue}/{(int)Station.MaxUEValue}";
            Utils.DrawBorderString(sb, energyText, new Vector2(barBg.Right + 8, barBg.Y), new Color(180, 150, 120) * alpha, 0.5f);
        }

        private void DrawHoverInfo(SpriteBatch sb) {
            //显示悬停物品信息
            if (hoveringSlot >= 0 && hoveringSlot < Station.StoredItems.Count) {
                Item item = Station.StoredItems[hoveringSlot];
                if (item != null && !item.IsAir) {
                    Main.HoverItem = item.Clone();
                    Main.hoverItemName = item.Name;
                }
            }
            else if (hoveringSlot >= 0 && Station.StoredItems.Count <= hoveringSlot) {
                ShowTooltip(sb, DropHint.Value);
            }
            else if (hoveringAmmoMode) {
                ShowTooltip(sb, AmmoModeHint.Value);
            }
        }

        private void ShowTooltip(SpriteBatch sb, string text) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float alpha = uiFadeAlpha;
            Vector2 mousePos = new Vector2(Main.mouseX, Main.mouseY);

            Vector2 textSize = FontAssets.MouseText.Value.MeasureString(text) * 0.7f;
            Vector2 textPos = mousePos + new Vector2(16, 16);

            Rectangle tooltipBg = new Rectangle((int)textPos.X - 8, (int)textPos.Y - 4, (int)textSize.X + 16, (int)textSize.Y + 8);
            sb.Draw(px, tooltipBg, new Rectangle(0, 0, 1, 1), new Color(12, 8, 6) * 0.95f);
            sb.Draw(px, new Rectangle(tooltipBg.X, tooltipBg.Y, tooltipBg.Width, 2), new Rectangle(0, 0, 1, 1), new Color(160, 90, 50) * 0.8f);
            sb.Draw(px, new Rectangle(tooltipBg.X, tooltipBg.Y, 2, tooltipBg.Height), new Rectangle(0, 0, 1, 1), new Color(160, 90, 50) * 0.8f);

            Utils.DrawBorderString(sb, text, textPos, new Color(240, 210, 170), 0.7f);
        }
    }

    #region 粒子类

    /// <summary>
    /// 投掷器电火花粒子
    /// </summary>
    internal class ThrowerSparkPRT
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Life;
        public float MaxLife;
        public float Scale;
        public Color SparkColor;

        public ThrowerSparkPRT(Vector2 pos) {
            Position = pos;
            Velocity = Main.rand.NextVector2Circular(1.5f, 1.5f);
            MaxLife = Main.rand.Next(20, 40);
            Life = MaxLife;
            Scale = Main.rand.NextFloat(0.6f, 1.2f);
            SparkColor = Color.Lerp(new Color(255, 180, 100), new Color(255, 120, 60), Main.rand.NextFloat());
        }

        public bool Update() {
            Life--;
            Position += Velocity;
            Velocity *= 0.96f;
            return Life <= 0;
        }

        public void Draw(SpriteBatch sb, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float lifeRatio = Life / MaxLife;
            Color drawColor = SparkColor * (lifeRatio * alpha);
            float drawScale = Scale * lifeRatio * 3f;
            sb.Draw(px, Position, new Rectangle(0, 0, 1, 1), drawColor, 0f, new Vector2(0.5f), drawScale, SpriteEffects.None, 0f);
        }
    }

    /// <summary>
    /// 投掷器数据流粒子
    /// </summary>
    internal class ThrowerDataPRT
    {
        public Vector2 Position;
        public float Life;
        public float MaxLife;
        public float Phase;

        public ThrowerDataPRT(Vector2 pos) {
            Position = pos;
            MaxLife = Main.rand.Next(40, 70);
            Life = MaxLife;
            Phase = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        public bool Update(Vector2 center) {
            Life--;
            float t = 1f - Life / MaxLife;
            Position += new Vector2((float)Math.Sin(Phase + t * 4f) * 0.5f, -0.3f);
            return Life <= 0;
        }

        public void Draw(SpriteBatch sb, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float lifeRatio = Life / MaxLife;
            Color drawColor = new Color(180, 120, 80) * (lifeRatio * alpha * 0.6f);
            sb.Draw(px, Position, new Rectangle(0, 0, 1, 1), drawColor, 0f, new Vector2(0.5f), 2f, SpriteEffects.None, 0f);
        }
    }

    #endregion
}
