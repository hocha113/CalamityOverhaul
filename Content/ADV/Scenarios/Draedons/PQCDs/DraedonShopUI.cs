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

        //商店数据
        private List<ShopItem> shopItems = new();
        private int selectedIndex = -1;
        private int scrollOffset = 0;
        private const int MaxVisibleItems = 6;
        private const int ItemSlotHeight = 80;

        //UI尺寸
        private const int PanelWidth = 600;
        private const int PanelHeight = 600;
        private Vector2 panelPosition;

        //科技粒子
        private readonly List<TechParticle> techParticles = new();
        private int particleSpawnTimer = 0;

        public override void Update() {
            //更新动画进度
            if (_active) {
                if (uiAlpha < 1f) uiAlpha += FadeSpeed;
                if (panelSlideProgress < 1f) panelSlideProgress += SlideSpeed;
            }
            else {
                if (uiAlpha > 0f) uiAlpha -= FadeSpeed * 1.5f;
                if (panelSlideProgress > 0f) panelSlideProgress -= SlideSpeed * 1.5f;
                if (uiAlpha <= 0f) return;
            }

            InitializeShop();

            uiAlpha = MathHelper.Clamp(uiAlpha, 0f, 1f);
            panelSlideProgress = MathHelper.Clamp(panelSlideProgress, 0f, 1f);

            //更新科技动画
            UpdateTechEffects();

            //更新粒子
            UpdateParticles();

            //计算面板位置（从右侧滑入）
            float slideOffset = (1f - CWRUtils.EaseOutCubic(panelSlideProgress)) * PanelWidth;
            panelPosition = new Vector2(Main.screenWidth - PanelWidth + slideOffset, (Main.screenHeight - PanelHeight) / 2f);

            //更新UI交互
            if (_active && panelSlideProgress > 0.9f) {
                UpdateInteraction();
            }
        }

        private void UpdateTechEffects() {
            scanLineTimer += 0.045f;
            dataStreamTimer += 0.055f;
            circuitPulseTimer += 0.028f;
            hologramFlicker += 0.12f;

            if (scanLineTimer > MathHelper.TwoPi) scanLineTimer -= MathHelper.TwoPi;
            if (dataStreamTimer > MathHelper.TwoPi) dataStreamTimer -= MathHelper.TwoPi;
            if (circuitPulseTimer > MathHelper.TwoPi) circuitPulseTimer -= MathHelper.TwoPi;
            if (hologramFlicker > MathHelper.TwoPi) hologramFlicker -= MathHelper.TwoPi;
        }

        private void UpdateParticles() {
            //生成新粒子
            particleSpawnTimer++;
            if (_active && particleSpawnTimer >= 20 && techParticles.Count < 25) {
                particleSpawnTimer = 0;
                Vector2 spawnPos = panelPosition + new Vector2(
                    Main.rand.NextFloat(50f, PanelWidth - 50f),
                    Main.rand.NextFloat(50f, PanelHeight - 50f)
                );
                techParticles.Add(new TechParticle(spawnPos));
            }

            //更新现有粒子
            for (int i = techParticles.Count - 1; i >= 0; i--) {
                if (techParticles[i].Update()) {
                    techParticles.RemoveAt(i);
                }
            }
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
                    scrollOffset = Math.Clamp(scrollOffset - Math.Sign(scrollDelta), 0, maxScroll);
                    SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.3f });
                }

                //检测物品点击
                UpdateItemSelection(MousePosition.ToPoint());
            }
            else if (keyLeftPressState == KeyPressState.Pressed && uiAlpha >= 1f) {
                _active = false;
                SoundEngine.PlaySound(SoundID.MenuClose);
            }

            //ESC关闭
            if (Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape)) {
                _active = false;
                SoundEngine.PlaySound(SoundID.MenuClose);
            }
        }

        private void UpdateItemSelection(Point mousePoint) {
            int itemListY = (int)(panelPosition.Y + 100);
            int itemListX = (int)(panelPosition.X + 20);

            for (int i = 0; i < Math.Min(MaxVisibleItems, shopItems.Count - scrollOffset); i++) {
                int index = i + scrollOffset;
                Rectangle itemRect = new Rectangle(
                    itemListX,
                    itemListY + i * ItemSlotHeight,
                    PanelWidth - 40,
                    ItemSlotHeight - 10
                );

                if (itemRect.Contains(mousePoint)) {
                    selectedIndex = index;

                    if (Main.mouseLeft && Main.mouseLeftRelease) {
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
            }
            else {
                SoundEngine.PlaySound(SoundID.MenuClose with { Pitch = -0.5f });
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (uiAlpha <= 0f) return;

            //绘制半透明背景
            DrawDarkenBackground(spriteBatch);

            //绘制主面板
            DrawMainPanel(spriteBatch);

            //绘制标题
            DrawTitle(spriteBatch);

            //绘制物品列表
            DrawItemList(spriteBatch);

            //绘制科技粒子
            DrawTechParticles(spriteBatch);
        }

        private void DrawDarkenBackground(SpriteBatch spriteBatch) {
            Rectangle fullScreen = new Rectangle(0, 0, Main.screenWidth, Main.screenHeight);
            Texture2D pixel = VaultAsset.placeholder2.Value;
            spriteBatch.Draw(pixel, fullScreen, Color.Black * (uiAlpha * 0.6f));
        }

        private void DrawMainPanel(SpriteBatch spriteBatch) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle panelRect = new Rectangle(
                (int)panelPosition.X,
                (int)panelPosition.Y,
                PanelWidth,
                PanelHeight
            );

            //主背景渐变
            int segments = 40;
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                float t2 = (i + 1) / (float)segments;
                int y1 = panelRect.Y + (int)(t * panelRect.Height);
                int y2 = panelRect.Y + (int)(t2 * panelRect.Height);
                Rectangle segment = new Rectangle(panelRect.X, y1, panelRect.Width, Math.Max(1, y2 - y1));

                float pulse = (float)Math.Sin(circuitPulseTimer * 0.6f + t * 2.0f) * 0.5f + 0.5f;
                Color baseColor = Color.Lerp(new Color(8, 12, 22), new Color(18, 28, 42), pulse);
                Color finalColor = Color.Lerp(baseColor, new Color(35, 55, 85), t * 0.45f) * uiAlpha;

                spriteBatch.Draw(pixel, segment, finalColor);
            }

            //全息闪烁
            float flicker = (float)Math.Sin(hologramFlicker * 1.5f) * 0.5f + 0.5f;
            spriteBatch.Draw(pixel, panelRect, new Color(15, 30, 45) * (uiAlpha * 0.25f * flicker));

            //扫描线
            DrawScanLines(spriteBatch, panelRect);

            //科技边框
            DrawTechBorder(spriteBatch, panelRect);
        }

        private void DrawScanLines(SpriteBatch spriteBatch, Rectangle rect) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            float scanY = rect.Y + (float)Math.Sin(scanLineTimer) * 0.5f * rect.Height + rect.Height * 0.5f;

            for (int i = -2; i <= 2; i++) {
                float offsetY = scanY + i * 3f;
                if (offsetY < rect.Y || offsetY > rect.Bottom) continue;

                float intensity = 1f - Math.Abs(i) * 0.3f;
                Color scanColor = new Color(60, 180, 255) * (uiAlpha * 0.15f * intensity);
                spriteBatch.Draw(pixel, new Rectangle(rect.X + 8, (int)offsetY, rect.Width - 16, 2), scanColor);
            }
        }

        private void DrawTechBorder(SpriteBatch spriteBatch, Rectangle rect) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            float pulse = (float)Math.Sin(circuitPulseTimer * 1.3f) * 0.5f + 0.5f;
            Color borderColor = Color.Lerp(new Color(40, 160, 240), new Color(80, 200, 255), pulse) * (uiAlpha * 0.85f);

            //外边框
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 3), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 3, rect.Width, 3), borderColor * 0.75f);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 3, rect.Height), borderColor * 0.9f);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - 3, rect.Y, 3, rect.Height), borderColor * 0.9f);

            //内发光
            Rectangle inner = rect;
            inner.Inflate(-6, -6);
            Color innerGlow = new Color(100, 200, 255) * (uiAlpha * 0.22f * pulse);
            spriteBatch.Draw(pixel, new Rectangle(inner.X, inner.Y, inner.Width, 1), innerGlow);
            spriteBatch.Draw(pixel, new Rectangle(inner.X, inner.Bottom - 1, inner.Width, 1), innerGlow * 0.7f);
        }

        private void DrawTitle(SpriteBatch spriteBatch) {
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            string title = "DRAEDON QUANTUM STORE";
            Vector2 titleSize = font.MeasureString(title) * 1.2f;
            Vector2 titlePos = panelPosition + new Vector2((PanelWidth - titleSize.X) / 2f, 30);

            //标题发光
            Color glowColor = new Color(80, 220, 255) * (uiAlpha * 0.8f);
            for (int i = 0; i < 4; i++) {
                float angle = MathHelper.TwoPi * i / 4f;
                Vector2 offset = angle.ToRotationVector2() * 2.5f;
                Utils.DrawBorderString(spriteBatch, title, titlePos + offset, glowColor * 0.6f, 1.2f);
            }

            //主标题
            Utils.DrawBorderString(spriteBatch, title, titlePos, Color.White * uiAlpha, 1.2f);
        }

        private void DrawItemList(SpriteBatch spriteBatch) {
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            int itemListY = (int)(panelPosition.Y + 100);
            int itemListX = (int)(panelPosition.X + 20);

            for (int i = 0; i < Math.Min(MaxVisibleItems, shopItems.Count - scrollOffset); i++) {
                int index = i + scrollOffset;
                ShopItem shopItem = shopItems[index];

                Vector2 itemPos = new Vector2(itemListX, itemListY + i * ItemSlotHeight);
                bool isSelected = index == selectedIndex;

                DrawShopItemSlot(spriteBatch, shopItem, itemPos, isSelected, font);
            }
        }

        private void DrawShopItemSlot(SpriteBatch spriteBatch, ShopItem shopItem, Vector2 position, bool isSelected, DynamicSpriteFont font) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle slotRect = new Rectangle(
                (int)position.X,
                (int)position.Y,
                PanelWidth - 40,
                ItemSlotHeight - 10
            );

            //槽位背景
            Color slotColor = isSelected
                ? new Color(80, 200, 255) * (uiAlpha * 0.3f)
                : new Color(30, 60, 90) * (uiAlpha * 0.2f);
            spriteBatch.Draw(pixel, slotRect, slotColor);

            //物品图标
            Texture2D itemTexture = TextureAssets.Item[shopItem.itemType].Value;
            Rectangle itemFrame = Main.itemAnimations[shopItem.itemType]?.GetFrame(itemTexture) ?? itemTexture.Bounds;
            float itemScale = Math.Min(48f / itemFrame.Width, 48f / itemFrame.Height);
            Vector2 itemDrawPos = position + new Vector2(35, ItemSlotHeight / 2f - 5);
            spriteBatch.Draw(itemTexture, itemDrawPos, itemFrame, Color.White * uiAlpha, 0f, itemFrame.Size() / 2f, itemScale, SpriteEffects.None, 0f);

            //物品名称
            string itemName = Lang.GetItemNameValue(shopItem.itemType);
            Vector2 namePos = position + new Vector2(80, 15);
            Utils.DrawBorderString(spriteBatch, itemName, namePos, Color.White * uiAlpha, 0.85f);

            //价格
            string priceText = $"{shopItem.price / 10000}g {(shopItem.price % 10000) / 100}s {shopItem.price % 100}c";
            Vector2 pricePos = position + new Vector2(80, 45);
            Color priceColor = player.CanAfford(shopItem.price)
                ? new Color(255, 215, 0)
                : new Color(255, 100, 100);
            Utils.DrawBorderString(spriteBatch, priceText, pricePos, priceColor * uiAlpha, 0.75f);
        }

        private void DrawTechParticles(SpriteBatch spriteBatch) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            foreach (var particle in techParticles) {
                particle.Draw(spriteBatch, pixel, uiAlpha);
            }
        }

        //粒子类
        private class TechParticle
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Life;
            public float MaxLife;
            public float Size;
            public float Rotation;

            public TechParticle(Vector2 position) {
                Position = position;
                Velocity = new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(-0.8f, -0.2f));
                Life = 0f;
                MaxLife = Main.rand.NextFloat(60f, 120f);
                Size = Main.rand.NextFloat(1.5f, 3f);
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            }

            public bool Update() {
                Life++;
                Position += Velocity;
                Velocity.Y -= 0.01f;
                Rotation += 0.02f;
                return Life >= MaxLife;
            }

            public void Draw(SpriteBatch sb, Texture2D tex, float alpha) {
                float t = Life / MaxLife;
                float fade = (float)Math.Sin(t * MathHelper.Pi) * alpha;
                Color color = new Color(80, 200, 255) * (0.6f * fade);

                sb.Draw(tex, Position, new Rectangle(0, 0, 1, 1), color, Rotation, new Vector2(0.5f),
                    new Vector2(Size * 2f, Size * 0.3f), SpriteEffects.None, 0f);
            }
        }

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
            shopItems.Clear();
            shopItems.Add(new ShopItem(ItemID.HealingPotion, 1, Item.buyPrice(silver: 50)));
            shopItems.Add(new ShopItem(ItemID.ManaPotion, 1, Item.buyPrice(silver: 25)));
            shopItems.Add(new ShopItem(ItemID.RecallPotion, 1, Item.buyPrice(silver: 10)));
            shopItems.Add(new ShopItem(ItemID.WormholePotion, 1, Item.buyPrice(silver: 5)));
            shopItems.Add(new ShopItem(ItemID.BattlePotion, 1, Item.buyPrice(silver: 15)));
            shopItems.Add(new ShopItem(ItemID.BuilderPotion, 1, Item.buyPrice(silver: 10)));
            shopItems.Add(new ShopItem(ItemID.CalmingPotion, 1, Item.buyPrice(silver: 8)));
        }
    }
}
