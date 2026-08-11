using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.UIs.NotificationPopup;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.Cyberwares.UIs
{
    /// <summary>侧栏义体背包，显示当前槽位可装列表</summary>
    internal class CyberInventoryPanel
    {
        #region 常量

        private const float PanelWidth = 240f;
        private const float PanelPadding = 10f;
        private const float ItemRowHeight = 50f;
        //字号放大后头部/容量条加高
        private const float HeaderHeight = 44f;
        private const float CapacityBarHeight = 26f;
        private const float ScrollBarWidth = 5f;

        #endregion

        #region 状态

        /// <summary>绑定槽位，-1 未选</summary>
        private int boundSlot = -1;

        /// <summary>展开进度 0~1</summary>
        private float openProgress;

        /// <summary>面板矩形</summary>
        private Rectangle panelRect;

        /// <summary>背包可装物品索引</summary>
        private readonly List<int> compatibleItems = [];

        /// <summary>悬停行，-1 无</summary>
        private int hoveredItemRow = -1;

        /// <summary>滚动偏移</summary>
        private float scrollOffset;

        /// <summary>已装备区可见</summary>
        private bool hasEquippedItem;

        /// <summary>上帧滚轮值</summary>
        private int oldScrollWheelValue;

        /// <summary>找 Victor 提醒节流</summary>
        private int reminderCooldown;

        /// <summary>本帧装备/卸载操作</summary>
        public bool ActionThisFrame { get; private set; }

        /// <summary>悬停义体，Tooltip 用</summary>
        private Item hoveredCyberItem;

        /// <summary>面板可见</summary>
        public bool IsVisible => boundSlot >= 0 || openProgress > 0.01f;

        #endregion

        #region 公共方法

        public void BindSlot(int slotIndex, CyberwarePlayer cyberPlayer) {
            if (slotIndex == boundSlot) return;
            boundSlot = slotIndex;
            scrollOffset = 0;
            oldScrollWheelValue = Mouse.GetState().ScrollWheelValue;
            RefreshItems(cyberPlayer);
        }

        public void Unbind() {
            boundSlot = -1;
            hasEquippedItem = false;
        }

        public void RefreshItems(CyberwarePlayer cyberPlayer) {
            compatibleItems.Clear();
            hasEquippedItem = false;
            if (boundSlot < 0 || cyberPlayer == null) return;

            compatibleItems.AddRange(cyberPlayer.GetCompatibleItems(boundSlot));

            Item equipped = cyberPlayer.EquippedCyberwares[boundSlot];
            hasEquippedItem = equipped != null && !equipped.IsAir;
        }

        public void Update(Rectangle mainPanelRect, int selectedSlot, CyberwarePlayer cyberPlayer) {
            ActionThisFrame = false;
            if (reminderCooldown > 0) {
                reminderCooldown--;
            }

            //同步绑定
            if (selectedSlot != boundSlot) {
                if (selectedSlot >= 0) {
                    BindSlot(selectedSlot, cyberPlayer);
                }
                else {
                    Unbind();
                }
            }

            //动画过渡
            float target = boundSlot >= 0 ? 1f : 0f;
            openProgress += (target - openProgress) * 0.18f;
            if (boundSlot < 0 && openProgress < 0.01f) {
                openProgress = 0;
                return;
            }

            //左/右槽位决定侧栏展开方向
            bool isLeftSlot = boundSlot >= 0 && boundSlot < 6;
            float easedProgress = VaultUtils.EaseOutCubic(Math.Clamp(openProgress, 0, 1));
            float actualWidth = PanelWidth * easedProgress;

            if (isLeftSlot) {
                //面板在主面板左侧
                panelRect = new Rectangle(
                    (int)(mainPanelRect.X - actualWidth - 6),
                    mainPanelRect.Y,
                    (int)actualWidth,
                    mainPanelRect.Height
                );
            }
            else {
                //面板在主面板右侧
                panelRect = new Rectangle(
                    mainPanelRect.Right + 6,
                    mainPanelRect.Y,
                    (int)actualWidth,
                    mainPanelRect.Height
                );
            }

            //交互检测
            if (openProgress > 0.5f) {
                UpdateInteraction(cyberPlayer);
            }
        }

        public void Draw(SpriteBatch sb, float parentAlpha, CyberwarePlayer cyberPlayer) {
            if (openProgress < 0.01f || panelRect.Width < 2) return;

            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null) return;

            float alpha = parentAlpha * Math.Clamp(openProgress, 0, 1);

            //uMode=1 轻量背板，无中央光场
            CyberPanelRenderer.DrawShaderBackground(sb, alpha * 0.95f, panelRect, Vector2.Zero, 0f, mode: 1);

            //边框
            Color borderColor = CyberwareTheme.Accent * (alpha * 0.6f);
            sb.Draw(px, new Rectangle(panelRect.X, panelRect.Y, panelRect.Width, 1), new Rectangle(0, 0, 1, 1), borderColor);
            sb.Draw(px, new Rectangle(panelRect.X, panelRect.Bottom - 1, panelRect.Width, 1), new Rectangle(0, 0, 1, 1), borderColor * 0.5f);
            sb.Draw(px, new Rectangle(panelRect.X, panelRect.Y, 1, panelRect.Height), new Rectangle(0, 0, 1, 1), borderColor * 0.7f);
            sb.Draw(px, new Rectangle(panelRect.Right - 1, panelRect.Y, 1, panelRect.Height), new Rectangle(0, 0, 1, 1), borderColor * 0.7f);

            if (openProgress < 0.6f) return;
            float contentAlpha = alpha * Math.Clamp((openProgress - 0.6f) / 0.4f, 0, 1);

            //标题头部
            DrawHeader(sb, px, contentAlpha, cyberPlayer);

            //容量条
            DrawCapacityBar(sb, px, contentAlpha, cyberPlayer);

            //已装备义体信息
            float yOffset = HeaderHeight + CapacityBarHeight + PanelPadding;
            if (hasEquippedItem) {
                yOffset = DrawEquippedSection(sb, px, contentAlpha, cyberPlayer, yOffset);
            }

            //分隔线
            float separatorY = panelRect.Y + yOffset;
            sb.Draw(px, new Rectangle(panelRect.X + 6, (int)separatorY, panelRect.Width - 12, 1),
                new Rectangle(0, 0, 1, 1), CyberwareTheme.Border * contentAlpha);
            yOffset += 6;

            //可选列表
            DrawItemList(sb, px, contentAlpha, cyberPlayer, yOffset);

            //自定义义体Tooltip
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

            //面板区域内拦截游戏输入
            Main.LocalPlayer.mouseInterface = true;

            //两把锁都每帧常驻：UI 跑在绘制阶段，等检测到 delta 再锁就晚一帧。
            //SuppressWeaponSwitch 管换武器（tick 倒计时，不受时序影响），
            //LockVanillaMouseScroll 管背包开启时的配方栏滚动
            UIInputGuard.SuppressWeaponSwitch();
            Terraria.GameInput.PlayerInput.LockVanillaMouseScroll("CalamityOverhaul/CyberwareInventory");

            //滚轮
            MouseState currentMouseState = Mouse.GetState();
            int scrollDelta = currentMouseState.ScrollWheelValue - oldScrollWheelValue;
            oldScrollWheelValue = currentMouseState.ScrollWheelValue;
            if (scrollDelta != 0) {
                scrollOffset -= scrollDelta * 0.3f;
                scrollOffset = Math.Clamp(scrollOffset, 0, Math.Max(0, compatibleItems.Count * ItemRowHeight - (panelRect.Height - HeaderHeight - CapacityBarHeight - 60)));
            }

            float yOffset = HeaderHeight + CapacityBarHeight + PanelPadding;

            //检测卸载区域的悬停/点击
            if (hasEquippedItem) {
                Rectangle unequipRect = new(
                    panelRect.X + (int)PanelPadding,
                    panelRect.Y + (int)yOffset,
                    panelRect.Width - (int)(PanelPadding * 2),
                    (int)ItemRowHeight
                );
                if (unequipRect.Contains((int)mouse.X, (int)mouse.Y)) {
                    hoveredItemRow = -2;//悬停卸载区
                    //只读，点提醒找 Victor
                    if (Main.mouseLeft && Main.mouseLeftRelease) {
                        NotifyClinicRequired();
                    }
                    //悬停义体，自定义 Tooltip
                    Item equipped = cyberPlayer.EquippedCyberwares[boundSlot];
                    if (equipped != null && !equipped.IsAir) {
                        hoveredCyberItem = equipped;
                    }
                    return;
                }
                yOffset += ItemRowHeight + 6;//分隔线
            }

            yOffset += 6; //分隔线后间距

            //检测物品列表的悬停/点击
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

                    //悬停义体，自定义 Tooltip
                    int invIndex = compatibleItems[i];
                    Item item = Main.LocalPlayer.inventory[invIndex];
                    if (item != null && !item.IsAir) {
                        hoveredCyberItem = item;
                    }

                    //只读，点提醒找 Victor
                    if (Main.mouseLeft && Main.mouseLeftRelease) {
                        NotifyClinicRequired();
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// 只读界面装/卸弹 Victor 提醒；真换装走诊所手术
        /// </summary>
        private void NotifyClinicRequired() {
            if (reminderCooldown > 0) {
                return;
            }
            reminderCooldown = 40;
            //HackTime 红色警示弹窗样式
            NotificationPopupSystem.Add(new HackTimeAccessDeniedEntry(
                CyberwareUI.ClinicRequiredTitle, CyberwareUI.ClinicRequiredDesc));
            ActionThisFrame = true;
        }

        #endregion

        #region 绘制子区域

        private void DrawHeader(SpriteBatch sb, Texture2D px, float alpha, CyberwarePlayer cyberPlayer) {
            //标题背景
            Rectangle headerRect = new(panelRect.X, panelRect.Y, panelRect.Width, (int)HeaderHeight);
            sb.Draw(px, headerRect, new Rectangle(0, 0, 1, 1), CyberwareTheme.SectionBg * (alpha * 0.8f));

            //底部红色分隔线
            sb.Draw(px, new Rectangle(panelRect.X + 4, panelRect.Y + (int)HeaderHeight - 1, panelRect.Width - 8, 1),
                new Rectangle(0, 0, 1, 1), CyberwareTheme.Accent * (alpha * 0.5f));

            //标题文字
            string title = "CYBERWARE";
            if (boundSlot >= 0 && boundSlot < CyberSlotRenderer.Definitions.Length) {
                title = CyberwareUI.Instance?.GetSlotLabel(boundSlot) ?? "CYBERWARE";
            }
            Utils.DrawBorderString(sb, title,
                new Vector2(panelRect.X + PanelPadding, panelRect.Y + 6),
                CyberwareTheme.Accent * alpha, 0.58f * CyberwareTheme.FontScale);

            //可选物品数量
            string countText = $"{compatibleItems.Count} AVAILABLE";
            Utils.DrawBorderString(sb, countText,
                new Vector2(panelRect.X + PanelPadding, panelRect.Y + 28),
                CyberwareTheme.TextDim * alpha, 0.46f * CyberwareTheme.FontScale);
        }

        private void DrawCapacityBar(SpriteBatch sb, Texture2D px, float alpha, CyberwarePlayer cyberPlayer) {
            float barY = panelRect.Y + HeaderHeight + 2;
            Rectangle barBgRect = new(
                panelRect.X + (int)PanelPadding,
                (int)barY,
                panelRect.Width - (int)(PanelPadding * 2),
                (int)CapacityBarHeight
            );

            //背景
            sb.Draw(px, barBgRect, new Rectangle(0, 0, 1, 1), CyberwareTheme.SlotEmpty * alpha);

            //标签
            int used = cyberPlayer.UsedCapacity;
            int max = cyberPlayer.MaxCapacity;
            string capText = $"CAPACITY {used}/{max}";
            Utils.DrawBorderString(sb, capText,
                new Vector2(barBgRect.X + 4, barBgRect.Y + 2),
                CyberwareTheme.TextNormal * alpha, 0.46f * CyberwareTheme.FontScale);

            //进度条
            float ratio = max > 0 ? (float)used / max : 0;
            int barInner = barBgRect.Width - 4;
            Rectangle fillRect = new(barBgRect.X + 2, barBgRect.Y + 16, (int)(barInner * ratio), 4);

            Color barColor = ratio > 0.85f ? CyberwareTheme.Accent :
                ratio > 0.6f ? CyberwareTheme.AccentGold : CyberwareTheme.AccentCyan;
            sb.Draw(px, fillRect, new Rectangle(0, 0, 1, 1), barColor * (alpha * 0.8f));

            //容量条底色
            Rectangle emptyRect = new(fillRect.Right, fillRect.Y, barInner - fillRect.Width, 4);
            sb.Draw(px, emptyRect, new Rectangle(0, 0, 1, 1), CyberwareTheme.Border * (alpha * 0.4f));
        }

        private float DrawEquippedSection(SpriteBatch sb, Texture2D px, float alpha, CyberwarePlayer cyberPlayer, float yOffset) {
            if (boundSlot < 0 || boundSlot >= CyberwarePlayer.SlotCount) return yOffset;
            Item equipped = cyberPlayer.EquippedCyberwares[boundSlot];
            if (equipped == null || equipped.IsAir) return yOffset;

            //"INSTALLED" 标签
            Utils.DrawBorderString(sb, "[ INSTALLED ]",
                new Vector2(panelRect.X + PanelPadding, panelRect.Y + yOffset - 2),
                CyberwareTheme.AccentGold * (alpha * 0.7f), 0.44f * CyberwareTheme.FontScale);

            yOffset += 12;

            //已装备物品行
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

            //边框
            Color eqBorder = isHoveredUnequip ? CyberwareTheme.Accent : CyberwareTheme.AccentGold;
            sb.Draw(px, new Rectangle(eqRect.X, eqRect.Y, eqRect.Width, 1), new Rectangle(0, 0, 1, 1), eqBorder * (alpha * 0.6f));
            sb.Draw(px, new Rectangle(eqRect.X, eqRect.Bottom - 1, eqRect.Width, 1), new Rectangle(0, 0, 1, 1), eqBorder * (alpha * 0.3f));

            //物品图标
            DrawItemIcon(sb, equipped, new Vector2(eqRect.X + 4, eqRect.Y + 4), alpha);

            //物品名称
            string name = equipped.Name ?? "???";
            if (name.Length > 18) name = name[..17] + "…";
            Utils.DrawBorderString(sb, name,
                new Vector2(eqRect.X + 44, eqRect.Y + 4),
                CyberwareTheme.TextBright * alpha, 0.52f * CyberwareTheme.FontScale);

            //只读引导
            string hint = isHoveredUnequip ? "> SEE RIPPERDOC <" : "VISIT VICTOR TO SWAP";
            Color hintColor = isHoveredUnequip ? CyberwareTheme.Accent : CyberwareTheme.TextDim;
            Utils.DrawBorderString(sb, hint,
                new Vector2(eqRect.X + 44, eqRect.Y + 28),
                hintColor * (alpha * 0.65f), 0.44f * CyberwareTheme.FontScale);

            return yOffset + ItemRowHeight + 4;
        }

        private void DrawItemList(SpriteBatch sb, Texture2D px, float alpha, CyberwarePlayer cyberPlayer, float yOffset) {
            if (compatibleItems.Count == 0) {
                Utils.DrawBorderString(sb, "NO COMPATIBLE",
                    new Vector2(panelRect.X + PanelPadding, panelRect.Y + yOffset + 8),
                    CyberwareTheme.TextDim * (alpha * 0.5f), 0.46f * CyberwareTheme.FontScale);
                Utils.DrawBorderString(sb, "CYBERWARE FOUND",
                    new Vector2(panelRect.X + PanelPadding, panelRect.Y + yOffset + 28),
                    CyberwareTheme.TextDim * (alpha * 0.5f), 0.46f * CyberwareTheme.FontScale);
                return;
            }

            //列表标签
            Utils.DrawBorderString(sb, "AVAILABLE",
                new Vector2(panelRect.X + PanelPadding, panelRect.Y + yOffset - 2),
                CyberwareTheme.AccentCyan * (alpha * 0.6f), 0.44f * CyberwareTheme.FontScale);
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

                //行背景
                Color rowBg = isHovered
                    ? Color.Lerp(CyberwareTheme.SlotEmpty, canEquip ? CyberwareTheme.AccentCyan : CyberwareTheme.Accent, 0.12f)
                    : CyberwareTheme.SlotInnerBg;
                sb.Draw(px, itemRect, new Rectangle(0, 0, 1, 1), rowBg * (alpha * 0.7f));

                //行边框
                if (isHovered) {
                    Color hBorder = canEquip ? CyberwareTheme.AccentCyan : CyberwareTheme.Accent;
                    sb.Draw(px, new Rectangle(itemRect.X, itemRect.Y, itemRect.Width, 1), new Rectangle(0, 0, 1, 1), hBorder * (alpha * 0.5f));
                    sb.Draw(px, new Rectangle(itemRect.X, itemRect.Y, 2, itemRect.Height), new Rectangle(0, 0, 1, 1), hBorder * (alpha * 0.6f));
                }

                //物品图标
                DrawItemIcon(sb, item, new Vector2(itemRect.X + 5, itemRect.Y + 4), alpha);

                //物品名称
                string name = item.Name ?? "???";
                if (name.Length > 16) name = name[..15] + "…";
                Color nameColor = canEquip ? CyberwareTheme.TextBright : CyberwareTheme.TextDim;
                Utils.DrawBorderString(sb, name,
                    new Vector2(itemRect.X + 44, itemRect.Y + 2),
                    nameColor * alpha, 0.50f * CyberwareTheme.FontScale);

                //容量消耗提示
                if (item.ModItem is BaseCyberware cyber) {
                    string costText = $"CAP: {cyber.CapacityCost}";
                    Color costColor = canEquip ? CyberwareTheme.AccentCyan : CyberwareTheme.Accent;
                    Utils.DrawBorderString(sb, costText,
                        new Vector2(itemRect.X + 44, itemRect.Y + 26),
                        costColor * (alpha * 0.55f), 0.42f * CyberwareTheme.FontScale);

                    if (!canEquip) {
                        Utils.DrawBorderString(sb, "OVER CAP",
                            new Vector2(itemRect.X + 130, itemRect.Y + 26),
                            CyberwareTheme.Accent * (alpha * 0.5f), 0.42f * CyberwareTheme.FontScale);
                    }
                }
            }

            //滚动条
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

            //适配券38x38的范围内
            float maxDim = Math.Max(tex.Width, tex.Height);
            float scale = maxDim > 38 ? 38f / maxDim : 1f;
            Vector2 iconCenter = position + new Vector2(19, 19);

            sb.Draw(tex, iconCenter, null, Color.White * alpha,
                0f, tex.Size() / 2f, scale, SpriteEffects.None, 0f);
        }

        #endregion
    }
}
