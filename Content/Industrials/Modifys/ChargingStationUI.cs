using CalamityMod.Items.DraedonMisc;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.ADV.UIEffect;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.Modifys
{
    internal class ChargingStationUI : UIHandle, ILocalizedModType
    {
        //面板尺寸
        private const float PanelWidth = 380f;
        private const float PanelHeight = 280f;

        //动画变量
        private float scanLineTimer = 0f;
        private float chargeGlow = 0f;
        private float pulseTimer = 0f;
        private float dataStream = 0f;
        private float gridPhase = 0f;
        private float sparkTimer = 0f;

        //粒子系统
        private readonly List<ElectricSparkPRT> electricSparks = new();
        private int sparkSpawnTimer = 0;
        private readonly List<DraedonDataPRT> dataParticles = new();
        private int dataParticleTimer = 0;

        public static ChargingStationUI Instance => UIHandleLoader.GetUIHandleOfType<ChargingStationUI>();
        public bool ids;
        public bool Open;
        public override bool Active => Open || uiFadeAlpha > 0;

        //UI淡入淡出
        private float uiFadeAlpha = 0f;

        //拖拽功能
        private bool isDragging = false;
        private Vector2 dragOffset = Vector2.Zero;
        private Rectangle titleBarRect;

        //鼠标交互
        private Rectangle panelRect;
        private Rectangle itemSlotRect;
        private Rectangle batterySlotRect;
        private Rectangle energyBarRect;
        private Rectangle chargeBarRect;
        private bool hoveringItemSlot = false;
        private bool hoveringBatterySlot = false;
        private bool hoveringEnergyBar = false;
        private bool hoveringChargeBar = false;
        private bool hoveringTitleBar = false;
        private bool hoveringPanel = false;

        //本地化文本
        protected static LocalizedText TitleText;
        protected static LocalizedText ItemSlotLabel;
        protected static LocalizedText BatterySlotLabel;
        protected static LocalizedText StationEnergyLabel;
        protected static LocalizedText ItemChargeLabel;
        protected static LocalizedText StatusLabel;
        protected static LocalizedText ChargingText;
        protected static LocalizedText IdleText;
        protected static LocalizedText InsertItemHint;
        protected static LocalizedText InsertBatteryHint;
        protected static LocalizedText EnergyUnit;

        private ChargingStationTP station;

        public string LocalizationCategory => "UI";

        public override void SetStaticDefaults() {
            TitleText = this.GetLocalization(nameof(TitleText), () => "充能站");
            ItemSlotLabel = this.GetLocalization(nameof(ItemSlotLabel), () => "充能物品");
            BatterySlotLabel = this.GetLocalization(nameof(BatterySlotLabel), () => "电池");
            StationEnergyLabel = this.GetLocalization(nameof(StationEnergyLabel), () => "站点能量");
            ItemChargeLabel = this.GetLocalization(nameof(ItemChargeLabel), () => "物品电量");
            StatusLabel = this.GetLocalization(nameof(StatusLabel), () => "状态:");
            ChargingText = this.GetLocalization(nameof(ChargingText), () => "充能中");
            IdleText = this.GetLocalization(nameof(IdleText), () => "待机");
            InsertItemHint = this.GetLocalization(nameof(InsertItemHint), () => "放入需要充能的物品");
            InsertBatteryHint = this.GetLocalization(nameof(InsertBatteryHint), () => "放入嘉登能量电池");
            EnergyUnit = this.GetLocalization(nameof(EnergyUnit), () => "UE");
        }

        public void Initialize(ChargingStationTP chargingStation) {
            if (station != chargingStation) {
                station = chargingStation;
                Open = true;
            }
            else {
                Open = !Open;
            }
            
            ids = true;
        }

        public override void Update() {
            if (Open) {
                if (uiFadeAlpha < 1f) {
                    uiFadeAlpha += 0.1f;
                }
            }
            else {
                if (uiFadeAlpha > 0f) {
                    uiFadeAlpha -= 0.1f;
                }
            }
            if (ids) {
                ids = false;
                DrawPosition = new Vector2(Main.screenWidth / 2, Main.screenHeight / 4f);
            }

            if (Open && (station == null || !station.Active || station.PosInWorld.To(player.Center).Length() > 160)) {
                Open = false;
                SoundEngine.PlaySound(CWRSound.ButtonZero with { Pitch = -0.2f, Volume = 0.6f });
                return;
            }

            //处理拖拽
            HandleDragging();

            //限制面板位置在屏幕内
            DrawPosition.X = MathHelper.Clamp(DrawPosition.X, PanelWidth / 2 + 10, Main.screenWidth - PanelWidth / 2 - 10);
            DrawPosition.Y = MathHelper.Clamp(DrawPosition.Y, PanelHeight / 2 + 10, Main.screenHeight - PanelHeight / 2 - 10);

            //更新动画计时器
            scanLineTimer += 0.04f;
            chargeGlow += 0.055f;
            pulseTimer += 0.02f;
            dataStream += 0.045f;
            gridPhase += 0.015f;
            sparkTimer += 0.1f;

            if (scanLineTimer > MathHelper.TwoPi) scanLineTimer -= MathHelper.TwoPi;
            if (chargeGlow > MathHelper.TwoPi) chargeGlow -= MathHelper.TwoPi;
            if (pulseTimer > MathHelper.TwoPi) pulseTimer -= MathHelper.TwoPi;
            if (dataStream > MathHelper.TwoPi) dataStream -= MathHelper.TwoPi;
            if (gridPhase > MathHelper.TwoPi) gridPhase -= MathHelper.TwoPi;
            if (sparkTimer > MathHelper.TwoPi) sparkTimer -= MathHelper.TwoPi;

            if (uiFadeAlpha < 0.01f) {
                return;
            }

            //计算面板区域
            Vector2 topLeft = DrawPosition - new Vector2(PanelWidth / 2, PanelHeight / 2);
            panelRect = new Rectangle((int)topLeft.X, (int)topLeft.Y, (int)PanelWidth, (int)PanelHeight);

            //计算标题栏区域（用于拖拽）
            titleBarRect = new Rectangle(panelRect.X, panelRect.Y, panelRect.Width, 55);

            //计算子区域
            itemSlotRect = new Rectangle((int)(topLeft.X + 35), (int)(topLeft.Y + 80), 70, 70);
            batterySlotRect = new Rectangle((int)(topLeft.X + 35), (int)(topLeft.Y + 170), 70, 70);
            energyBarRect = new Rectangle((int)(topLeft.X + 145), (int)(topLeft.Y + 80), 40, 160);
            chargeBarRect = new Rectangle((int)(topLeft.X + 285), (int)(topLeft.Y + 80), 40, 160);

            //鼠标交互检测
            Vector2 mousePos = new Vector2(Main.mouseX, Main.mouseY);
            hoveringItemSlot = itemSlotRect.Contains(mousePos.ToPoint()) && !isDragging;
            hoveringBatterySlot = batterySlotRect.Contains(mousePos.ToPoint()) && !isDragging;
            hoveringEnergyBar = energyBarRect.Contains(mousePos.ToPoint()) && !isDragging;
            hoveringChargeBar = chargeBarRect.Contains(mousePos.ToPoint()) && !isDragging;
            hoveringTitleBar = titleBarRect.Contains(mousePos.ToPoint()) && !isDragging;
            hoveringPanel = panelRect.Contains(mousePos.ToPoint());

            if (hoveringPanel) {
                player.mouseInterface = true;
            }

            //处理槽位交互
            if (hoveringItemSlot && UIHandleLoader.keyLeftPressState == KeyPressState.Pressed) {
                if (ChargingStationTP.ItemIsCharge(Main.mouseItem, out _, out _) || Main.mouseItem.IsAir) {
                    HandlerSlotItem(ref station.Item);
                    station.SendData();
                }
                else {
                    SoundEngine.PlaySound(SoundID.MenuClose);
                    CombatText.NewText(station.HitBox, new Color(255, 100, 80), "只能放入可充能物品!", false);
                }
            }

            if (hoveringBatterySlot && UIHandleLoader.keyLeftPressState == KeyPressState.Pressed) {
                if (Main.mouseItem.type == ModContent.ItemType<DraedonPowerCell>() || Main.mouseItem.IsAir) {
                    HandlerSlotItem(ref station.Empty);
                    station.SendData();
                }
                else {
                    SoundEngine.PlaySound(SoundID.MenuClose);
                    CombatText.NewText(station.HitBox, new Color(255, 100, 80), "只能放入嘉登能量电池!", false);
                }
            }

            //更新粒子
            UpdateParticles();
        }

        private void HandleDragging() {
            Vector2 mousePos = new Vector2(Main.mouseX, Main.mouseY);

            //开始拖拽
            if (hoveringPanel && !hoveringItemSlot && !hoveringBatterySlot && !hoveringEnergyBar && !hoveringChargeBar 
                && UIHandleLoader.keyLeftPressState == KeyPressState.Pressed && !isDragging) {
                isDragging = true;
                dragOffset = DrawPosition - mousePos;
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.3f });
            }

            //执行拖拽
            if (isDragging) {
                DrawPosition = mousePos + dragOffset;
                if (UIHandleLoader.keyLeftPressState == KeyPressState.Released) {
                    //结束拖拽
                    isDragging = false;
                    SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.3f });
                }
            }
        }

        private static void HandlerSlotItem(ref Item setItem) {
            if (setItem.IsAir && Main.mouseItem.IsAir) {
                return;
            }

            SoundEngine.PlaySound(SoundID.Grab);

            if (setItem.type == ItemID.None) {
                setItem = Main.mouseItem.Clone();
                Main.mouseItem.TurnToAir();
            }
            else {
                if (setItem.type == Main.mouseItem.type && setItem.stack < setItem.maxStack) {
                    setItem.stack += Main.mouseItem.stack;
                    Main.mouseItem.TurnToAir();
                }
                else {
                    if (Main.mouseItem.IsAir && Main.keyState.PressingShift()) {
                        Main.LocalPlayer.QuickSpawnItem(new EntitySource_WorldEvent(), setItem, setItem.stack);
                        setItem.TurnToAir();
                        return;
                    }
                    Item swopItem = setItem.Clone();
                    setItem = Main.mouseItem.Clone();
                    Main.mouseItem = swopItem;
                }
            }
        }

        private void UpdateParticles() {
            if (uiFadeAlpha < 0.3f) return;

            Vector2 panelCenter = DrawPosition;

            //电火花粒子
            sparkSpawnTimer++;
            if (sparkSpawnTimer >= 6 && electricSparks.Count < 30) {
                sparkSpawnTimer = 0;
                if (station.MachineData.UEvalue > 0 || !station.Item.IsAir) {
                    float xPos = Main.rand.NextFloat(panelCenter.X - PanelWidth / 2 + 40, panelCenter.X + PanelWidth / 2 - 40);
                    Vector2 startPos = new(xPos, panelCenter.Y + Main.rand.NextFloat(-PanelHeight / 2 + 40, PanelHeight / 2 - 40));
                    electricSparks.Add(new ElectricSparkPRT(startPos));
                }
            }
            for (int i = electricSparks.Count - 1; i >= 0; i--) {
                if (electricSparks[i].Update()) {
                    electricSparks.RemoveAt(i);
                }
            }

            //数据流粒子
            dataParticleTimer++;
            if (dataParticleTimer >= 20 && dataParticles.Count < 12) {
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
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (uiFadeAlpha < 0.01f || station == null) return;

            //绘制主面板
            DrawMainPanel(spriteBatch);

            //绘制粒子
            foreach (var particle in dataParticles) {
                particle.Draw(spriteBatch, uiFadeAlpha * 0.5f);
            }
            foreach (var spark in electricSparks) {
                spark.Draw(spriteBatch, uiFadeAlpha * 0.8f);
            }

            //绘制UI元素
            DrawSlots(spriteBatch);
            DrawEnergyBar(spriteBatch);
            DrawChargeBar(spriteBatch);
            DrawStatusText(spriteBatch);
            DrawSlotHover(spriteBatch);
        }

        private void DrawMainPanel(SpriteBatch sb) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float alpha = uiFadeAlpha;

            //主背景渐变，废土深色调
            int segments = 40;
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                float t2 = (i + 1) / (float)segments;
                int y1 = panelRect.Y + (int)(t * panelRect.Height);
                int y2 = panelRect.Y + (int)(t2 * panelRect.Height);
                Rectangle r = new(panelRect.X, y1, panelRect.Width, Math.Max(1, y2 - y1));

                //废土色调，深灰、暗红、锈橙
                Color wastelandDark = new Color(12, 8, 8);
                Color rustMid = new Color(25, 15, 10);
                Color emberGlow_color = new Color(45, 22, 15);

                float pulse = (float)Math.Sin(pulseTimer * 0.6f + t * 2f) * 0.5f + 0.5f;
                Color baseColor = Color.Lerp(wastelandDark, rustMid, pulse);
                Color finalColor = Color.Lerp(baseColor, emberGlow_color, t * 0.4f);
                finalColor *= alpha * 0.88f;

                sb.Draw(px, r, new Rectangle(0, 0, 1, 1), finalColor);
            }

            //热量闪烁覆盖层
            float flicker = (float)Math.Sin(chargeGlow * 1.5f) * 0.5f + 0.5f;
            Color heatOverlay = new Color(40, 18, 10) * (alpha * 0.3f * flicker);
            sb.Draw(px, panelRect, new Rectangle(0, 0, 1, 1), heatOverlay);

            //锈蚀网格纹理
            DrawRustGrid(sb, panelRect, alpha * 0.7f);

            //扫描线效果
            DrawWastelandScanLines(sb, panelRect, alpha * 0.8f);

            //内发光
            float innerPulse = (float)Math.Sin(pulseTimer * 1.2f) * 0.5f + 0.5f;
            Rectangle inner = panelRect;
            inner.Inflate(-10, -10);
            sb.Draw(px, inner, new Rectangle(0, 0, 1, 1), new Color(140, 60, 30) * (alpha * 0.08f * innerPulse));

            //废土边框
            DrawWastelandFrame(sb, panelRect, alpha, innerPulse);

            //标题文字
            string title = TitleText.Value;
            Vector2 titlePos = new Vector2(panelRect.Center.X, panelRect.Y + 28);
            Vector2 titleSize = FontAssets.MouseText.Value.MeasureString(title) * 0.9f;

            //发光描边
            Color glowColor = new Color(255, 140, 80) * (alpha * 0.6f);
            for (int i = 0; i < 4; i++) {
                float angle = MathHelper.TwoPi * i / 4f;
                Vector2 offset = angle.ToRotationVector2() * 2.5f;
                Utils.DrawBorderString(sb, title, titlePos - titleSize / 2 + offset, glowColor, 0.9f);
            }

            Utils.DrawBorderString(sb, title, titlePos - titleSize / 2, new Color(220, 180, 160) * alpha, 0.9f);

            //拖拽提示
            if (hoveringTitleBar && !isDragging) {
                Color hintColor = new Color(200, 180, 140) * (alpha * 0.6f);
                string dragHint = "◈";
                Vector2 dragHintPos = new Vector2(panelRect.Right - 25, panelRect.Y + 15);
                Utils.DrawBorderString(sb, dragHint, dragHintPos, hintColor, 0.8f);
            }
        }

        private void DrawRustGrid(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            int gridRows = 10;
            float rowHeight = rect.Height / (float)gridRows;

            for (int row = 0; row < gridRows; row++) {
                float t = row / (float)gridRows;
                float y = rect.Y + row * rowHeight;
                float phase = gridPhase + t * MathHelper.Pi * 0.7f;
                float brightness = (float)Math.Sin(phase) * 0.5f + 0.5f;

                //锈色网格线
                Color gridColor = new Color(80, 40, 25) * (alpha * 0.05f * brightness);
                sb.Draw(px, new Rectangle(rect.X + 20, (int)y, rect.Width - 40, 1), new Rectangle(0, 0, 1, 1), gridColor);

                if (Main.rand.NextBool(8)) {
                    int spotX = rect.X + Main.rand.Next(20, rect.Width - 20);
                    sb.Draw(px, new Rectangle(spotX, (int)y, 3, 1), new Rectangle(0, 0, 1, 1), gridColor * 1.8f);
                }
            }
        }

        private void DrawWastelandScanLines(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float scanY = rect.Y + (float)Math.Sin(scanLineTimer) * 0.5f * rect.Height + rect.Height * 0.5f;

            for (int i = -2; i <= 2; i++) {
                float offsetY = scanY + i * 4f;
                if (offsetY < rect.Y || offsetY > rect.Bottom) continue;

                float intensity = 1f - Math.Abs(i) * 0.3f;
                Color scanColor = new Color(180, 90, 50) * (alpha * 0.12f * intensity);
                int thickness = i == 0 ? 2 : 1;
                sb.Draw(px, new Rectangle(rect.X + 15, (int)offsetY, rect.Width - 30, thickness), new Rectangle(0, 0, 1, 1), scanColor);
            }
        }

        private void DrawWastelandFrame(SpriteBatch sb, Rectangle rect, float alpha, float pulse) {
            Texture2D px = VaultAsset.placeholder2.Value;

            //外框，锈橙色
            Color rustEdge = Color.Lerp(new Color(140, 70, 40), new Color(200, 110, 60), pulse) * (alpha * 0.75f);
            sb.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 4), new Rectangle(0, 0, 1, 1), rustEdge);
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 4, rect.Width, 4), new Rectangle(0, 0, 1, 1), rustEdge * 0.7f);
            sb.Draw(px, new Rectangle(rect.X, rect.Y, 4, rect.Height), new Rectangle(0, 0, 1, 1), rustEdge * 0.85f);
            sb.Draw(px, new Rectangle(rect.Right - 4, rect.Y, 4, rect.Height), new Rectangle(0, 0, 1, 1), rustEdge * 0.85f);

            //内框发光
            Rectangle inner = rect;
            inner.Inflate(-10, -10);
            Color innerGlow = new Color(200, 100, 50) * (alpha * 0.18f * pulse);
            sb.Draw(px, new Rectangle(inner.X, inner.Y, inner.Width, 2), new Rectangle(0, 0, 1, 1), innerGlow);
            sb.Draw(px, new Rectangle(inner.X, inner.Bottom - 2, inner.Width, 2), new Rectangle(0, 0, 1, 1), innerGlow * 0.6f);
            sb.Draw(px, new Rectangle(inner.X, inner.Y, 2, inner.Height), new Rectangle(0, 0, 1, 1), innerGlow * 0.8f);
            sb.Draw(px, new Rectangle(inner.Right - 2, inner.Y, 2, inner.Height), new Rectangle(0, 0, 1, 1), innerGlow * 0.8f);

            //角落标记，废土警告标志
            DrawWastelandMark(sb, new Vector2(rect.X + 16, rect.Y + 16), alpha * 0.9f);
            DrawWastelandMark(sb, new Vector2(rect.Right - 16, rect.Y + 16), alpha * 0.9f);
            DrawWastelandMark(sb, new Vector2(rect.X + 16, rect.Bottom - 16), alpha * 0.65f);
            DrawWastelandMark(sb, new Vector2(rect.Right - 16, rect.Bottom - 16), alpha * 0.65f);
        }

        private static void DrawWastelandMark(SpriteBatch sb, Vector2 pos, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float size = 7f;
            Color markColor = new Color(200, 100, 50) * alpha;

            //警告三角形样式
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), markColor, 0f, new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.2f), SpriteEffects.None, 0f);
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), markColor * 0.8f, MathHelper.PiOver2, new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.2f), SpriteEffects.None, 0f);
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), markColor * 0.5f, 0f, new Vector2(0.5f, 0.5f), new Vector2(size * 0.4f, size * 0.4f), SpriteEffects.None, 0f);
        }

        private void DrawSlotHover(SpriteBatch sb) {
            //悬停提示
            if (hoveringItemSlot && station.Item.IsAir) {
                ShowTooltip(sb, InsertItemHint.Value);
            }
            else if (hoveringItemSlot && !station.Item.IsAir) {
                Main.HoverItem = station.Item.Clone();
                Main.hoverItemName = station.Item.Name;
            }

            if (hoveringBatterySlot && station.Empty.IsAir) {
                ShowTooltip(sb, InsertBatteryHint.Value);
            }
            else if (hoveringBatterySlot && !station.Empty.IsAir) {
                Main.HoverItem = station.Empty.Clone();
                Main.hoverItemName = station.Empty.Name;
            }
        }

        private void DrawSlots(SpriteBatch sb) {
            float alpha = uiFadeAlpha;

            //绘制物品槽
            DrawSlot(sb, itemSlotRect, hoveringItemSlot, ItemSlotLabel.Value, alpha);
            if (!station.Item.IsAir) {
                VaultUtils.SimpleDrawItem(sb, station.Item.type, itemSlotRect.Center.ToVector2(), 50, 1f, 0, Color.White * alpha);
                if (station.Item.stack > 1) {
                    string stackText = station.Item.stack.ToString();
                    Vector2 stackSize = FontAssets.ItemStack.Value.MeasureString(stackText);
                    Utils.DrawBorderStringFourWay(sb, FontAssets.ItemStack.Value, stackText,
                        itemSlotRect.Right - stackSize.X * 0.8f - 8, itemSlotRect.Bottom - stackSize.Y * 0.8f - 8,
                        Color.White * alpha, Color.Black * alpha, new Vector2(0.3f), 0.8f);
                }
            }

            //绘制电池槽
            DrawSlot(sb, batterySlotRect, hoveringBatterySlot, BatterySlotLabel.Value, alpha);
            if (!station.Empty.IsAir) {
                VaultUtils.SimpleDrawItem(sb, station.Empty.type, batterySlotRect.Center.ToVector2(), 50, 1f, 0, Color.White * alpha);
                if (station.Empty.stack > 1) {
                    string stackText = station.Empty.stack.ToString();
                    Vector2 stackSize = FontAssets.ItemStack.Value.MeasureString(stackText);
                    Utils.DrawBorderStringFourWay(sb, FontAssets.ItemStack.Value, stackText,
                        batterySlotRect.Right - stackSize.X * 0.8f - 8, batterySlotRect.Bottom - stackSize.Y * 0.8f - 8,
                        Color.White * alpha, Color.Black * alpha, new Vector2(0.3f), 0.8f);
                }
            }
        }

        private void DrawSlot(SpriteBatch sb, Rectangle rect, bool hovering, string label, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float hoverGlow = hovering ? 0.3f : 0f;

            //背景，深色金属质感
            Color slotBg = new Color(18, 12, 10) * (alpha * 0.9f);
            sb.Draw(px, rect, new Rectangle(0, 0, 1, 1), slotBg);

            //边框，锈蚀金属
            Color edgeColor = Color.Lerp(new Color(120, 70, 40), new Color(180, 110, 60), (float)Math.Sin(pulseTimer * 1.3f) * 0.5f + 0.5f) * (alpha * (0.75f + hoverGlow));
            sb.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 3), new Rectangle(0, 0, 1, 1), edgeColor);
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 3, rect.Width, 3), new Rectangle(0, 0, 1, 1), edgeColor);
            sb.Draw(px, new Rectangle(rect.X, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), edgeColor);
            sb.Draw(px, new Rectangle(rect.Right - 3, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), edgeColor);

            //标签
            Vector2 labelSize = FontAssets.MouseText.Value.MeasureString(label) * 0.65f;
            Vector2 labelPos = new Vector2(rect.Center.X - labelSize.X / 2, rect.Y - 22);

            Color labelGlow = new Color(220, 140, 80) * (alpha * 0.5f);
            Utils.DrawBorderString(sb, label, labelPos + new Vector2(1.2f, 1.2f), labelGlow, 0.65f);
            Utils.DrawBorderString(sb, label, labelPos, new Color(240, 200, 160) * alpha, 0.65f);
        }

        private void DrawEnergyBar(SpriteBatch sb) {
            DrawBar(sb, energyBarRect, station.MachineData.UEvalue, station.MaxUEValue,
                hoveringEnergyBar, StationEnergyLabel.Value, new Color(120, 80, 40), new Color(220, 160, 80));
        }

        private void DrawChargeBar(SpriteBatch sb) {
            if (station.Item.IsAir) {
                DrawBar(sb, chargeBarRect, 0, 1, hoveringChargeBar, ItemChargeLabel.Value,
                    new Color(80, 60, 50), new Color(120, 100, 80));
                return;
            }

            if (ChargingStationTP.ItemIsCharge(station.Item, out float ueValue, out float maxValue)) {
                DrawBar(sb, chargeBarRect, ueValue, maxValue, hoveringChargeBar, ItemChargeLabel.Value,
                    new Color(150, 100, 60), new Color(255, 180, 100));
            }
        }

        private void DrawBar(SpriteBatch sb, Rectangle rect, float value, float maxValue, bool hovering,
            string label, Color lowColor, Color highColor) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float alpha = uiFadeAlpha;
            float hoverGlow = hovering ? 0.3f : 0f;

            //背景
            Color barBg = new Color(18, 12, 10) * (alpha * 0.9f);
            sb.Draw(px, rect, new Rectangle(0, 0, 1, 1), barBg);

            float ratio = MathHelper.Clamp(value / maxValue, 0f, 1f);
            int maxFillHeight = rect.Height - 12;
            int fillHeight = (int)(maxFillHeight * ratio);
            fillHeight = Math.Min(fillHeight, maxFillHeight);
            fillHeight = Math.Max(fillHeight, 0);

            Rectangle fillRect = new Rectangle(
                rect.X + 6,
                rect.Bottom - fillHeight - 6,
                rect.Width - 12,
                fillHeight
            );

            if (fillHeight > 0) {
                int fillSegments = Math.Max(1, fillHeight / 4);
                for (int i = 0; i < fillSegments; i++) {
                    float t = i / (float)fillSegments;
                    float t2 = (i + 1) / (float)fillSegments;

                    int y1 = fillRect.Y + (int)(t * fillRect.Height);
                    int y2 = fillRect.Y + (int)(t2 * fillRect.Height);
                    Rectangle segRect = new(fillRect.X, y1, fillRect.Width, Math.Max(1, y2 - y1));

                    Color color = Color.Lerp(lowColor, highColor, 1f - t);
                    float pulse = (float)Math.Sin(sparkTimer * 2f - t * 5f) * 0.25f + 0.75f;
                    sb.Draw(px, segRect, new Rectangle(0, 0, 1, 1), color * (alpha * pulse));
                }

                if (fillHeight > 10 && ratio > 0.3f) {
                    float flowY = rect.Bottom - 6 - (float)Math.Sin(sparkTimer * 3f) * Math.Min(fillHeight * 0.7f, maxFillHeight * 0.7f);
                    for (int i = 0; i < 3; i++) {
                        float offsetY = flowY + i * 16f - 32f;
                        if (offsetY > fillRect.Y && offsetY < fillRect.Bottom) {
                            Color flowColor = highColor * (alpha * 0.6f * (1f - i * 0.3f));
                            sb.Draw(px, new Rectangle(fillRect.X, (int)offsetY, fillRect.Width, 3), new Rectangle(0, 0, 1, 1), flowColor);
                        }
                    }
                }
            }

            //边框
            Color edgeColor = Color.Lerp(new Color(120, 70, 40), new Color(180, 110, 60), (float)Math.Sin(pulseTimer * 1.3f) * 0.5f + 0.5f) * (alpha * (0.75f + hoverGlow));
            sb.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 3), new Rectangle(0, 0, 1, 1), edgeColor);
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 3, rect.Width, 3), new Rectangle(0, 0, 1, 1), edgeColor);
            sb.Draw(px, new Rectangle(rect.X, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), edgeColor);
            sb.Draw(px, new Rectangle(rect.Right - 3, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), edgeColor);

            //标签
            Vector2 labelSize = FontAssets.MouseText.Value.MeasureString(label) * 0.6f;
            Vector2 labelPos = new Vector2(rect.Center.X - labelSize.X / 2, rect.Y - 22);

            Color labelGlow = new Color(220, 140, 80) * (alpha * 0.5f);
            Utils.DrawBorderString(sb, label, labelPos + new Vector2(1.2f, 1.2f), labelGlow, 0.6f);
            Utils.DrawBorderString(sb, label, labelPos, new Color(240, 200, 160) * alpha, 0.6f);

            if (hovering) {
                string valueText = $"{(int)value}/{(int)maxValue} {EnergyUnit.Value}";
                ShowTooltip(sb, valueText);
            }
        }

        private void DrawStatusText(SpriteBatch sb) {
            float alpha = uiFadeAlpha;
            Vector2 infoCenter = new Vector2(panelRect.Center.X, panelRect.Bottom - 30);

            string statusLabel = StatusLabel.Value;
            Vector2 statusLabelPos = new Vector2(infoCenter.X - 65, infoCenter.Y);

            Color labelGlow = new Color(200, 120, 70) * (alpha * 0.5f);
            Utils.DrawBorderString(sb, statusLabel, statusLabelPos + new Vector2(1.2f, 1.2f), labelGlow, 0.7f);
            Utils.DrawBorderString(sb, statusLabel, statusLabelPos, new Color(220, 180, 140) * alpha, 0.7f);

            bool isCharging = !station.Item.IsAir && station.MachineData.UEvalue > 0.1f;
            string statusText = isCharging ? ChargingText.Value : IdleText.Value;
            Color statusColor = isCharging ? new Color(255, 180, 100) : new Color(160, 140, 120);
            Vector2 statusTextPos = new Vector2(infoCenter.X + 15, infoCenter.Y);

            if (isCharging) {
                float blink = (float)Math.Sin(sparkTimer * 4f) * 0.3f + 0.7f;
                statusColor *= blink;

                for (int i = 0; i < 3; i++) {
                    float glowAngle = MathHelper.TwoPi * i / 3f + sparkTimer;
                    Vector2 glowOffset = glowAngle.ToRotationVector2() * 1.8f;
                    Utils.DrawBorderString(sb, statusText, statusTextPos + glowOffset, statusColor * (alpha * 0.3f), 0.7f);
                }
            }

            Utils.DrawBorderString(sb, statusText, statusTextPos, statusColor * alpha, 0.7f);
        }

        private void ShowTooltip(SpriteBatch sb, string text) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float alpha = uiFadeAlpha;
            Vector2 mousePos = new Vector2(Main.mouseX, Main.mouseY);

            Vector2 textSize = FontAssets.MouseText.Value.MeasureString(text) * 0.75f;
            Vector2 textPos = mousePos + new Vector2(18, 18);

            //工业风格提示框
            Rectangle tooltipBg = new Rectangle((int)textPos.X - 10, (int)textPos.Y - 6, (int)textSize.X + 20, (int)textSize.Y + 12);
            sb.Draw(px, tooltipBg, new Rectangle(0, 0, 1, 1), new Color(15, 10, 8) * 0.95f);
            sb.Draw(px, new Rectangle(tooltipBg.X, tooltipBg.Y, tooltipBg.Width, 3), new Rectangle(0, 0, 1, 1), new Color(180, 100, 50) * 0.8f);
            sb.Draw(px, new Rectangle(tooltipBg.X, tooltipBg.Y, 3, tooltipBg.Height), new Rectangle(0, 0, 1, 1), new Color(180, 100, 50) * 0.8f);

            Utils.DrawBorderString(sb, text, textPos, new Color(255, 220, 180), 0.75f);
        }
    }

    //电火花粒子
    internal class ElectricSparkPRT
    {
        private Vector2 position;
        private Vector2 velocity;
        private float lifetime;
        private float maxLifetime;
        private float scale;
        private Color color;

        public ElectricSparkPRT(Vector2 pos) {
            position = pos;
            velocity = Main.rand.NextVector2Circular(2f, 2f);
            maxLifetime = Main.rand.NextFloat(30f, 60f);
            lifetime = 0f;
            scale = Main.rand.NextFloat(0.8f, 1.5f);
            color = new Color(100, 180, 255);
        }

        public bool Update() {
            lifetime++;
            position += velocity;
            velocity *= 0.98f;
            velocity.Y -= 0.08f;

            return lifetime >= maxLifetime;
        }

        public void Draw(SpriteBatch sb, float alpha) {
            float lifeRatio = 1f - (lifetime / maxLifetime);
            float drawAlpha = alpha * lifeRatio;

            Texture2D glow = CWRAsset.SoftGlow.Value;
            sb.Draw(glow, position - Main.screenPosition, null,
                color with { A = 0 } * drawAlpha,
                0f, glow.Size() / 2, scale * 0.15f * lifeRatio, SpriteEffects.None, 0f);
        }
    }
}
