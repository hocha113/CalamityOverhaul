using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;

namespace CalamityOverhaul.Content.ADV.Scenarios.Draedons.PQCDs
{
    /// <summary>
    /// 嘉登商店
    /// </summary>
    internal class DraedonShopUI : UIHandle
    {
        public static DraedonShopUI Instance => UIHandleLoader.GetUIHandleOfType<DraedonShopUI>();

        //UI状态
        private bool _active;
        public override bool Active {
            get => _active || uiAlpha > 0f;
            set => _active = value;
        }

        //动画参数
        private float uiAlpha = 0f;
        private float panelSlideProgress = 0f;
        private const float FadeSpeed = 0.08f;
        private const float SlideSpeed = 0.12f;

        //科技动画参数
        private float scanLineTimer = 0f;
        private float dataStreamTimer = 0f;
        private float circuitPulseTimer = 0f;
        private float hologramFlicker = 0f;
        private float hexGridPhase = 0f;
        private float energyFlowTimer = 0f;

        //商店数据
        private List<ShopItem> shopItems = new();
        private int selectedIndex = -1;
        private int hoveredIndex = -1;
        private int scrollOffset = 0;
        private const int MaxVisibleItems = 6;
        private const int ItemSlotHeight = 85;
        private float[] slotHoverProgress = new float[20];

        //UI尺寸
        private const int PanelWidth = 680;
        private const int PanelHeight = 640;
        private Vector2 panelPosition;
        private const float TechSideMargin = 35f;

        //科技粒子
        private readonly List<DataParticleFx> dataParticles = new();
        private int dataParticleSpawnTimer = 0;
        private readonly List<CircuitNodeFx> circuitNodes = new();
        private int circuitNodeSpawnTimer = 0;
        private readonly List<EnergyLine> energyLines = new();
        private int energyLineSpawnTimer = 0;

        //货币显示动画
        private float coinDisplayPulse = 0f;

        public override void Update() {
            //更新动画进度
            if (_active) {
                if (uiAlpha < 1f) uiAlpha += FadeSpeed;
                if (panelSlideProgress < 1f) panelSlideProgress += SlideSpeed;
            }
            else {
                if (uiAlpha > 0f) uiAlpha -= FadeSpeed * 1.5f;
                if (panelSlideProgress > 0f) panelSlideProgress -= SlideSpeed * 1.5f;
                if (uiAlpha <= 0f) {
                    CleanupEffects();
                    return;
                }
            }

            InitializeShop();

            uiAlpha = MathHelper.Clamp(uiAlpha, 0f, 1f);
            panelSlideProgress = MathHelper.Clamp(panelSlideProgress, 0f, 1f);

            //更新科技动画
            UpdateTechEffects();

            //更新粒子和特效
            UpdateParticles();

            //计算面板位置，从右侧滑入
            float slideOffset = (1f - CWRUtils.EaseOutCubic(panelSlideProgress)) * PanelWidth;
            panelPosition = new Vector2(Main.screenWidth - PanelWidth + slideOffset, (Main.screenHeight - PanelHeight) / 2f);

            //更新UI交互
            if (_active && panelSlideProgress > 0.9f) {
                UpdateInteraction();
            }

            //更新槽位悬停动画
            UpdateSlotHoverAnimations();
        }

        private void UpdateTechEffects() {
            scanLineTimer += 0.048f;
            dataStreamTimer += 0.055f;
            circuitPulseTimer += 0.025f;
            hologramFlicker += 0.12f;
            hexGridPhase += 0.015f;
            energyFlowTimer += 0.035f;
            coinDisplayPulse += 0.08f;

            if (scanLineTimer > MathHelper.TwoPi) scanLineTimer -= MathHelper.TwoPi;
            if (dataStreamTimer > MathHelper.TwoPi) dataStreamTimer -= MathHelper.TwoPi;
            if (circuitPulseTimer > MathHelper.TwoPi) circuitPulseTimer -= MathHelper.TwoPi;
            if (hologramFlicker > MathHelper.TwoPi) hologramFlicker -= MathHelper.TwoPi;
            if (hexGridPhase > MathHelper.TwoPi) hexGridPhase -= MathHelper.TwoPi;
            if (energyFlowTimer > MathHelper.TwoPi) energyFlowTimer -= MathHelper.TwoPi;
            if (coinDisplayPulse > MathHelper.TwoPi) coinDisplayPulse -= MathHelper.TwoPi;
        }

        private void UpdateParticles() {
            //数据粒子刷新
            dataParticleSpawnTimer++;
            if (_active && dataParticleSpawnTimer >= 15 && dataParticles.Count < 30) {
                dataParticleSpawnTimer = 0;
                Vector2 spawnPos = panelPosition + new Vector2(
                    Main.rand.NextFloat(TechSideMargin, PanelWidth - TechSideMargin),
                    Main.rand.NextFloat(50f, PanelHeight - 50f)
                );
                dataParticles.Add(new DataParticleFx(spawnPos));
            }
            for (int i = dataParticles.Count - 1; i >= 0; i--) {
                if (dataParticles[i].Update(panelPosition, new Vector2(PanelWidth, PanelHeight))) {
                    dataParticles.RemoveAt(i);
                }
            }

            //电路节点刷新
            circuitNodeSpawnTimer++;
            if (_active && circuitNodeSpawnTimer >= 30 && circuitNodes.Count < 12) {
                circuitNodeSpawnTimer = 0;
                Vector2 spawnPos = panelPosition + new Vector2(
                    Main.rand.NextFloat(TechSideMargin, PanelWidth - TechSideMargin),
                    Main.rand.NextFloat(80f, PanelHeight - 80f)
                );
                circuitNodes.Add(new CircuitNodeFx(spawnPos));
            }
            for (int i = circuitNodes.Count - 1; i >= 0; i--) {
                if (circuitNodes[i].Update(panelPosition, new Vector2(PanelWidth, PanelHeight))) {
                    circuitNodes.RemoveAt(i);
                }
            }

            //能量线刷新
            energyLineSpawnTimer++;
            if (_active && energyLineSpawnTimer >= 40 && energyLines.Count < 8) {
                energyLineSpawnTimer = 0;
                bool isVertical = Main.rand.NextBool();
                Vector2 start, end;
                if (isVertical) {
                    float x = panelPosition.X + Main.rand.NextFloat(TechSideMargin, PanelWidth - TechSideMargin);
                    start = new Vector2(x, panelPosition.Y + 70);
                    end = new Vector2(x, panelPosition.Y + PanelHeight - 30);
                }
                else {
                    float y = panelPosition.Y + Main.rand.NextFloat(100f, PanelHeight - 100f);
                    start = new Vector2(panelPosition.X + TechSideMargin, y);
                    end = new Vector2(panelPosition.X + PanelWidth - TechSideMargin, y);
                }
                energyLines.Add(new EnergyLine(start, end));
            }
            for (int i = energyLines.Count - 1; i >= 0; i--) {
                if (energyLines[i].Update()) {
                    energyLines.RemoveAt(i);
                }
            }
        }

        private void UpdateSlotHoverAnimations() {
            for (int i = 0; i < slotHoverProgress.Length; i++) {
                float target = (i == hoveredIndex) ? 1f : 0f;
                slotHoverProgress[i] = MathHelper.Lerp(slotHoverProgress[i], target, 0.15f);
            }
        }

        private void CleanupEffects() {
            dataParticles.Clear();
            circuitNodes.Clear();
            energyLines.Clear();
            hoveredIndex = -1;
            selectedIndex = -1;
        }

        private void UpdateInteraction() {
            UIHitBox = new Rectangle(
                (int)panelPosition.X,
                (int)panelPosition.Y,
                PanelWidth,
                PanelHeight
            );

            hoverInMainPage = UIHitBox.Intersects(MouseHitBox);

            if (hoverInMainPage) {
                player.mouseInterface = true;
                player.CWR().DontSwitchWeaponTime = 2;

                //滚轮滚动
                int scrollDelta = PlayerInput.ScrollWheelDeltaForUI;
                if (scrollDelta != 0) {
                    int maxScroll = Math.Max(0, shopItems.Count - MaxVisibleItems);
                    int oldOffset = scrollOffset;
                    scrollOffset = Math.Clamp(scrollOffset - Math.Sign(scrollDelta), 0, maxScroll);
                    if (oldOffset != scrollOffset) {
                        SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.25f, Pitch = -0.3f });
                    }
                }

                //检测物品点击和悬停
                UpdateItemSelection(MousePosition.ToPoint());
            }
            else if (keyLeftPressState == KeyPressState.Pressed && uiAlpha >= 1f) {
                _active = false;
                SoundEngine.PlaySound(SoundID.MenuClose with { Pitch = 0.2f });
            }

            //ESC关闭
            if (Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape)) {
                _active = false;
                SoundEngine.PlaySound(SoundID.MenuClose with { Pitch = 0.2f });
            }
        }

        private void UpdateItemSelection(Point mousePoint) {
            int itemListY = (int)(panelPosition.Y + 120);
            int itemListX = (int)(panelPosition.X + 30);
            int oldHoveredIndex = hoveredIndex;
            hoveredIndex = -1;

            for (int i = 0; i < Math.Min(MaxVisibleItems, shopItems.Count - scrollOffset); i++) {
                int index = i + scrollOffset;
                Rectangle itemRect = new Rectangle(
                    itemListX,
                    itemListY + i * ItemSlotHeight,
                    PanelWidth - 60,
                    ItemSlotHeight - 8
                );

                if (itemRect.Contains(mousePoint)) {
                    hoveredIndex = index;

                    //悬停音效
                    if (oldHoveredIndex != hoveredIndex && hoveredIndex != -1) {
                        SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.2f, Pitch = 0.4f });
                    }

                    if (Main.mouseLeft && Main.mouseLeftRelease) {
                        selectedIndex = index;
                        TryPurchaseItem(index);
                    }
                    break;
                }
            }
        }

        private void TryPurchaseItem(int index) {
            if (index < 0 || index >= shopItems.Count) return;

            ShopItem shopItem = shopItems[index];

            //检查是否有足够的钱币
            if (player.BuyItem(shopItem.price)) {
                //给予物品
                player.QuickSpawnItem(player.GetSource_OpenItem(shopItem.itemType), shopItem.itemType, shopItem.stack);
                SoundEngine.PlaySound(SoundID.Coins);
                SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.6f, Pitch = 0.3f });
            }
            else {
                SoundEngine.PlaySound(SoundID.MenuClose with { Pitch = -0.5f, Volume = 0.8f });
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (uiAlpha <= 0f) return;

            //绘制半透明背景
            DrawDarkenBackground(spriteBatch);

            //绘制主面板
            DrawMainPanel(spriteBatch);

            //绘制标题和货币显示
            DrawHeader(spriteBatch);

            //绘制物品列表
            DrawItemList(spriteBatch);

            //绘制科技特效
            DrawTechEffects(spriteBatch);

            //绘制滚动条提示
            DrawScrollHint(spriteBatch);
        }

        private void DrawDarkenBackground(SpriteBatch spriteBatch) {
            Rectangle fullScreen = new Rectangle(0, 0, Main.screenWidth, Main.screenHeight);
            Texture2D pixel = VaultAsset.placeholder2.Value;
            spriteBatch.Draw(pixel, fullScreen, Color.Black * (uiAlpha * 0.7f));
        }

        private void DrawMainPanel(SpriteBatch spriteBatch) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle panelRect = new Rectangle(
                (int)panelPosition.X,
                (int)panelPosition.Y,
                PanelWidth,
                PanelHeight
            );

            //深度阴影
            Rectangle shadow = panelRect;
            shadow.Offset(6, 8);
            spriteBatch.Draw(pixel, shadow, Color.Black * (uiAlpha * 0.7f));

            //主背景渐变
            int segments = 50;
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                float t2 = (i + 1) / (float)segments;
                int y1 = panelRect.Y + (int)(t * panelRect.Height);
                int y2 = panelRect.Y + (int)(t2 * panelRect.Height);
                Rectangle segment = new Rectangle(panelRect.X, y1, panelRect.Width, Math.Max(1, y2 - y1));

                Color techDark = new Color(8, 12, 22);
                Color techMid = new Color(18, 28, 42);
                Color techEdge = new Color(35, 55, 85);

                float pulse = (float)Math.Sin(circuitPulseTimer * 0.6f + t * 2.5f) * 0.5f + 0.5f;
                Color blendBase = Color.Lerp(techDark, techMid, pulse);
                Color finalColor = Color.Lerp(blendBase, techEdge, t * 0.5f) * (uiAlpha * 0.94f);

                spriteBatch.Draw(pixel, segment, finalColor);
            }

            //全息闪烁叠加
            float flicker = (float)Math.Sin(hologramFlicker * 1.5f) * 0.5f + 0.5f;
            spriteBatch.Draw(pixel, panelRect, new Color(15, 30, 45) * (uiAlpha * 0.25f * flicker));

            //六角网格
            DrawHexGrid(spriteBatch, panelRect);

            //扫描线
            DrawScanLines(spriteBatch, panelRect);

            //内部脉冲发光
            float innerPulse = (float)Math.Sin(circuitPulseTimer * 1.3f) * 0.5f + 0.5f;
            Rectangle inner = panelRect;
            inner.Inflate(-5, -5);
            spriteBatch.Draw(pixel, inner, new Color(40, 180, 255) * (uiAlpha * 0.12f * innerPulse));

            //科技边框
            DrawTechFrame(spriteBatch, panelRect, innerPulse);
        }

        private void DrawHexGrid(SpriteBatch spriteBatch, Rectangle rect) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            int hexRows = 12;
            float hexHeight = rect.Height / (float)hexRows;

            for (int row = 0; row < hexRows; row++) {
                float t = row / (float)hexRows;
                float y = rect.Y + row * hexHeight;
                float phase = hexGridPhase + t * MathHelper.Pi;
                float brightness = (float)Math.Sin(phase) * 0.5f + 0.5f;

                Color gridColor = new Color(25, 90, 140) * (uiAlpha * 0.05f * brightness);
                spriteBatch.Draw(pixel, new Rectangle(rect.X + 15, (int)y, rect.Width - 30, 1), gridColor);
            }

            //垂直网格线
            int hexCols = 15;
            float hexWidth = rect.Width / (float)hexCols;
            for (int col = 0; col < hexCols; col++) {
                float t = col / (float)hexCols;
                float x = rect.X + col * hexWidth;
                float phase = hexGridPhase + t * MathHelper.Pi * 0.7f;
                float brightness = (float)Math.Sin(phase) * 0.5f + 0.5f;

                Color gridColor = new Color(25, 90, 140) * (uiAlpha * 0.04f * brightness);
                spriteBatch.Draw(pixel, new Rectangle((int)x, rect.Y + 20, 1, rect.Height - 40), gridColor);
            }
        }

        private void DrawScanLines(SpriteBatch spriteBatch, Rectangle rect) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            float scanY = rect.Y + (float)Math.Sin(scanLineTimer) * 0.5f * rect.Height + rect.Height * 0.5f;

            for (int i = -3; i <= 3; i++) {
                float offsetY = scanY + i * 3.5f;
                if (offsetY < rect.Y || offsetY > rect.Bottom) continue;

                float intensity = 1f - Math.Abs(i) * 0.25f;
                Color scanColor = new Color(60, 180, 255) * (uiAlpha * 0.18f * intensity);
                spriteBatch.Draw(pixel, new Rectangle(rect.X + 12, (int)offsetY, rect.Width - 24, 2), scanColor);
            }
        }

        private void DrawTechFrame(SpriteBatch spriteBatch, Rectangle rect, float pulse) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Color borderColor = Color.Lerp(new Color(40, 160, 240), new Color(80, 200, 255), pulse) * (uiAlpha * 0.9f);

            //外边框
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 4), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 4, rect.Width, 4), borderColor * 0.75f);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 4, rect.Height), borderColor * 0.9f);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - 4, rect.Y, 4, rect.Height), borderColor * 0.9f);

            //内发光边框
            Rectangle inner = rect;
            inner.Inflate(-8, -8);
            Color innerGlow = new Color(100, 200, 255) * (uiAlpha * 0.25f * pulse);
            spriteBatch.Draw(pixel, new Rectangle(inner.X, inner.Y, inner.Width, 2), innerGlow);
            spriteBatch.Draw(pixel, new Rectangle(inner.X, inner.Bottom - 2, inner.Width, 2), innerGlow * 0.7f);
            spriteBatch.Draw(pixel, new Rectangle(inner.X, inner.Y, 2, inner.Height), innerGlow * 0.9f);
            spriteBatch.Draw(pixel, new Rectangle(inner.Right - 2, inner.Y, 2, inner.Height), innerGlow * 0.9f);

            //角落电路装饰
            DrawCornerCircuit(spriteBatch, new Vector2(rect.X + 15, rect.Y + 15), uiAlpha);
            DrawCornerCircuit(spriteBatch, new Vector2(rect.Right - 15, rect.Y + 15), uiAlpha);
            DrawCornerCircuit(spriteBatch, new Vector2(rect.X + 15, rect.Bottom - 15), uiAlpha * 0.7f);
            DrawCornerCircuit(spriteBatch, new Vector2(rect.Right - 15, rect.Bottom - 15), uiAlpha * 0.7f);
        }

        private static void DrawCornerCircuit(SpriteBatch sb, Vector2 pos, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float size = 8f;
            Color c = new Color(100, 220, 255) * alpha;

            sb.Draw(px, pos, null, c, 0f, new Vector2(0.5f), new Vector2(size, size * 0.2f), SpriteEffects.None, 0f);
            sb.Draw(px, pos, null, c * 0.85f, MathHelper.PiOver2, new Vector2(0.5f), new Vector2(size, size * 0.2f), SpriteEffects.None, 0f);
            sb.Draw(px, pos, null, c * 0.6f, 0f, new Vector2(0.5f), new Vector2(size * 0.5f), SpriteEffects.None, 0f);
        }

        private void DrawHeader(SpriteBatch spriteBatch) {
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            
            //标题
            string title = "DRAEDON QUANTUM STORE";
            Vector2 titleSize = font.MeasureString(title) * 1.3f;
            Vector2 titlePos = panelPosition + new Vector2((PanelWidth - titleSize.X) / 2f, 25);

            //标题发光效果
            Color glowColor = new Color(80, 220, 255) * (uiAlpha * 0.9f);
            for (int i = 0; i < 8; i++) {
                float angle = MathHelper.TwoPi * i / 8f;
                Vector2 offset = angle.ToRotationVector2() * 3f;
                Utils.DrawBorderString(spriteBatch, title, titlePos + offset, glowColor * 0.5f, 1.3f);
            }
            Utils.DrawBorderString(spriteBatch, title, titlePos, Color.White * uiAlpha, 1.3f);

            //分隔线
            Vector2 lineStart = panelPosition + new Vector2(40, 75);
            Vector2 lineEnd = lineStart + new Vector2(PanelWidth - 80, 0);
            DrawGradientLine(spriteBatch, lineStart, lineEnd, 
                new Color(60, 160, 240) * (uiAlpha * 0.9f), 
                new Color(60, 160, 240) * (uiAlpha * 0.1f), 2f);

            //货币显示
            DrawCurrencyDisplay(spriteBatch);
        }

        private void DrawCurrencyDisplay(SpriteBatch spriteBatch) {
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            Vector2 currencyPos = panelPosition + new Vector2(40, 85);

            //计算玩家总货币
            long totalCopper = 0;
            for (int i = 0; i < 58; i++) {
                Item item = player.inventory[i];
                if (item.type == ItemID.CopperCoin) totalCopper += item.stack;
                if (item.type == ItemID.SilverCoin) totalCopper += item.stack * 100;
                if (item.type == ItemID.GoldCoin) totalCopper += item.stack * 10000;
                if (item.type == ItemID.PlatinumCoin) totalCopper += item.stack * 1000000;
            }

            int platinum = (int)(totalCopper / 1000000);
            int gold = (int)((totalCopper % 1000000) / 10000);
            int silver = (int)((totalCopper % 10000) / 100);
            int copper = (int)(totalCopper % 100);

            string currencyText = $"FUNDS: {platinum}p {gold}g {silver}s {copper}c";
            float pulse = (float)Math.Sin(coinDisplayPulse) * 0.5f + 0.5f;
            Color currencyColor = Color.Lerp(new Color(255, 215, 0), new Color(255, 255, 200), pulse * 0.3f) * uiAlpha;

            //货币文字发光
            Color currencyGlow = new Color(255, 215, 0) * (uiAlpha * 0.6f);
            for (int i = 0; i < 4; i++) {
                float angle = MathHelper.TwoPi * i / 4f;
                Vector2 offset = angle.ToRotationVector2() * 1.5f;
                Utils.DrawBorderString(spriteBatch, currencyText, currencyPos + offset, currencyGlow * 0.5f, 0.85f);
            }
            Utils.DrawBorderString(spriteBatch, currencyText, currencyPos, currencyColor, 0.85f);
        }

        private void DrawItemList(SpriteBatch spriteBatch) {
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            int itemListY = (int)(panelPosition.Y + 120);
            int itemListX = (int)(panelPosition.X + 30);

            for (int i = 0; i < Math.Min(MaxVisibleItems, shopItems.Count - scrollOffset); i++) {
                int index = i + scrollOffset;
                ShopItem shopItem = shopItems[index];

                Vector2 itemPos = new Vector2(itemListX, itemListY + i * ItemSlotHeight);
                bool isHovered = index == hoveredIndex;
                bool isSelected = index == selectedIndex;
                float hoverProgress = slotHoverProgress[Math.Min(index, slotHoverProgress.Length - 1)];

                DrawShopItemSlot(spriteBatch, shopItem, itemPos, isHovered, isSelected, hoverProgress, font);
            }
        }

        private void DrawShopItemSlot(SpriteBatch spriteBatch, ShopItem shopItem, Vector2 position, 
            bool isHovered, bool isSelected, float hoverProgress, DynamicSpriteFont font) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle slotRect = new Rectangle(
                (int)position.X,
                (int)position.Y,
                PanelWidth - 60,
                ItemSlotHeight - 8
            );

            //槽位背景
            Color baseSlotColor = new Color(20, 40, 60);
            Color hoverSlotColor = new Color(40, 80, 120);
            Color selectedSlotColor = new Color(60, 120, 180);

            Color slotColor = baseSlotColor;
            if (isSelected) {
                slotColor = Color.Lerp(hoverSlotColor, selectedSlotColor, 0.6f);
            }
            else if (isHovered) {
                slotColor = Color.Lerp(baseSlotColor, hoverSlotColor, hoverProgress);
            }
            slotColor *= uiAlpha * 0.6f;

            spriteBatch.Draw(pixel, slotRect, slotColor);

            //槽位边框
            float borderPulse = (float)Math.Sin(circuitPulseTimer * 1.5f + position.Y * 0.01f) * 0.5f + 0.5f;
            Color borderColor = Color.Lerp(
                new Color(40, 140, 200),
                new Color(80, 200, 255),
                borderPulse * hoverProgress
            ) * (uiAlpha * (0.4f + hoverProgress * 0.4f));

            spriteBatch.Draw(pixel, new Rectangle(slotRect.X, slotRect.Y, slotRect.Width, 2), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(slotRect.X, slotRect.Bottom - 2, slotRect.Width, 2), borderColor * 0.7f);

            //悬停发光效果
            if (hoverProgress > 0.01f) {
                Rectangle glowRect = slotRect;
                glowRect.Inflate(2, 2);
                Color glowColor = new Color(80, 200, 255) * (uiAlpha * 0.15f * hoverProgress);
                spriteBatch.Draw(pixel, glowRect, glowColor);
            }

            //物品图标
            Texture2D itemTexture = TextureAssets.Item[shopItem.itemType].Value;
            Rectangle itemFrame = Main.itemAnimations[shopItem.itemType]?.GetFrame(itemTexture) ?? itemTexture.Bounds;
            float itemScale = Math.Min(56f / itemFrame.Width, 56f / itemFrame.Height);
            itemScale *= 1f + hoverProgress * 0.1f; //悬停放大效果
            Vector2 itemDrawPos = position + new Vector2(42, ItemSlotHeight / 2f - 4);
            
            //物品图标发光
            if (hoverProgress > 0.01f) {
                Color itemGlow = new Color(80, 200, 255) * (uiAlpha * 0.3f * hoverProgress);
                spriteBatch.Draw(itemTexture, itemDrawPos, itemFrame, itemGlow, 0f, 
                    itemFrame.Size() / 2f, itemScale * 1.15f, SpriteEffects.None, 0f);
            }
            spriteBatch.Draw(itemTexture, itemDrawPos, itemFrame, Color.White * uiAlpha, 0f, 
                itemFrame.Size() / 2f, itemScale, SpriteEffects.None, 0f);

            //物品名称
            string itemName = Lang.GetItemNameValue(shopItem.itemType);
            Vector2 namePos = position + new Vector2(90, 18);
            Color nameColor = Color.Lerp(new Color(200, 230, 255), new Color(255, 255, 255), hoverProgress) * uiAlpha;
            Utils.DrawBorderString(spriteBatch, itemName, namePos, nameColor, 0.9f);

            //价格显示
            DrawPriceDisplay(spriteBatch, shopItem, position, hoverProgress);

            //数据流效果
            float dataShift = (float)Math.Sin(dataStreamTimer * 1.5f + position.Y * 0.02f) * (1f + hoverProgress);
            Vector2 dataLinePos = position + new Vector2(dataShift + 90, ItemSlotHeight / 2f + 5);
            Color dataColor = new Color(60, 160, 240) * (uiAlpha * (0.15f + hoverProgress * 0.15f));
            spriteBatch.Draw(pixel, new Rectangle((int)dataLinePos.X, (int)dataLinePos.Y, 
                (int)((PanelWidth - 150) * (0.7f + hoverProgress * 0.3f)), 1), dataColor);
        }

        private void DrawPriceDisplay(SpriteBatch spriteBatch, ShopItem shopItem, Vector2 position, float hoverProgress) {
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            Vector2 pricePos = position + new Vector2(90, 45);

            int platinum = shopItem.price / 1000000;
            int gold = (shopItem.price % 1000000) / 10000;
            int silver = (shopItem.price % 10000) / 100;
            int copper = shopItem.price % 100;

            string priceText = "";
            if (platinum > 0) priceText += $"{platinum}p ";
            if (gold > 0) priceText += $"{gold}g ";
            if (silver > 0) priceText += $"{silver}s ";
            if (copper > 0 || priceText == "") priceText += $"{copper}c";

            bool canAfford = player.CanAfford(shopItem.price);
            Color priceColor = canAfford
                ? Color.Lerp(new Color(255, 215, 0), new Color(255, 255, 100), hoverProgress)
                : Color.Lerp(new Color(255, 100, 100), new Color(255, 150, 150), hoverProgress);
            priceColor *= uiAlpha;

            //价格发光
            if (canAfford && hoverProgress > 0.01f) {
                Color priceGlow = new Color(255, 215, 0) * (uiAlpha * 0.5f * hoverProgress);
                for (int i = 0; i < 4; i++) {
                    float angle = MathHelper.TwoPi * i / 4f;
                    Vector2 offset = angle.ToRotationVector2() * 1.2f;
                    Utils.DrawBorderString(spriteBatch, priceText, pricePos + offset, priceGlow * 0.4f, 0.8f);
                }
            }

            Utils.DrawBorderString(spriteBatch, priceText, pricePos, priceColor, 0.8f);
        }

        private void DrawTechEffects(SpriteBatch spriteBatch) {
            //绘制能量线
            foreach (var line in energyLines) {
                line.Draw(spriteBatch, uiAlpha);
            }

            //绘制电路节点
            foreach (var node in circuitNodes) {
                node.Draw(spriteBatch, uiAlpha * 0.85f);
            }

            //绘制数据粒子
            foreach (var particle in dataParticles) {
                particle.Draw(spriteBatch, uiAlpha * 0.75f);
            }
        }

        private void DrawScrollHint(SpriteBatch spriteBatch) {
            if (shopItems.Count <= MaxVisibleItems) return;

            DynamicSpriteFont font = FontAssets.MouseText.Value;
            int maxScroll = Math.Max(0, shopItems.Count - MaxVisibleItems);
            
            //滚动进度条
            float scrollProgress = maxScroll > 0 ? scrollOffset / (float)maxScroll : 0f;
            Vector2 barPos = panelPosition + new Vector2(PanelWidth - 20, 120);
            int barHeight = (MaxVisibleItems * ItemSlotHeight) - 20;
            
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle barBg = new Rectangle((int)barPos.X, (int)barPos.Y, 4, barHeight);
            spriteBatch.Draw(pixel, barBg, new Color(40, 100, 150) * (uiAlpha * 0.3f));

            int indicatorHeight = Math.Max(20, barHeight * MaxVisibleItems / shopItems.Count);
            int indicatorY = (int)(barPos.Y + scrollProgress * (barHeight - indicatorHeight));
            Rectangle indicator = new Rectangle((int)barPos.X - 1, indicatorY, 6, indicatorHeight);
            Color indicatorColor = new Color(80, 200, 255) * uiAlpha;
            spriteBatch.Draw(pixel, indicator, indicatorColor);

            //滚动提示文字
            if (scrollOffset > 0 || scrollOffset < maxScroll) {
                string hint = $"▲▼ [{scrollOffset + 1}/{shopItems.Count}]";
                Vector2 hintPos = panelPosition + new Vector2(PanelWidth - 35, PanelHeight - 25);
                Utils.DrawBorderString(spriteBatch, hint, hintPos, new Color(100, 180, 230) * (uiAlpha * 0.6f), 0.7f);
            }
        }

        private static void DrawGradientLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color startColor, Color endColor, float thickness) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 edge = end - start;
            float length = edge.Length();
            if (length < 1f) return;

            edge.Normalize();
            float rotation = (float)Math.Atan2(edge.Y, edge.X);
            int segments = Math.Max(1, (int)(length / 10f));

            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                Vector2 segPos = start + edge * (length * t);
                float segLength = length / segments;
                Color color = Color.Lerp(startColor, endColor, t);
                spriteBatch.Draw(pixel, segPos, null, color, rotation, new Vector2(0, 0.5f), 
                    new Vector2(segLength, thickness), SpriteEffects.None, 0f);
            }
        }

        #region 粒子特效类
        private class DataParticleFx
        {
            public Vector2 Pos;
            public float Size;
            public float Rot;
            public float Life;
            public float MaxLife;
            public float Seed;
            public Vector2 Velocity;

            public DataParticleFx(Vector2 p) {
                Pos = p;
                Size = Main.rand.NextFloat(1.5f, 3.5f);
                Rot = Main.rand.NextFloat(MathHelper.TwoPi);
                Life = 0f;
                MaxLife = Main.rand.NextFloat(80f, 150f);
                Seed = Main.rand.NextFloat(10f);
                Velocity = new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(-0.6f, -0.2f));
            }

            public bool Update(Vector2 panelPos, Vector2 panelSize) {
                Life++;
                Rot += 0.025f;
                Pos += Velocity;
                Velocity.Y -= 0.015f;

                if (Life >= MaxLife) return true;
                if (Pos.X < panelPos.X - 50 || Pos.X > panelPos.X + panelSize.X + 50 || 
                    Pos.Y < panelPos.Y - 50 || Pos.Y > panelPos.Y + panelSize.Y + 50) {
                    return true;
                }
                return false;
            }

            public void Draw(SpriteBatch sb, float alpha) {
                float t = Life / MaxLife;
                float fade = (float)Math.Sin(t * MathHelper.Pi) * alpha;
                float scale = Size * (0.7f + (float)Math.Sin((Life + Seed * 40f) * 0.09f) * 0.3f);

                Color c = new Color(80, 200, 255) * (0.8f * fade);
                Texture2D px = VaultAsset.placeholder2.Value;

                sb.Draw(px, Pos, null, c, Rot, new Vector2(0.5f), 
                    new Vector2(scale * 2f, scale * 0.3f), SpriteEffects.None, 0f);
                sb.Draw(px, Pos, null, c * 0.9f, Rot + MathHelper.PiOver2, new Vector2(0.5f), 
                    new Vector2(scale * 2f, scale * 0.3f), SpriteEffects.None, 0f);
            }
        }

        private class CircuitNodeFx
        {
            public Vector2 Pos;
            public float Radius;
            public float PulseSpeed;
            public float Life;
            public float MaxLife;
            public float Seed;

            public CircuitNodeFx(Vector2 start) {
                Pos = start;
                Radius = Main.rand.NextFloat(2f, 5f);
                PulseSpeed = Main.rand.NextFloat(0.8f, 1.6f);
                Life = 0f;
                MaxLife = Main.rand.NextFloat(100f, 180f);
                Seed = Main.rand.NextFloat(10f);
            }

            public bool Update(Vector2 panelPos, Vector2 panelSize) {
                Life++;
                return Life >= MaxLife;
            }

            public void Draw(SpriteBatch sb, float alpha) {
                Texture2D px = VaultAsset.placeholder2.Value;
                float t = Life / MaxLife;
                float fade = (float)Math.Sin(t * MathHelper.Pi);
                float pulse = (float)Math.Sin((Life + Seed * 20f) * 0.08f * PulseSpeed) * 0.5f + 0.5f;
                float scale = Radius * (0.8f + pulse * 0.4f);

                Color core = new Color(100, 220, 255) * (alpha * 0.7f * fade);
                Color ring = new Color(40, 140, 200) * (alpha * 0.5f * fade);

                sb.Draw(px, Pos, null, ring, 0f, new Vector2(0.5f), 
                    new Vector2(scale * 2.2f), SpriteEffects.None, 0f);
                sb.Draw(px, Pos, null, core, 0f, new Vector2(0.5f), 
                    new Vector2(scale), SpriteEffects.None, 0f);
                sb.Draw(px, Pos, null, core * 0.4f, 0f, new Vector2(0.5f), 
                    new Vector2(scale * 0.3f), SpriteEffects.None, 0f);
            }
        }

        private class EnergyLine
        {
            public Vector2 Start;
            public Vector2 End;
            public float Life;
            public float MaxLife;
            public float FlowSpeed;
            public float FlowOffset;

            public EnergyLine(Vector2 start, Vector2 end) {
                Start = start;
                End = end;
                Life = 0f;
                MaxLife = Main.rand.NextFloat(120f, 200f);
                FlowSpeed = Main.rand.NextFloat(0.02f, 0.05f);
                FlowOffset = Main.rand.NextFloat(MathHelper.TwoPi);
            }

            public bool Update() {
                Life++;
                FlowOffset += FlowSpeed;
                if (FlowOffset > MathHelper.TwoPi) FlowOffset -= MathHelper.TwoPi;
                return Life >= MaxLife;
            }

            public void Draw(SpriteBatch sb, float alpha) {
                float t = Life / MaxLife;
                float fade = (float)Math.Sin(t * MathHelper.Pi);
                
                Vector2 direction = End - Start;
                float length = direction.Length();
                direction.Normalize();

                int segments = (int)(length / 15f);
                for (int i = 0; i < segments; i++) {
                    float segT = i / (float)segments;
                    float wave = (float)Math.Sin(FlowOffset + segT * MathHelper.TwoPi * 2f) * 0.5f + 0.5f;
                    
                    Vector2 pos = Start + direction * (length * segT);
                    Color color = new Color(60, 160, 240) * (alpha * 0.15f * fade * wave);
                    
                    Texture2D px = VaultAsset.placeholder2.Value;
                    sb.Draw(px, pos, null, color, 0f, new Vector2(0.5f), 
                        new Vector2(3f, 1.5f), SpriteEffects.None, 0f);
                }
            }
        }
        #endregion

        //商店物品数据
        public class ShopItem
        {
            public int itemType;
            public int stack;
            public int price;

            public ShopItem(int itemType, int stack, int price) {
                Main.instance.LoadItem(itemType);
                this.itemType = itemType;
                this.stack = stack;
                this.price = price;
            }
        }

        //初始化商店物品
        public void InitializeShop() {
            if (shopItems.Count > 0) return;
            //添加嘉登材料合成的物品
            ShopHandle.Handle(shopItems);
        }
    }
}
