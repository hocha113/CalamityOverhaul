using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Items.OceanRaiderses.OceanRaidersUIs
{
    /// <summary>
    /// 海洋吞噬者UI渲染器
    /// </summary>
    internal class OceanRaidersRenderer
    {
        private readonly Player player;
        private OceanRaidersTP machine;
        private readonly OceanRaidersAnimation animation;
        private readonly OceanRaidersInteraction interaction;

        //UI尺寸常量 - 调整以适应20x18格子
        private const int PanelWidth = 760;
        private const int PanelHeight = 760;
        private const int SlotSize = 32;
        private const int SlotPadding = 4;
        private const int SlotsPerRow = 20;
        private const int SlotRows = 17;
        private const int HeaderHeight = 80; //标题区域高度
        private const int StorageStartX = 20; //存储区左边距
        private const int StorageStartY = HeaderHeight + 10; //存储区顶部边距

        public OceanRaidersRenderer(Player player, OceanRaidersTP machine,
            OceanRaidersAnimation animation, OceanRaidersInteraction interaction) {
            this.player = player;
            this.machine = machine;
            this.animation = animation;
            this.interaction = interaction;
        }

        public void UpdateMachine(OceanRaidersTP newMachine) {
            machine = newMachine;
        }

        /// <summary>
        /// 计算面板位置
        /// </summary>
        public Vector2 CalculatePanelPosition() {
            float slideOffset = (1f - animation.PanelSlideProgress) * 100f;
            return new Vector2(
                Main.screenWidth / 2 - PanelWidth / 2,
                Main.screenHeight / 2 - PanelHeight / 2 + slideOffset
            );
        }

        /// <summary>
        /// 主绘制函数
        /// </summary>
        public void Draw(SpriteBatch spriteBatch, Vector2 panelPosition, OceanRaidersEffects effects) {
            if (animation.UIAlpha <= 0f) return;

            //绘制粒子效果（背景层）
            effects.DrawEffects(spriteBatch, animation.UIAlpha * 0.6f);

            //绘制主面板
            DrawMainPanel(spriteBatch, panelPosition);

            //绘制标题
            DrawHeader(spriteBatch, panelPosition);

            //绘制关闭按钮
            DrawCloseButton(spriteBatch, panelPosition);

            //绘制分隔线
            DrawHeaderDivider(spriteBatch, panelPosition);

            //绘制存储槽位
            DrawStorageSlots(spriteBatch, panelPosition);

            //绘制底部信息
            DrawFooter(spriteBatch, panelPosition);

            //绘制前景粒子效果
            effects.DrawEffects(spriteBatch, animation.UIAlpha * 0.4f);
        }

        private void DrawMainPanel(SpriteBatch spriteBatch, Vector2 panelPosition) {
            Rectangle panelRect = new Rectangle(
                (int)panelPosition.X,
                (int)panelPosition.Y,
                PanelWidth,
                PanelHeight
            );

            Texture2D pixel = VaultAsset.placeholder2.Value;

            //绘制阴影
            Rectangle shadow = panelRect;
            shadow.Offset(6, 8);
            spriteBatch.Draw(pixel, shadow, new Rectangle(0, 0, 1, 1), Color.Black * (animation.UIAlpha * 0.60f));

            //绘制渐变背景
            DrawGradientBackground(spriteBatch, panelRect, pixel);

            //绘制毒性波浪覆盖
            DrawToxicWaveOverlay(spriteBatch, panelRect, pixel);

            //绘制内部发光
            float pulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2.2f) * 0.5f + 0.5f;
            Rectangle inner = panelRect;
            inner.Inflate(-6, -6);
            spriteBatch.Draw(pixel, inner, new Rectangle(0, 0, 1, 1),
                new Color(80, 100, 35) * (animation.UIAlpha * 0.09f * (0.5f + pulse * 0.5f)));

            //绘制硫磺海边框
            DrawSulfseaFrame(spriteBatch, panelRect, pulse, pixel);
        }

        private void DrawGradientBackground(SpriteBatch spriteBatch, Rectangle panelRect, Texture2D pixel) {
            int segments = 30;
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                float t2 = (i + 1) / (float)segments;
                int y1 = panelRect.Y + (int)(t * panelRect.Height);
                int y2 = panelRect.Y + (int)(t2 * panelRect.Height);
                Rectangle r = new(panelRect.X, y1, panelRect.Width, Math.Max(1, y2 - y1));

                //硫磺海配色
                Color sulfurDeep = new Color(12, 18, 8);
                Color toxicMid = new Color(28, 38, 15);
                Color acidEdge = new Color(65, 85, 30);
                float breathing = (float)Math.Sin(animation.SulfurPulse) * 0.5f + 0.5f;
                Color blendBase = Color.Lerp(sulfurDeep, toxicMid,
                    (float)Math.Sin(animation.SulfurPulse * 0.5f + t * 1.4f) * 0.5f + 0.5f);
                Color c = Color.Lerp(blendBase, acidEdge, t * 0.7f * (0.3f + breathing * 0.7f));
                c *= animation.UIAlpha * 0.92f;
                spriteBatch.Draw(pixel, r, new Rectangle(0, 0, 1, 1), c);
            }

            //叠加瘴气层
            float miasmaEffect = (float)Math.Sin(animation.MiasmaTimer * 1.1f) * 0.5f + 0.5f;
            Color miasmaTint = new Color(45, 55, 20) * (animation.UIAlpha * 0.4f * miasmaEffect);
            spriteBatch.Draw(pixel, panelRect, new Rectangle(0, 0, 1, 1), miasmaTint);
        }

        private void DrawToxicWaveOverlay(SpriteBatch spriteBatch, Rectangle rect, Texture2D pixel) {
            int bands = 6;
            for (int i = 0; i < bands; i++) {
                float t = i / (float)bands;
                float y = rect.Y + 18 + t * (rect.Height - 36);
                float amp = 7f + (float)Math.Sin((animation.ToxicWavePhase + t) * 2.2f) * 4.5f;
                float thickness = 2.2f;
                int segments = 42;
                Vector2 prev = Vector2.Zero;
                for (int s = 0; s <= segments; s++) {
                    float p = s / (float)segments;
                    float localY = y + (float)Math.Sin(animation.ToxicWavePhase * 2.2f + p * MathHelper.TwoPi * 1.3f + t) * amp;
                    Vector2 point = new(rect.X + 8 + p * (rect.Width - 16), localY);
                    if (s > 0) {
                        Vector2 diff = point - prev;
                        float len = diff.Length();
                        if (len > 0.01f) {
                            float rot = diff.ToRotation();
                            Color c = new Color(60, 90, 30) * (animation.UIAlpha * 0.08f);
                            spriteBatch.Draw(pixel, prev, new Rectangle(0, 0, 1, 1), c, rot,
                                Vector2.Zero, new Vector2(len, thickness), SpriteEffects.None, 0f);
                        }
                    }
                    prev = point;
                }
            }
        }

        private static void DrawSulfseaFrame(SpriteBatch spriteBatch, Rectangle rect, float pulse, Texture2D pixel) {
            float alpha = 0.85f;
            Color edge = Color.Lerp(new Color(70, 100, 35), new Color(130, 160, 65), pulse) * alpha;

            //外框
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 2), new Rectangle(0, 0, 1, 1), edge);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), new Rectangle(0, 0, 1, 1), edge * 0.75f);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 2, rect.Height), new Rectangle(0, 0, 1, 1), edge * 0.88f);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), new Rectangle(0, 0, 1, 1), edge * 0.88f);

            //内框
            Rectangle inner = rect;
            inner.Inflate(-5, -5);
            Color innerC = new Color(140, 170, 70) * (alpha * 0.22f * pulse);
            spriteBatch.Draw(pixel, new Rectangle(inner.X, inner.Y, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerC);
            spriteBatch.Draw(pixel, new Rectangle(inner.X, inner.Bottom - 1, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerC * 0.7f);
            spriteBatch.Draw(pixel, new Rectangle(inner.X, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerC * 0.88f);
            spriteBatch.Draw(pixel, new Rectangle(inner.Right - 1, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerC * 0.88f);

            //四角星标
            DrawCornerStar(spriteBatch, new Vector2(rect.X + 10, rect.Y + 10), alpha * 0.9f);
            DrawCornerStar(spriteBatch, new Vector2(rect.Right - 10, rect.Y + 10), alpha * 0.9f);
            DrawCornerStar(spriteBatch, new Vector2(rect.X + 10, rect.Bottom - 10), alpha * 0.65f);
            DrawCornerStar(spriteBatch, new Vector2(rect.Right - 10, rect.Bottom - 10), alpha * 0.65f);
        }

        private static void DrawCornerStar(SpriteBatch spriteBatch, Vector2 pos, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            float size = 5f;
            Color c = new Color(160, 190, 80) * alpha;
            spriteBatch.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), c, 0f,
                new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.26f), SpriteEffects.None, 0f);
            spriteBatch.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), c * 0.8f, MathHelper.PiOver2,
                new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.26f), SpriteEffects.None, 0f);
        }

        private void DrawHeader(SpriteBatch spriteBatch, Vector2 panelPosition) {
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            string title = OceanRaidersUI.TitleText.Value;
            Vector2 titleSize = font.MeasureString(title);
            Vector2 titlePos = panelPosition + new Vector2(PanelWidth / 2 - titleSize.X / 2 * 1.1f, 15);

            //发光效果
            Color titleGlow = new Color(160, 190, 80) * (animation.UIAlpha * 0.75f);
            for (int i = 0; i < 4; i++) {
                float angle = MathHelper.TwoPi * i / 4f;
                Vector2 offset = angle.ToRotationVector2() * 2f;
                Utils.DrawBorderString(spriteBatch, title, titlePos + offset, titleGlow * 0.6f, 1.1f);
            }

            Utils.DrawBorderString(spriteBatch, title, titlePos, Color.White * animation.UIAlpha, 1.1f);
        }

        private void DrawCloseButton(SpriteBatch spriteBatch, Vector2 panelPosition) {
            int buttonSize = OceanRaidersInteraction.CloseButtonSize;
            Rectangle buttonRect = new Rectangle(
                (int)(panelPosition.X + PanelWidth - buttonSize - 10),
                (int)(panelPosition.Y + 10),
                buttonSize,
                buttonSize
            );

            Texture2D pixel = VaultAsset.placeholder2.Value;
            Color buttonColor = interaction.IsCloseButtonHovered
                ? new Color(180, 80, 60) * animation.UIAlpha
                : new Color(120, 140, 60) * (animation.UIAlpha * 0.7f);

            spriteBatch.Draw(pixel, buttonRect, new Rectangle(0, 0, 1, 1), buttonColor * 0.5f);

            //绘制X
            float crossSize = buttonSize * 0.5f;
            Vector2 center = buttonRect.Center.ToVector2();
            DrawLine(spriteBatch, center - new Vector2(crossSize / 2, crossSize / 2),
                center + new Vector2(crossSize / 2, crossSize / 2), buttonColor, 2f);
            DrawLine(spriteBatch, center - new Vector2(crossSize / 2, -crossSize / 2),
                center + new Vector2(crossSize / 2, -crossSize / 2), buttonColor, 2f);
        }

        private static void DrawLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color color, float thickness) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 edge = end - start;
            float length = edge.Length();
            if (length < 1f) return;
            float rotation = edge.ToRotation();
            spriteBatch.Draw(pixel, start, new Rectangle(0, 0, 1, 1), color, rotation,
                Vector2.Zero, new Vector2(length, thickness), SpriteEffects.None, 0);
        }

        private void DrawHeaderDivider(SpriteBatch spriteBatch, Vector2 panelPosition) {
            Vector2 divStart = panelPosition + new Vector2(20, 55);
            Vector2 divEnd = divStart + new Vector2(PanelWidth - 40, 0);
            DrawGradientLine(spriteBatch, divStart, divEnd,
                new Color(100, 140, 50) * (animation.UIAlpha * 0.9f),
                new Color(100, 140, 50) * (animation.UIAlpha * 0.08f), 1.3f);
        }

        private static void DrawGradientLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end,
            Color startColor, Color endColor, float thickness) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 edge = end - start;
            float length = edge.Length();
            if (length < 1f) return;
            edge.Normalize();
            float rotation = (float)Math.Atan2(edge.Y, edge.X);
            int segments = Math.Max(1, (int)(length / 11f));
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                Vector2 segPos = start + edge * (length * t);
                float segLength = length / segments;
                Color color = Color.Lerp(startColor, endColor, t);
                spriteBatch.Draw(pixel, segPos, new Rectangle(0, 0, 1, 1), color, rotation,
                    new Vector2(0, 0.5f), new Vector2(segLength, thickness), SpriteEffects.None, 0);
            }
        }

        private void DrawStorageSlots(SpriteBatch spriteBatch, Vector2 panelPosition) {
            Vector2 storageStartPos = panelPosition + new Vector2(StorageStartX, StorageStartY);
            DynamicSpriteFont font = FontAssets.MouseText.Value;

            //绘制所有槽位
            for (int row = 0; row < SlotRows; row++) {
                for (int col = 0; col < SlotsPerRow; col++) {
                    int index = row * SlotsPerRow + col;
                    Vector2 slotPos = storageStartPos + new Vector2(
                        col * (SlotSize + SlotPadding),
                        row * (SlotSize + SlotPadding)
                    );

                    bool isHovered = interaction.HoveredSlot == index;
                    float hoverProgress = animation.SlotHoverProgress[index];

                    DrawStorageSlot(spriteBatch, slotPos, index, isHovered, hoverProgress, font);
                }
            }
        }

        private void DrawStorageSlot(SpriteBatch spriteBatch, Vector2 position, int index,
            bool isHovered, float hoverProgress, DynamicSpriteFont font) {
            Rectangle slotRect = new Rectangle((int)position.X, (int)position.Y, SlotSize, SlotSize);
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //槽位背景
            Color bgColor = new Color(20, 28, 15) * (animation.UIAlpha * 0.8f);
            if (isHovered) {
                bgColor = Color.Lerp(bgColor, new Color(60, 80, 40), hoverProgress * 0.6f);
            }
            spriteBatch.Draw(pixel, slotRect, new Rectangle(0, 0, 1, 1), bgColor);

            //槽位边框
            Color borderColor = new Color(80, 100, 45) * (animation.UIAlpha * 0.7f);
            if (isHovered) {
                borderColor = Color.Lerp(borderColor, new Color(140, 170, 70), hoverProgress);
            }
            DrawSlotBorder(spriteBatch, slotRect, borderColor);

            //获取并绘制物品
            if (index < machine.storedItems.Count) {
                Item item = machine.storedItems[index];
                if (item != null && item.type > ItemID.None && item.stack > 0) {
                    //预加载物品纹理
                    Main.instance.LoadItem(item.type);
                    
                    //绘制物品图标
                    Texture2D itemTexture = TextureAssets.Item[item.type].Value;
                    float scale = Math.Min(SlotSize * 0.9f / itemTexture.Width, SlotSize * 0.9f / itemTexture.Height);
                    Vector2 itemPos = position + new Vector2(SlotSize / 2);

                    //悬停时放大
                    if (isHovered) {
                        scale *= 1f + hoverProgress * 0.15f;
                    }

                    spriteBatch.Draw(itemTexture, itemPos, null, Color.White * animation.UIAlpha,
                        0f, itemTexture.Size() / 2, scale, SpriteEffects.None, 0);

                    //绘制数量
                    if (item.stack > 1) {
                        string stackText = item.stack.ToString();
                        Vector2 stackSize = font.MeasureString(stackText) * 0.7f;
                        Vector2 stackPos = position + new Vector2(SlotSize - stackSize.X - 2, SlotSize - stackSize.Y);
                        Utils.DrawBorderString(spriteBatch, stackText, stackPos,
                            Color.White * animation.UIAlpha, 0.7f);
                    }

                    //悬停显示物品名称
                    if (isHovered && hoverProgress > 0.5f) {
                        DrawItemTooltip(spriteBatch, item, position + new Vector2(SlotSize / 2, -20));
                    }
                }
            }
        }

        private static void DrawSlotBorder(SpriteBatch spriteBatch, Rectangle rect, Color color) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            int thickness = 1;
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), new Rectangle(0, 0, 1, 1), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), new Rectangle(0, 0, 1, 1), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), new Rectangle(0, 0, 1, 1), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), new Rectangle(0, 0, 1, 1), color);
        }

        private void DrawItemTooltip(SpriteBatch spriteBatch, Item item, Vector2 position) {
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            string itemName = item.Name;
            Vector2 textSize = font.MeasureString(itemName) * 0.7f;
            Vector2 tooltipPos = position - new Vector2(textSize.X / 2, 0);

            //背景
            Rectangle bgRect = new Rectangle(
                (int)(tooltipPos.X - 4),
                (int)(tooltipPos.Y - 2),
                (int)(textSize.X + 8),
                (int)(textSize.Y + 4)
            );
            Texture2D pixel = VaultAsset.placeholder2.Value;
            spriteBatch.Draw(pixel, bgRect, new Rectangle(0, 0, 1, 1),
                new Color(15, 20, 10) * (animation.UIAlpha * 0.9f));

            //文本
            Utils.DrawBorderString(spriteBatch, itemName, tooltipPos,
                Color.White * animation.UIAlpha, 0.7f);
        }

        private void DrawFooter(SpriteBatch spriteBatch, Vector2 panelPosition) {
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            int usedSlots = machine.storedItems.Count;
            int totalSlots = SlotsPerRow * SlotRows;
            string infoText = $"{OceanRaidersUI.StorageText.Value}: {usedSlots}/{totalSlots}";
            Vector2 infoSize = font.MeasureString(infoText) * 0.8f;
            Vector2 infoPos = panelPosition + new Vector2(PanelWidth / 2 - infoSize.X / 2, PanelHeight - 30);

            Utils.DrawBorderString(spriteBatch, infoText, infoPos,
                new Color(140, 170, 75) * animation.UIAlpha, 0.8f);
        }
    }
}
