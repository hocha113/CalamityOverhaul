using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.Cyberwares.UIs
{
    /// <summary>
    /// 义体选择背包面板，在主面板侧面显示可装入当前槽位的义体物品列表
    /// </summary>
    internal class CyberInventoryPanel
    {
        #region 常量

        private const float PanelWidth = 240f;
        private const float PanelPadding = 10f;
        private const float ItemRowHeight = 50f;
        //放大字号后给头部和容量条预留更多纵向空间
        private const float HeaderHeight = 44f;
        private const float CapacityBarHeight = 26f;
        private const float ScrollBarWidth = 5f;

        #endregion

        #region 状态

        /// <summary>
        /// 当前绑定的槽位索引，-1表示未选中
        /// </summary>
        private int boundSlot = -1;

        /// <summary>
        /// 面板展开进度 0~1
        /// </summary>
        private float openProgress;

        /// <summary>
        /// 面板目标位置矩形
        /// </summary>
        private Rectangle panelRect;

        /// <summary>
        /// 当前可选物品列表（玩家背包中的索引）
        /// </summary>
        private readonly List<int> compatibleItems = [];

        /// <summary>
        /// 鼠标悬停的物品行，-1无
        /// </summary>
        private int hoveredItemRow = -1;

        /// <summary>
        /// 滚动偏移量
        /// </summary>
        private float scrollOffset;

        /// <summary>
        /// 是否显示已装备义体的卸载按钮区
        /// </summary>
        private bool hasEquippedItem;

        /// <summary>
        /// 上一帧滚轮值，用于计算滚动增量
        /// </summary>
        private int oldScrollWheelValue;

        /// <summary>
        /// 本帧是否发生了装备/卸载操作
        /// </summary>
        public bool ActionThisFrame { get; private set; }

        /// <summary>
        /// 当前悬停的义体物品（供自定义Tooltip绘制用）
        /// </summary>
        private Item hoveredCyberItem;

        /// <summary>
        /// 面板是否处于可见状态
        /// </summary>
        public bool IsVisible => boundSlot >= 0 || openProgress > 0.01f;

        #endregion

        #region 公共方法

        /// <summary>
        /// 绑定到指定槽位并刷新可选列表
        /// </summary>
        public void BindSlot(int slotIndex, CyberwarePlayer cyberPlayer) {
            if (slotIndex == boundSlot) return;
            boundSlot = slotIndex;
            scrollOffset = 0;
            oldScrollWheelValue = Mouse.GetState().ScrollWheelValue;
            RefreshItems(cyberPlayer);
        }

        /// <summary>
        /// 关闭面板
        /// </summary>
        public void Unbind() {
            boundSlot = -1;
            hasEquippedItem = false;
        }

        /// <summary>
        /// 刷新当前槽位的可选物品列表
        /// </summary>
        public void RefreshItems(CyberwarePlayer cyberPlayer) {
            compatibleItems.Clear();
            hasEquippedItem = false;
            if (boundSlot < 0 || cyberPlayer == null) return;

            compatibleItems.AddRange(cyberPlayer.GetCompatibleItems(boundSlot));

            Item equipped = cyberPlayer.EquippedCyberwares[boundSlot];
            hasEquippedItem = equipped != null && !equipped.IsAir;
        }

        /// <summary>
        /// 更新面板交互逻辑
        /// </summary>
        public void Update(Rectangle mainPanelRect, int selectedSlot, CyberwarePlayer cyberPlayer) {
            ActionThisFrame = false;

            // 同步绑定状态
            if (selectedSlot != boundSlot) {
                if (selectedSlot >= 0) {
                    BindSlot(selectedSlot, cyberPlayer);
                }
                else {
                    Unbind();
                }
            }

            // 动画过渡
            float target = boundSlot >= 0 ? 1f : 0f;
            openProgress += (target - openProgress) * 0.18f;
            if (boundSlot < 0 && openProgress < 0.01f) {
                openProgress = 0;
                return;
            }

            // 计算面板位置：左侧槽位在面板左边展开，右侧槽位在面板右边展开
            bool isLeftSlot = boundSlot >= 0 && boundSlot < 6;
            float easedProgress = CWRUtils.EaseOutCubic(Math.Clamp(openProgress, 0, 1));
            float actualWidth = PanelWidth * easedProgress;

            if (isLeftSlot) {
                // 面板在主面板左侧
                panelRect = new Rectangle(
                    (int)(mainPanelRect.X - actualWidth - 6),
                    mainPanelRect.Y,
                    (int)actualWidth,
                    mainPanelRect.Height
                );
            }
            else {
                // 面板在主面板右侧
                panelRect = new Rectangle(
                    mainPanelRect.Right + 6,
                    mainPanelRect.Y,
                    (int)actualWidth,
                    mainPanelRect.Height
                );
            }

            // 交互检测
            if (openProgress > 0.5f) {
                UpdateInteraction(cyberPlayer);
            }
        }

        /// <summary>
        /// 绘制面板
        /// </summary>
        public void Draw(SpriteBatch sb, float parentAlpha, CyberwarePlayer cyberPlayer) {
            if (openProgress < 0.01f || panelRect.Width < 2) return;

            Texture2D px = CWRAsset.Placeholder_White?.Value;
            if (px == null) return;

            float alpha = parentAlpha * Math.Clamp(openProgress, 0, 1);

            //背板：复用 CyberwarePanel.fx 的 uMode=1 轻量模式，与主面板视觉同源但去掉中央光场与扫描带
            CyberPanelRenderer.DrawShaderBackground(sb, alpha * 0.95f, panelRect, Vector2.Zero, 0f, mode: 1);

            // 边框
            Color borderColor = CyberwareTheme.Accent * (alpha * 0.6f);
            sb.Draw(px, new Rectangle(panelRect.X, panelRect.Y, panelRect.Width, 1), new Rectangle(0, 0, 1, 1), borderColor);
            sb.Draw(px, new Rectangle(panelRect.X, panelRect.Bottom - 1, panelRect.Width, 1), new Rectangle(0, 0, 1, 1), borderColor * 0.5f);
            sb.Draw(px, new Rectangle(panelRect.X, panelRect.Y, 1, panelRect.Height), new Rectangle(0, 0, 1, 1), borderColor * 0.7f);
            sb.Draw(px, new Rectangle(panelRect.Right - 1, panelRect.Y, 1, panelRect.Height), new Rectangle(0, 0, 1, 1), borderColor * 0.7f);

            if (openProgress < 0.6f) return;
            float contentAlpha = alpha * Math.Clamp((openProgress - 0.6f) / 0.4f, 0, 1);

            // 标题头部
            DrawHeader(sb, px, contentAlpha, cyberPlayer);

            // 容量条
            DrawCapacityBar(sb, px, contentAlpha, cyberPlayer);

            // 已装备义体信息
            float yOffset = HeaderHeight + CapacityBarHeight + PanelPadding;
            if (hasEquippedItem) {
                yOffset = DrawEquippedSection(sb, px, contentAlpha, cyberPlayer, yOffset);
            }

            // 分隔线
            float separatorY = panelRect.Y + yOffset;
            sb.Draw(px, new Rectangle(panelRect.X + 6, (int)separatorY, panelRect.Width - 12, 1),
                new Rectangle(0, 0, 1, 1), CyberwareTheme.Border * contentAlpha);
            yOffset += 6;

            // 可选列表
            DrawItemList(sb, px, contentAlpha, cyberPlayer, yOffset);

            // 自定义义体Tooltip
            if (hoveredCyberItem != null && !hoveredCyberItem.IsAir) {
                CyberTooltipRenderer.DrawTooltip(sb, hoveredCyberItem, new Vector2(Main.mouseX, Main.mouseY));
            }
        }

        #endregion

        #region 交互

        private void UpdateInteraction(CyberwarePlayer cyberPlayer) {
            hoveredItemRow = -1;
            hoveredCyberItem = null;
            Vector2 mouse = new(Main.mouseX, Main.mouseY);

            if (!panelRect.Contains((int)mouse.X, (int)mouse.Y)) return;

            // 面板区域内拦截游戏输入
            Main.LocalPlayer.mouseInterface = true;

            // 滚轮
            MouseState currentMouseState = Mouse.GetState();
            int scrollDelta = currentMouseState.ScrollWheelValue - oldScrollWheelValue;
            oldScrollWheelValue = currentMouseState.ScrollWheelValue;
            if (scrollDelta != 0) {
                scrollOffset -= scrollDelta * 0.3f;
                scrollOffset = Math.Clamp(scrollOffset, 0, Math.Max(0, compatibleItems.Count * ItemRowHeight - (panelRect.Height - HeaderHeight - CapacityBarHeight - 60)));
                Terraria.GameInput.PlayerInput.LockVanillaMouseScroll("CalamityOverhaul/CyberwareInventory");
            }

            float yOffset = HeaderHeight + CapacityBarHeight + PanelPadding;

            // 检测卸载区域的悬停/点击
            if (hasEquippedItem) {
                Rectangle unequipRect = new(
                    panelRect.X + (int)PanelPadding,
                    panelRect.Y + (int)yOffset,
                    panelRect.Width - (int)(PanelPadding * 2),
                    (int)ItemRowHeight
                );
                if (unequipRect.Contains((int)mouse.X, (int)mouse.Y)) {
                    hoveredItemRow = -2; // 特殊值表示悬停在卸载区
                    if (Main.mouseLeft && Main.mouseLeftRelease) {
                        DoUnequip(cyberPlayer);
                    }
                    // 记录悬停义体用于自定义Tooltip
                    Item equipped = cyberPlayer.EquippedCyberwares[boundSlot];
                    if (equipped != null && !equipped.IsAir) {
                        hoveredCyberItem = equipped;
                    }
                    return;
                }
                yOffset += ItemRowHeight + 6;// 分隔线
            }

            yOffset += 6; // 分隔线后间距

            // 检测物品列表的悬停/点击
            for (int i = 0; i < compatibleItems.Count; i++) {
                float itemY = panelRect.Y + yOffset + i * ItemRowHeight - scrollOffset;
                if (itemY + ItemRowHeight < panelRect.Y + yOffset) continue;
                if (itemY > panelRect.Bottom - PanelPadding) break;

                Rectangle itemRect = new(
                    panelRect.X + (int)PanelPadding,
                    (int)itemY,
                    panelRect.Width - (int)(PanelPadding * 2),
                    (int)ItemRowHeight
                );

                if (itemRect.Contains((int)mouse.X, (int)mouse.Y)) {
                    hoveredItemRow = i;

                    // 记录悬停义体用于自定义Tooltip
                    int invIndex = compatibleItems[i];
                    Item item = Main.LocalPlayer.inventory[invIndex];
                    if (item != null && !item.IsAir) {
                        hoveredCyberItem = item;
                    }

                    // 左键点击装备
                    if (Main.mouseLeft && Main.mouseLeftRelease) {
                        DoEquip(cyberPlayer, i);
                    }
                    break;
                }
            }
        }

        private void DoEquip(CyberwarePlayer cyberPlayer, int listIndex) {
            if (listIndex < 0 || listIndex >= compatibleItems.Count) return;
            int invIndex = compatibleItems[listIndex];
            Player player = Main.LocalPlayer;

            if (invIndex < 0 || invIndex >= player.inventory.Length) return;
            Item item = player.inventory[invIndex];
            if (item == null || item.IsAir) return;
            if (!cyberPlayer.CanEquip(item, boundSlot)) return;

            // 卸载旧义体（如有），归还到背包
            Item oldItem = cyberPlayer.Unequip(boundSlot);
            if (oldItem != null && !oldItem.IsAir) {
                player.QuickSpawnItem(player.GetSource_Misc("CyberwareUnequip"), oldItem, oldItem.stack);
            }

            // 装备新义体（内部会Clone）
            cyberPlayer.Equip(item, boundSlot);

            // 从背包消耗原物品
            item.TurnToAir();

            // 刷新列表
            RefreshItems(cyberPlayer);
            ActionThisFrame = true;
        }

        private void DoUnequip(CyberwarePlayer cyberPlayer) {
            Player player = Main.LocalPlayer;

            Item oldItem = cyberPlayer.Unequip(boundSlot);
            if (oldItem != null && !oldItem.IsAir) {
                player.QuickSpawnItem(player.GetSource_Misc("CyberwareUnequip"), oldItem, oldItem.stack);
            }

            RefreshItems(cyberPlayer);
            ActionThisFrame = true;
        }

        #endregion

        #region 绘制子区域

        private void DrawHeader(SpriteBatch sb, Texture2D px, float alpha, CyberwarePlayer cyberPlayer) {
            // 标题背景
            Rectangle headerRect = new(panelRect.X, panelRect.Y, panelRect.Width, (int)HeaderHeight);
            sb.Draw(px, headerRect, new Rectangle(0, 0, 1, 1), CyberwareTheme.SectionBg * (alpha * 0.8f));

            // 底部红色分隔线
            sb.Draw(px, new Rectangle(panelRect.X + 4, panelRect.Y + (int)HeaderHeight - 1, panelRect.Width - 8, 1),
                new Rectangle(0, 0, 1, 1), CyberwareTheme.Accent * (alpha * 0.5f));

            // 标题文字
            string title = "CYBERWARE";
            if (boundSlot >= 0 && boundSlot < CyberSlotRenderer.Definitions.Length) {
                title = CyberwareUI.Instance?.GetSlotLabel(boundSlot) ?? "CYBERWARE";
            }
            Utils.DrawBorderString(sb, title,
                new Vector2(panelRect.X + PanelPadding, panelRect.Y + 6),
                CyberwareTheme.Accent * alpha, 0.58f * CyberwareTheme.FontScale);

            // 可选物品数量
            string countText = $"{compatibleItems.Count} AVAILABLE";
            Utils.DrawBorderString(sb, countText,
                new Vector2(panelRect.X + PanelPadding, panelRect.Y + 28),
                CyberwareTheme.TextDim * alpha, 0.42f * CyberwareTheme.FontScale);
        }

        private void DrawCapacityBar(SpriteBatch sb, Texture2D px, float alpha, CyberwarePlayer cyberPlayer) {
            float barY = panelRect.Y + HeaderHeight + 2;
            Rectangle barBgRect = new(
                panelRect.X + (int)PanelPadding,
                (int)barY,
                panelRect.Width - (int)(PanelPadding * 2),
                (int)CapacityBarHeight
            );

            // 背景
            sb.Draw(px, barBgRect, new Rectangle(0, 0, 1, 1), CyberwareTheme.SlotEmpty * alpha);

            // 标签
            int used = cyberPlayer.UsedCapacity;
            int max = cyberPlayer.MaxCapacity;
            string capText = $"CAPACITY {used}/{max}";
            Utils.DrawBorderString(sb, capText,
                new Vector2(barBgRect.X + 4, barBgRect.Y + 2),
                CyberwareTheme.TextNormal * alpha, 0.42f * CyberwareTheme.FontScale);

            // 进度条
            float ratio = max > 0 ? (float)used / max : 0;
            int barInner = barBgRect.Width - 4;
            Rectangle fillRect = new(barBgRect.X + 2, barBgRect.Y + 16, (int)(barInner * ratio), 4);

            Color barColor = ratio > 0.85f ? CyberwareTheme.Accent :
                ratio > 0.6f ? CyberwareTheme.AccentGold : CyberwareTheme.AccentCyan;
            sb.Draw(px, fillRect, new Rectangle(0, 0, 1, 1), barColor * (alpha * 0.8f));

            // 容量条底色
            Rectangle emptyRect = new(fillRect.Right, fillRect.Y, barInner - fillRect.Width, 4);
            sb.Draw(px, emptyRect, new Rectangle(0, 0, 1, 1), CyberwareTheme.Border * (alpha * 0.4f));
        }

        private float DrawEquippedSection(SpriteBatch sb, Texture2D px, float alpha, CyberwarePlayer cyberPlayer, float yOffset) {
            if (boundSlot < 0 || boundSlot >= CyberwarePlayer.SlotCount) return yOffset;
            Item equipped = cyberPlayer.EquippedCyberwares[boundSlot];
            if (equipped == null || equipped.IsAir) return yOffset;

            // "INSTALLED" 标签
            Utils.DrawBorderString(sb, "[ INSTALLED ]",
                new Vector2(panelRect.X + PanelPadding, panelRect.Y + yOffset - 2),
                CyberwareTheme.AccentGold * (alpha * 0.7f), 0.40f * CyberwareTheme.FontScale);

            yOffset += 12;

            // 已装备物品行
            Rectangle eqRect = new(
                panelRect.X + (int)PanelPadding,
                panelRect.Y + (int)yOffset,
                panelRect.Width - (int)(PanelPadding * 2),
                (int)ItemRowHeight
            );

            bool isHoveredUnequip = hoveredItemRow == -2;
            Color rowBg = isHoveredUnequip
                ? Color.Lerp(CyberwareTheme.SlotEmpty, CyberwareTheme.Accent, 0.15f)
                : CyberwareTheme.SlotEmpty;
            sb.Draw(px, eqRect, new Rectangle(0, 0, 1, 1), rowBg * (alpha * 0.7f));

            // 边框
            Color eqBorder = isHoveredUnequip ? CyberwareTheme.Accent : CyberwareTheme.AccentGold;
            sb.Draw(px, new Rectangle(eqRect.X, eqRect.Y, eqRect.Width, 1), new Rectangle(0, 0, 1, 1), eqBorder * (alpha * 0.6f));
            sb.Draw(px, new Rectangle(eqRect.X, eqRect.Bottom - 1, eqRect.Width, 1), new Rectangle(0, 0, 1, 1), eqBorder * (alpha * 0.3f));

            // 物品图标
            DrawItemIcon(sb, equipped, new Vector2(eqRect.X + 4, eqRect.Y + 4), alpha);

            // 物品名称
            string name = equipped.Name ?? "???";
            if (name.Length > 18) name = name[..17] + "…";
            Utils.DrawBorderString(sb, name,
                new Vector2(eqRect.X + 44, eqRect.Y + 4),
                CyberwareTheme.TextBright * alpha, 0.48f * CyberwareTheme.FontScale);

            // 卸载提示
            string hint = isHoveredUnequip ? "> UNINSTALL <" : "CLICK TO UNINSTALL";
            Color hintColor = isHoveredUnequip ? CyberwareTheme.Accent : CyberwareTheme.TextDim;
            Utils.DrawBorderString(sb, hint,
                new Vector2(eqRect.X + 44, eqRect.Y + 28),
                hintColor * (alpha * 0.65f), 0.38f * CyberwareTheme.FontScale);

            return yOffset + ItemRowHeight + 4;
        }

        private void DrawItemList(SpriteBatch sb, Texture2D px, float alpha, CyberwarePlayer cyberPlayer, float yOffset) {
            if (compatibleItems.Count == 0) {
                Utils.DrawBorderString(sb, "NO COMPATIBLE",
                    new Vector2(panelRect.X + PanelPadding, panelRect.Y + yOffset + 8),
                    CyberwareTheme.TextDim * (alpha * 0.5f), 0.44f * CyberwareTheme.FontScale);
                Utils.DrawBorderString(sb, "CYBERWARE FOUND",
                    new Vector2(panelRect.X + PanelPadding, panelRect.Y + yOffset + 28),
                    CyberwareTheme.TextDim * (alpha * 0.5f), 0.44f * CyberwareTheme.FontScale);
                return;
            }

            // 列表标签
            Utils.DrawBorderString(sb, "AVAILABLE",
                new Vector2(panelRect.X + PanelPadding, panelRect.Y + yOffset - 2),
                CyberwareTheme.AccentCyan * (alpha * 0.6f), 0.40f * CyberwareTheme.FontScale);
            yOffset += 18;

            float listTop = panelRect.Y + yOffset;
            float listBottom = panelRect.Bottom - PanelPadding;

            for (int i = 0; i < compatibleItems.Count; i++) {
                float itemY = listTop + i * ItemRowHeight - scrollOffset;
                if (itemY + ItemRowHeight < listTop) continue;
                if (itemY > listBottom) break;

                int invIndex = compatibleItems[i];
                Item item = Main.LocalPlayer.inventory[invIndex];
                if (item == null || item.IsAir) continue;

                Rectangle itemRect = new(
                    panelRect.X + (int)PanelPadding,
                    (int)itemY,
                    panelRect.Width - (int)(PanelPadding * 2),
                    (int)ItemRowHeight - 2
                );

                bool isHovered = hoveredItemRow == i;
                bool canEquip = cyberPlayer.CanEquip(item, boundSlot);

                // 行背景
                Color rowBg = isHovered
                    ? Color.Lerp(CyberwareTheme.SlotEmpty, canEquip ? CyberwareTheme.AccentCyan : CyberwareTheme.Accent, 0.12f)
                    : CyberwareTheme.SlotInnerBg;
                sb.Draw(px, itemRect, new Rectangle(0, 0, 1, 1), rowBg * (alpha * 0.7f));

                // 行边框
                if (isHovered) {
                    Color hBorder = canEquip ? CyberwareTheme.AccentCyan : CyberwareTheme.Accent;
                    sb.Draw(px, new Rectangle(itemRect.X, itemRect.Y, itemRect.Width, 1), new Rectangle(0, 0, 1, 1), hBorder * (alpha * 0.5f));
                    sb.Draw(px, new Rectangle(itemRect.X, itemRect.Y, 2, itemRect.Height), new Rectangle(0, 0, 1, 1), hBorder * (alpha * 0.6f));
                }

                // 物品图标
                DrawItemIcon(sb, item, new Vector2(itemRect.X + 5, itemRect.Y + 4), alpha);

                // 物品名称
                string name = item.Name ?? "???";
                if (name.Length > 16) name = name[..15] + "…";
                Color nameColor = canEquip ? CyberwareTheme.TextBright : CyberwareTheme.TextDim;
                Utils.DrawBorderString(sb, name,
                    new Vector2(itemRect.X + 44, itemRect.Y + 2),
                    nameColor * alpha, 0.44f * CyberwareTheme.FontScale);

                // 容量消耗提示
                if (item.ModItem is BaseCyberware cyber) {
                    string costText = $"CAP: {cyber.CapacityCost}";
                    Color costColor = canEquip ? CyberwareTheme.AccentCyan : CyberwareTheme.Accent;
                    Utils.DrawBorderString(sb, costText,
                        new Vector2(itemRect.X + 44, itemRect.Y + 26),
                        costColor * (alpha * 0.55f), 0.38f * CyberwareTheme.FontScale);

                    if (!canEquip) {
                        Utils.DrawBorderString(sb, "OVER CAP",
                            new Vector2(itemRect.X + 130, itemRect.Y + 26),
                            CyberwareTheme.Accent * (alpha * 0.5f), 0.36f * CyberwareTheme.FontScale);
                    }
                }
            }

            // 滚动条
            if (compatibleItems.Count * ItemRowHeight > listBottom - listTop) {
                float totalHeight = compatibleItems.Count * ItemRowHeight;
                float viewHeight = listBottom - listTop;
                float scrollBarHeight = Math.Max(20, viewHeight * viewHeight / totalHeight);
                float scrollBarY = listTop + scrollOffset / totalHeight * viewHeight;
                scrollBarY = Math.Clamp(scrollBarY, listTop, listBottom - scrollBarHeight);

                sb.Draw(px,
                    new Rectangle(panelRect.Right - (int)ScrollBarWidth - 2, (int)scrollBarY, (int)ScrollBarWidth, (int)scrollBarHeight),
                    new Rectangle(0, 0, 1, 1), CyberwareTheme.Accent * (alpha * 0.3f));
            }
        }

        private static void DrawItemIcon(SpriteBatch sb, Item item, Vector2 position, float alpha) {
            if (item == null || item.IsAir) return;

            Texture2D tex = TextureAssets.Item[item.type]?.Value;
            if (tex == null) return;

            // 适配券38x38的范围内
            float maxDim = Math.Max(tex.Width, tex.Height);
            float scale = maxDim > 38 ? 38f / maxDim : 1f;
            Vector2 iconCenter = position + new Vector2(19, 19);

            sb.Draw(tex, iconCenter, null, Color.White * alpha,
                0f, tex.Size() / 2f, scale, SpriteEffects.None, 0f);
        }

        #endregion
    }
}
