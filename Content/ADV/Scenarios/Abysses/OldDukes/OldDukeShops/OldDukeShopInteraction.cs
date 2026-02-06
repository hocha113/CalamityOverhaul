using CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Quest.Findfragments;
using CalamityOverhaul.OtherMods.ImproveGame.Ammos;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.OldDukeShops
{
    /// <summary>
    /// 老公爵商店交互逻辑处理器
    /// </summary>
    internal class OldDukeShopInteraction
    {
        private readonly Player player;
        private readonly List<OldDukeShopItem> shopItems;

        //选中和悬停状态
        public int SelectedIndex { get; set; } = -1;
        public int HoveredIndex { get; private set; } = -1;
        public int ScrollOffset { get; set; } = 0;

        //长按购买逻辑
        private int holdingPurchaseIndex = -1;
        private int holdingPurchaseTimer = 0;
        private int purchaseCooldown = 20;
        private const int InitialPurchaseCooldown = 30;
        private const int MinPurchaseCooldown = 2;
        private const int HoldThreshold = 20;
        public int ConsecutivePurchaseCount { get; private set; } = 0;

        //UI尺寸常量 
        public const int MaxVisibleItems = 7;//显示项数量
        public const int ItemSlotHeight = 78;//稍微小一点的高度

        //滚动条
        private readonly OldDukeScrollBar scrollBar = new();

        //关闭按钮
        public bool IsCloseButtonHovered { get; private set; } = false;
        public const int CloseButtonSize = 32;

        public int HoldingPurchaseIndex => holdingPurchaseIndex;
        public int HoldingPurchaseTimer => holdingPurchaseTimer;
        public bool IsScrollBarDragging => scrollBar.IsDragging;

        public OldDukeShopInteraction(Player player, List<OldDukeShopItem> shopItems) {
            this.player = player;
            this.shopItems = shopItems;
        }

        /// <summary>
        /// 处理滚轮滚动
        /// </summary>
        public void HandleScroll() {
            //如果滚动条正在拖动，不响应滚轮
            if (scrollBar.IsDragging) return;

            int scrollDelta = PlayerInput.ScrollWheelDeltaForUI;
            if (scrollDelta != 0) {
                int maxScroll = Math.Max(0, shopItems.Count - MaxVisibleItems);
                int oldOffset = ScrollOffset;
                ScrollOffset = Math.Clamp(ScrollOffset - Math.Sign(scrollDelta), 0, maxScroll);
                if (oldOffset != ScrollOffset) {
                    SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.25f, Pitch = -0.3f });
                }
            }
        }

        /// <summary>
        /// 更新滚动条
        /// </summary>
        public void UpdateScrollBar(Vector2 panelPosition, Point mousePosition,
            bool mouseLeftDown, bool mouseLeftRelease) {
            if (shopItems.Count <= MaxVisibleItems) return;

            int barHeight = MaxVisibleItems * ItemSlotHeight - 20;
            int maxScroll = Math.Max(0, shopItems.Count - MaxVisibleItems);

            scrollBar.Update(panelPosition, barHeight, ScrollOffset, maxScroll,
                shopItems.Count, MaxVisibleItems, mousePosition, mouseLeftDown,
                mouseLeftRelease, out int newScrollOffset);

            ScrollOffset = newScrollOffset;
        }

        /// <summary>
        /// 绘制滚动条
        /// </summary>
        public void DrawScrollBar(Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch,
            Vector2 panelPosition, float uiAlpha, float sulfurPulseTimer) {
            if (shopItems.Count <= MaxVisibleItems) return;

            int barHeight = MaxVisibleItems * ItemSlotHeight - 20;
            int maxScroll = Math.Max(0, shopItems.Count - MaxVisibleItems);

            scrollBar.Draw(spriteBatch, panelPosition, barHeight, ScrollOffset, maxScroll,
                shopItems.Count, MaxVisibleItems, uiAlpha, sulfurPulseTimer);
        }

        /// <summary>
        /// 更新关闭按钮交互
        /// </summary>
        public bool UpdateCloseButton(Point mousePoint, Vector2 panelPosition, bool mouseLeftRelease) {
            Rectangle closeButtonRect = new Rectangle(
                (int)(panelPosition.X + 580 - CloseButtonSize - 15),
                (int)(panelPosition.Y + 15),
                CloseButtonSize,
                CloseButtonSize
            );

            bool wasHovered = IsCloseButtonHovered;
            IsCloseButtonHovered = closeButtonRect.Contains(mousePoint);

            //悬停音效
            if (IsCloseButtonHovered && !wasHovered) {
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.2f, Pitch = 0.3f });
            }

            //点击关闭
            if (IsCloseButtonHovered && mouseLeftRelease) {
                SoundEngine.PlaySound(SoundID.MenuClose with { Pitch = -0.3f });
                return true;
            }

            return false;
        }

        /// <summary>
        /// 更新物品选择和购买逻辑
        /// </summary>
        public void UpdateItemSelection(Point mousePoint, Vector2 itemListPos, int panelWidth) {
            int itemListY = (int)itemListPos.Y;
            int itemListX = (int)itemListPos.X;
            int oldHoveredIndex = HoveredIndex;
            HoveredIndex = -1;

            for (int i = 0; i < Math.Min(MaxVisibleItems, shopItems.Count - ScrollOffset); i++) {
                int index = i + ScrollOffset;
                Rectangle itemRect = new Rectangle(
                    itemListX,
                    itemListY + i * ItemSlotHeight,
                    panelWidth - 65,
                    ItemSlotHeight - 6
                );

                if (itemRect.Contains(mousePoint)) {
                    HoveredIndex = index;

                    //悬停音效
                    if (oldHoveredIndex != HoveredIndex && HoveredIndex != -1) {
                        SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.2f, Pitch = 0.2f });
                    }

                    HandlePurchaseInput(index);
                    break;
                }
            }

            //鼠标离开所有物品，重置长按状态
            if (HoveredIndex == -1) {
                ResetHoldingState();
            }
        }

        private void HandlePurchaseInput(int index) {
            if (Main.mouseLeft) {
                if (Main.mouseLeftRelease) {
                    //首次点击
                    SelectedIndex = index;
                    holdingPurchaseIndex = index;
                    holdingPurchaseTimer = 0;
                    ConsecutivePurchaseCount = 0;
                    purchaseCooldown = InitialPurchaseCooldown;
                    TryPurchaseItem(index);
                }
                else {
                    //持续按住
                    if (holdingPurchaseIndex == index) {
                        holdingPurchaseTimer++;

                        //达到阈值，开始连续购买
                        if (holdingPurchaseTimer >= HoldThreshold) {
                            if (holdingPurchaseTimer % purchaseCooldown == 0) {
                                TryPurchaseItem(index);
                                ConsecutivePurchaseCount++;

                                //逐渐加速，每购买5次，冷却减少20%
                                if (ConsecutivePurchaseCount % 5 == 0) {
                                    purchaseCooldown = Math.Max(
                                        MinPurchaseCooldown,
                                        (int)(purchaseCooldown * 0.8f)
                                    );
                                }
                            }
                        }
                    }
                    else {
                        //切换到不同物品，重置
                        holdingPurchaseIndex = index;
                        holdingPurchaseTimer = 0;
                        ConsecutivePurchaseCount = 0;
                        purchaseCooldown = InitialPurchaseCooldown;
                    }
                }
            }
            else {
                //松开鼠标，重置长按状态
                if (holdingPurchaseIndex == index) {
                    ResetHoldingState();
                }
            }
        }

        private void TryPurchaseItem(int index) {
            if (index < 0 || index >= shopItems.Count) return;

            OldDukeShopItem shopItem = shopItems[index];

            //检查是否有足够的海洋残片
            int oceanFragmentCount = FindFragmentUI.GetFragmentCount();

            if (oceanFragmentCount >= shopItem.price) {
                //消耗海洋残片，从所有储物位置循环删除直到达到所需数量
                int remainingToConsume = shopItem.price;

                var bigBags = player.GetBigBagItems() ?? [];
                //依次从各个储物位置消耗
                Item[][] inventories = [
                    player.inventory,
                    player.bank.item,
                    player.bank2.item,
                    player.bank3.item,
                    player.bank4.item,
                    [.. bigBags],
                ];

                foreach (Item[] inventory in inventories) {
                    if (remainingToConsume <= 0) break;
                    if (inventory == null) continue;

                    for (int i = 0; i < inventory.Length && remainingToConsume > 0; i++) {
                        Item invItem = inventory[i];
                        if (invItem.type == ModContent.ItemType<Oceanfragments>()) {
                            int consumeAmount = Math.Min(invItem.stack, remainingToConsume);
                            invItem.stack -= consumeAmount;
                            remainingToConsume -= consumeAmount;

                            if (invItem.stack <= 0) {
                                invItem.TurnToAir();
                            }
                        }
                    }
                }

                //给予物品
                player.QuickSpawnItem(player.GetSource_OpenItem(shopItem.itemType), shopItem.itemType, shopItem.stack);

                //购买音效
                SoundEngine.PlaySound(SoundID.Grab with { Volume = 0.7f, Pitch = -0.1f });
                SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.5f, Pitch = 0.2f });
            }
            else {
                //玩家不足音效
                SoundEngine.PlaySound(SoundID.MenuClose with { Pitch = -0.5f, Volume = 0.8f });
            }
        }

        /// <summary>
        /// 重置长按购买状态
        /// </summary>
        public void ResetHoldingState() {
            holdingPurchaseIndex = -1;
            holdingPurchaseTimer = 0;
            ConsecutivePurchaseCount = 0;
            purchaseCooldown = InitialPurchaseCooldown;
        }

        /// <summary>
        /// 重置所有交互状态
        /// </summary>
        public void Reset() {
            HoveredIndex = -1;
            SelectedIndex = -1;
            ScrollOffset = 0;
            IsCloseButtonHovered = false;
            ResetHoldingState();
            scrollBar.Reset();
        }
    }
}
