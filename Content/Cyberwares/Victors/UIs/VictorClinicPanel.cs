using CalamityOverhaul.Content.Cyberwares.Implementation.CstmVisualEyes;
using CalamityOverhaul.Content.Cyberwares.Implementation.MimicPerchedAuxBrains;
using CalamityOverhaul.Content.Cyberwares.Implementation.OmniElectricFoots;
using CalamityOverhaul.Content.Cyberwares.Implementation.PlowSteelClampArms;
using CalamityOverhaul.Content.Cyberwares.Implementation.PrimePlasamas;
using CalamityOverhaul.Content.Cyberwares.Implementation.Sandevistans;
using CalamityOverhaul.Content.Cyberwares.Implementation.SCCA32CRPs;
using CalamityOverhaul.Content.Cyberwares.Implementation.SelfHackCrystals;
using CalamityOverhaul.Content.Cyberwares.Implementation.SelfHealingSkelents;
using CalamityOverhaul.Content.Cyberwares.UIs;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Victors.UIs
{
    /// <summary>
    /// 诊所侧栏：已安装/已拥有/在售三段
    /// <br/>安装走手术，卸载归背包，购买扣金入背包
    /// </summary>
    internal class VictorClinicPanel
    {
        #region 常量

        private const float PanelWidth = 322f;
        private const float PanelPadding = 12f;
        private const float HeaderHeight = 52f;
        private const float CapacityBarHeight = 30f;
        private const float InstalledRowHeight = 56f;
        private const float LabelHeight = 24f;
        private const float RowHeight = 56f;
        private const float ScrollBarWidth = 5f;

        private const int KindLabel = 0;
        private const int KindOwned = 1;
        private const int KindShop = 2;
        private const int LabelOwned = 0;
        private const int LabelShop = 1;

        #endregion

        #region 商店静态数据（按槽位分组）

        private static int[] allShopTypes;
        private static Dictionary<int, List<int>> shopBySlot;

        private static void EnsureShopData() {
            if (allShopTypes != null) {
                return;
            }
            allShopTypes = [
                ModContent.ItemType<MimicPerchedAuxBrain>(),
                ModContent.ItemType<CstmVisualEye>(),
                ModContent.ItemType<SCCA32CRP>(),
                ModContent.ItemType<PlowSteelClampArm>(),
                ModContent.ItemType<OmniElectricFoot>(),
                ModContent.ItemType<SelfHackCrystal>(),
                ModContent.ItemType<SandevistansItem>(),
                ModContent.ItemType<PrimePlasama>(),
                ModContent.ItemType<SelfHealingSkelent>(),
            ];
            shopBySlot = [];
            foreach (int t in allShopTypes) {
                if (ContentSamples.ItemsByType.TryGetValue(t, out Item it) && it.ModItem is BaseCyberware bc) {
                    int slot = (int)bc.SlotCategory;
                    if (!shopBySlot.TryGetValue(slot, out List<int> list)) {
                        list = [];
                        shopBySlot[slot] = list;
                    }
                    list.Add(t);
                }
            }
        }

        private static long PriceOf(int type) =>
            ContentSamples.ItemsByType.TryGetValue(type, out Item it) && it.value > 0 ? it.value : Item.buyPrice(0, 5);

        #endregion

        #region 状态

        private readonly struct Row(int kind, int value)
        {
            public readonly int Kind = kind;
            public readonly int Value = value;
        }

        private int boundSlot = -1;
        private float openProgress;
        private Rectangle panelRect;
        private float scrollOffset;
        private int oldScrollWheelValue;
        private bool hasEquippedItem;
        private int lastOwnedCount = -1;
        private int lastEquippedType = -1;

        private readonly List<Row> rows = [];
        private CyberwarePlayer cyberPlayer;
        private Item hoveredCyberItem;
        private int hoveredEntryKey = int.MinValue;
        private int lastHoverKey = int.MinValue;
        private float hoverAnim;

        public bool ActionThisFrame { get; private set; }
        public bool IsVisible => boundSlot >= 0 || openProgress > 0.01f;

        #endregion

        #region 绑定 / 刷新

        public void BindSlot(int slotIndex, CyberwarePlayer cp) {
            if (slotIndex == boundSlot) {
                return;
            }
            boundSlot = slotIndex;
            scrollOffset = 0;
            oldScrollWheelValue = Mouse.GetState().ScrollWheelValue;
            RefreshItems(cp);
        }

        public void Unbind() {
            boundSlot = -1;
            hasEquippedItem = false;
            rows.Clear();
            lastOwnedCount = -1;
            lastEquippedType = -1;
        }

        public void RefreshItems(CyberwarePlayer cp) {
            EnsureShopData();
            rows.Clear();
            hasEquippedItem = false;
            cyberPlayer = cp;
            if (boundSlot < 0 || cp == null) {
                return;
            }

            Item equipped = cp.EquippedCyberwares[boundSlot];
            hasEquippedItem = equipped != null && !equipped.IsAir;
            int installedType = hasEquippedItem ? equipped.type : -1;

            List<int> owned = cp.GetCompatibleItems(boundSlot);
            HashSet<int> ownedTypes = [];
            rows.Add(new Row(KindLabel, LabelOwned));
            foreach (int invIndex in owned) {
                rows.Add(new Row(KindOwned, invIndex));
                Item it = Main.LocalPlayer.inventory[invIndex];
                if (it != null && !it.IsAir) {
                    ownedTypes.Add(it.type);
                }
            }

            rows.Add(new Row(KindLabel, LabelShop));
            if (shopBySlot.TryGetValue(boundSlot, out List<int> shopTypes)) {
                foreach (int t in shopTypes) {
                    if (t == installedType || ownedTypes.Contains(t)) {
                        continue;
                    }
                    rows.Add(new Row(KindShop, t));
                }
            }

            lastOwnedCount = owned.Count;
            lastEquippedType = installedType;
        }

        #endregion

        #region 更新

        public void Update(Rectangle mainPanelRect, int selectedSlot, CyberwarePlayer cp) {
            ActionThisFrame = false;
            cyberPlayer = cp;

            if (selectedSlot != boundSlot) {
                if (selectedSlot >= 0) {
                    BindSlot(selectedSlot, cp);
                }
                else {
                    Unbind();
                }
            }
            else if (boundSlot >= 0 && cp != null) {
                int equippedType = cp.EquippedCyberwares[boundSlot]?.type ?? 0;
                int ownedCount = 0;
                for (int i = 0; i < Main.InventorySlotsTotal; i++) {
                    Item inv = Main.LocalPlayer.inventory[i];
                    if (inv != null && !inv.IsAir && inv.ModItem is BaseCyberware bc && (int)bc.SlotCategory == boundSlot) {
                        ownedCount++;
                    }
                }
                if (ownedCount != lastOwnedCount || equippedType != lastEquippedType) {
                    RefreshItems(cp);
                }
            }

            float target = boundSlot >= 0 ? 1f : 0f;
            openProgress += (target - openProgress) * 0.18f;
            if (boundSlot < 0 && openProgress < 0.01f) {
                openProgress = 0f;
                return;
            }

            bool isLeftSlot = boundSlot is >= 0 and < 6;
            float eased = VaultUtils.EaseOutCubic(Math.Clamp(openProgress, 0f, 1f));
            float actualWidth = PanelWidth * eased;
            panelRect = isLeftSlot
                ? new Rectangle((int)(mainPanelRect.X - actualWidth - 8), mainPanelRect.Y, (int)actualWidth, mainPanelRect.Height)
                : new Rectangle(mainPanelRect.Right + 8, mainPanelRect.Y, (int)actualWidth, mainPanelRect.Height);

            if (openProgress > 0.5f) {
                UpdateInteraction();
            }

            //悬停 key 变时重置 hoverAnim
            if (hoveredEntryKey != lastHoverKey) {
                hoverAnim = 0f;
                lastHoverKey = hoveredEntryKey;
            }
            hoverAnim = MathHelper.Clamp(hoverAnim + (hoveredEntryKey != int.MinValue ? 0.2f : -0.3f), 0f, 1f);
        }

        private void UpdateInteraction() {
            hoveredEntryKey = int.MinValue;
            hoveredCyberItem = null;
            Point mouse = new(Main.mouseX, Main.mouseY);
            if (!panelRect.Contains(mouse)) {
                return;
            }

            Main.LocalPlayer.mouseInterface = true;

            int wheel = Mouse.GetState().ScrollWheelValue;
            int delta = wheel - oldScrollWheelValue;
            oldScrollWheelValue = wheel;
            if (delta != 0) {
                scrollOffset = Math.Clamp(scrollOffset - delta * 0.35f, 0f, MaxScroll());
                Terraria.GameInput.PlayerInput.LockVanillaMouseScroll("CalamityOverhaul/VictorClinic");
            }

            if (hasEquippedItem) {
                Rectangle installedRect = InstalledRect();
                if (installedRect.Contains(mouse)) {
                    hoveredEntryKey = -2;
                    Item eq = cyberPlayer.EquippedCyberwares[boundSlot];
                    if (eq != null && !eq.IsAir) {
                        hoveredCyberItem = eq;
                    }
                    if (Main.mouseLeft && Main.mouseLeftRelease) {
                        DoUnequip();
                    }
                    return;
                }
            }

            int index = 0;
            foreach ((Row row, Rectangle rect) in VisibleRows()) {
                if (row.Kind != KindLabel && rect.Contains(mouse)) {
                    hoveredEntryKey = row.Kind * 10000 + index;
                    if (row.Kind == KindOwned) {
                        Item it = Main.LocalPlayer.inventory[row.Value];
                        if (it != null && !it.IsAir) {
                            hoveredCyberItem = it;
                        }
                        if (Main.mouseLeft && Main.mouseLeftRelease) {
                            DoInstall(row.Value);
                        }
                    }
                    else if (row.Kind == KindShop) {
                        if (ContentSamples.ItemsByType.TryGetValue(row.Value, out Item sample)) {
                            hoveredCyberItem = sample;
                        }
                        if (Main.mouseLeft && Main.mouseLeftRelease) {
                            DoBuy(row.Value);
                        }
                    }
                    break;
                }
                index++;
            }
        }

        private void DoInstall(int invIndex) {
            Player player = Main.LocalPlayer;
            if (invIndex < 0 || invIndex >= player.inventory.Length) {
                return;
            }
            Item item = player.inventory[invIndex];
            if (item == null || item.IsAir || !cyberPlayer.CanEquip(item, boundSlot)) {
                SoundEngine.PlaySound(SoundID.MenuClose with { Pitch = -0.4f });
                return;
            }

            //安装走 VictorSurgery 帧 86 换装
            ActionThisFrame = true;
            VictorSurgery.BeginInstall(invIndex, boundSlot);
        }

        private void DoUnequip() {
            if (!hasEquippedItem) {
                return;
            }
            ActionThisFrame = true;
            VictorSurgery.BeginUninstall(boundSlot);
        }

        private void DoBuy(int type) {
            Player player = Main.LocalPlayer;
            long price = PriceOf(type);
            if (price <= 0 || player.BuyItem(price)) {
                player.QuickSpawnItem(player.GetSource_Misc("VictorClinicShop"), type, 1);
                SoundEngine.PlaySound(SoundID.Coins);
                SoundEngine.PlaySound(SoundID.Grab with { Pitch = 0.1f });
                RefreshItems(cyberPlayer);
                ActionThisFrame = true;
            }
            else {
                SoundEngine.PlaySound(SoundID.MenuClose with { Pitch = -0.5f });
            }
        }

        #endregion

        #region 布局

        private float ListTopOffset() {
            float y = HeaderHeight + CapacityBarHeight + PanelPadding;
            if (hasEquippedItem) {
                y += 16f + InstalledRowHeight + 10f;
            }
            return y;
        }

        private Rectangle InstalledRect() {
            float y = panelRect.Y + HeaderHeight + CapacityBarHeight + PanelPadding + 16f;
            return new Rectangle(panelRect.X + (int)PanelPadding, (int)y,
                panelRect.Width - (int)(PanelPadding * 2), (int)InstalledRowHeight);
        }

        private float MaxScroll() {
            float total = 0f;
            foreach (Row r in rows) {
                total += r.Kind == KindLabel ? LabelHeight : RowHeight;
            }
            float view = panelRect.Bottom - PanelPadding - (panelRect.Y + ListTopOffset());
            return Math.Max(0f, total - view);
        }

        private IEnumerable<(Row row, Rectangle rect)> VisibleRows() {
            float listTop = panelRect.Y + ListTopOffset();
            float listBottom = panelRect.Bottom - PanelPadding;
            float y = listTop - scrollOffset;
            foreach (Row r in rows) {
                float h = r.Kind == KindLabel ? LabelHeight : RowHeight;
                if (y + h >= listTop && y <= listBottom) {
                    Rectangle rect = new(panelRect.X + (int)PanelPadding, (int)y,
                        panelRect.Width - (int)(PanelPadding * 2), (int)(h - 4));
                    yield return (r, rect);
                }
                y += h;
            }
        }

        #endregion

        #region 绘制

        public void Draw(SpriteBatch sb, float parentAlpha, CyberwarePlayer cp) {
            if (openProgress < 0.01f || panelRect.Width < 2) {
                return;
            }
            cyberPlayer = cp;
            Texture2D px = CWRAsset.Placeholder_White?.Value;
            if (px == null) {
                return;
            }

            float alpha = parentAlpha * Math.Clamp(openProgress, 0f, 1f);

            CyberPanelRenderer.DrawShaderBackground(sb, alpha * 0.95f, panelRect, Microsoft.Xna.Framework.Vector2.Zero, 0f, mode: 1);
            VictorUIStyle.DrawCorners(sb, panelRect, CyberwareTheme.Accent * (alpha * 0.8f), 18, 2);

            if (openProgress < 0.6f) {
                return;
            }
            float contentAlpha = alpha * Math.Clamp((openProgress - 0.6f) / 0.4f, 0f, 1f);

            DrawHeader(sb, px, contentAlpha);
            DrawCapacityBar(sb, px, contentAlpha);
            if (hasEquippedItem) {
                DrawInstalled(sb, px, contentAlpha);
            }
            DrawList(sb, px, contentAlpha);

            if (hoveredCyberItem != null && !hoveredCyberItem.IsAir) {
                CyberTooltipRenderer.DrawTooltip(sb, hoveredCyberItem, new Microsoft.Xna.Framework.Vector2(Main.mouseX, Main.mouseY));
            }
        }

        private void DrawHeader(SpriteBatch sb, Texture2D px, float alpha) {
            Rectangle header = new(panelRect.X, panelRect.Y, panelRect.Width, (int)HeaderHeight);
            sb.Draw(px, header, new Rectangle(0, 0, 1, 1), CyberwareTheme.SectionBg * (alpha * 0.85f));
            VictorUIStyle.DrawHDivider(sb, panelRect.X + 6, panelRect.Right - 6, panelRect.Y + (int)HeaderHeight - 1, CyberwareTheme.Accent * (alpha * 0.6f));

            string title = VictorClinicUI.Instance?.GetSlotLabel(boundSlot) ?? "CYBERWARE";
            sb.Draw(px, new Rectangle(panelRect.X + (int)PanelPadding, panelRect.Y + 10, 4, 22), CyberwareTheme.Accent * (alpha * 0.9f));
            Utils.DrawBorderString(sb, title, new Microsoft.Xna.Framework.Vector2(panelRect.X + PanelPadding + 12, panelRect.Y + 8),
                CyberwareTheme.Accent * alpha, 0.72f * CyberwareTheme.FontScale);

            VictorUIStyle.DrawPrice(sb, new Microsoft.Xna.Framework.Vector2(panelRect.Right - PanelPadding, panelRect.Y + 32),
                VictorUIStyle.CountCoins(Main.LocalPlayer), alpha, 0.44f * CyberwareTheme.FontScale, rightAlign: true);
        }

        private void DrawCapacityBar(SpriteBatch sb, Texture2D px, float alpha) {
            float barY = panelRect.Y + HeaderHeight + 4;
            Rectangle bg = new(panelRect.X + (int)PanelPadding, (int)barY,
                panelRect.Width - (int)(PanelPadding * 2), (int)CapacityBarHeight);
            sb.Draw(px, bg, new Rectangle(0, 0, 1, 1), CyberwareTheme.SlotEmpty * (alpha * 0.9f));

            int used = cyberPlayer.UsedCapacity;
            int max = cyberPlayer.MaxCapacity;
            Utils.DrawBorderString(sb, $"CAPACITY {used}/{max}", new Microsoft.Xna.Framework.Vector2(bg.X + 6, bg.Y + 3),
                CyberwareTheme.TextNormal * alpha, 0.48f * CyberwareTheme.FontScale);

            float ratio = max > 0 ? (float)used / max : 0f;
            int inner = bg.Width - 6;
            Rectangle fill = new(bg.X + 3, bg.Y + 20, (int)(inner * ratio), 5);
            Color barColor = ratio > 0.85f ? CyberwareTheme.Accent : ratio > 0.6f ? CyberwareTheme.AccentGold : CyberwareTheme.AccentCyan;
            sb.Draw(px, fill, new Rectangle(0, 0, 1, 1), barColor * (alpha * 0.9f));
            sb.Draw(px, new Rectangle(fill.Right, fill.Y, inner - fill.Width, 5), new Rectangle(0, 0, 1, 1), CyberwareTheme.Border * (alpha * 0.4f));
        }

        private void DrawInstalled(SpriteBatch sb, Texture2D px, float alpha) {
            Item eq = cyberPlayer.EquippedCyberwares[boundSlot];
            if (eq == null || eq.IsAir) {
                return;
            }
            float headerY = panelRect.Y + HeaderHeight + CapacityBarHeight + PanelPadding;
            VictorUIStyle.DrawSectionHeader(sb, new Rectangle(panelRect.X + (int)PanelPadding, (int)headerY, panelRect.Width - (int)(PanelPadding * 2), 14),
                "INSTALLED", CyberwareTheme.AccentGold, alpha, 0.48f * CyberwareTheme.FontScale);

            Rectangle row = InstalledRect();
            bool hover = hoveredEntryKey == -2;
            VictorUIStyle.DrawCommandRow(sb, row, CyberwareTheme.AccentGold, hover ? hoverAnim : 0f, alpha, separator: false);

            VictorUIStyle.DrawItemIcon(sb, eq.type, new Microsoft.Xna.Framework.Vector2(row.X + 30, row.Center.Y), 40f, alpha);
            Utils.DrawBorderString(sb, VictorUIStyle.Trim(eq.Name, 16), new Microsoft.Xna.Framework.Vector2(row.X + 56, row.Y + 7),
                CyberwareTheme.TextBright * alpha, 0.52f * CyberwareTheme.FontScale);
            string hint = hover ? "> UNINSTALL <" : "CLICK TO UNINSTALL";
            Utils.DrawBorderString(sb, hint, new Microsoft.Xna.Framework.Vector2(row.X + 56, row.Y + 32),
                (hover ? CyberwareTheme.Accent : CyberwareTheme.TextDim) * (alpha * 0.8f), 0.46f * CyberwareTheme.FontScale);
        }

        private void DrawList(SpriteBatch sb, Texture2D px, float alpha) {
            long balance = VictorUIStyle.CountCoins(Main.LocalPlayer);
            bool anyOwned = false;
            bool anyShop = false;
            foreach (Row r in rows) {
                if (r.Kind == KindOwned) {
                    anyOwned = true;
                }
                else if (r.Kind == KindShop) {
                    anyShop = true;
                }
            }

            int index = 0;
            foreach ((Row row, Rectangle rect) in VisibleRows()) {
                if (row.Kind == KindLabel) {
                    bool isOwned = row.Value == LabelOwned;
                    string label = isOwned
                        ? Language.GetTextValue("Mods.CalamityOverhaul.UI.VictorClinicUI.LabelOwned")
                        : Language.GetTextValue("Mods.CalamityOverhaul.UI.VictorClinicUI.LabelForSale");
                    Color accent = isOwned ? CyberwareTheme.AccentCyan : CyberwareTheme.AccentGold;
                    VictorUIStyle.DrawSectionHeader(sb, rect, label, accent, alpha, 0.48f * CyberwareTheme.FontScale);

                    if (isOwned && !anyOwned) {
                        Utils.DrawBorderString(sb, Language.GetTextValue("Mods.CalamityOverhaul.UI.VictorClinicUI.EmptyOwned"), new Microsoft.Xna.Framework.Vector2(rect.X + 16, rect.Y + LabelHeight - 2),
                            CyberwareTheme.TextDim * (alpha * 0.5f), 0.44f * CyberwareTheme.FontScale);
                    }
                    else if (!isOwned && !anyShop) {
                        Utils.DrawBorderString(sb, Language.GetTextValue("Mods.CalamityOverhaul.UI.VictorClinicUI.EmptyShop"), new Microsoft.Xna.Framework.Vector2(rect.X + 16, rect.Y + LabelHeight - 2),
                            CyberwareTheme.TextDim * (alpha * 0.5f), 0.44f * CyberwareTheme.FontScale);
                    }
                    index++;
                    continue;
                }

                bool hover = hoveredEntryKey == row.Kind * 10000 + index;
                float hv = hover ? hoverAnim : 0f;
                if (row.Kind == KindOwned) {
                    DrawOwnedRow(sb, rect, row.Value, hv, alpha);
                }
                else {
                    DrawShopRow(sb, rect, row.Value, hv, alpha, balance);
                }
                index++;
            }

            DrawScrollBar(sb, px, alpha);
        }

        private void DrawOwnedRow(SpriteBatch sb, Rectangle rect, int invIndex, float hv, float alpha) {
            Item item = Main.LocalPlayer.inventory[invIndex];
            if (item == null || item.IsAir) {
                return;
            }
            bool canEquip = cyberPlayer.CanEquip(item, boundSlot);
            Color accent = canEquip ? CyberwareTheme.AccentCyan : CyberwareTheme.Accent;
            int slide = VictorUIStyle.DrawCommandRow(sb, rect, accent, hv, alpha);

            VictorUIStyle.DrawItemIcon(sb, item.type, new Microsoft.Xna.Framework.Vector2(rect.X + 28 + slide, rect.Center.Y), 38f, alpha);
            Utils.DrawBorderString(sb, VictorUIStyle.Trim(item.Name, 15), new Microsoft.Xna.Framework.Vector2(rect.X + 54 + slide, rect.Y + 7),
                (canEquip ? CyberwareTheme.TextBright : CyberwareTheme.TextDim) * alpha, 0.52f * CyberwareTheme.FontScale);

            string sub = item.ModItem is BaseCyberware bc ? $"CAP {bc.CapacityCost}" : "";
            Utils.DrawBorderString(sb, sub, new Microsoft.Xna.Framework.Vector2(rect.X + 54 + slide, rect.Y + 32),
                accent * (alpha * 0.7f), 0.44f * CyberwareTheme.FontScale);

            string act = !canEquip ? "OVER CAP" : hv > 0.5f ? "▶ INSTALL" : "INSTALL";
            float aScale = 0.46f * CyberwareTheme.FontScale;
            float aW = FontAssets.MouseText.Value.MeasureString(act).X * aScale;
            Utils.DrawBorderString(sb, act, new Microsoft.Xna.Framework.Vector2(rect.Right - aW - 10, rect.Y + (rect.Height - 16) / 2f),
                (canEquip ? accent : CyberwareTheme.Accent) * (alpha * (0.6f + 0.4f * hv)), aScale);
        }

        private void DrawShopRow(SpriteBatch sb, Rectangle rect, int type, float hv, float alpha, long balance) {
            long price = PriceOf(type);
            bool affordable = price <= 0 || balance >= price;
            Color accent = CyberwareTheme.AccentGold;
            int slide = VictorUIStyle.DrawCommandRow(sb, rect, accent, hv, alpha);

            VictorUIStyle.DrawItemIcon(sb, type, new Microsoft.Xna.Framework.Vector2(rect.X + 28 + slide, rect.Center.Y), 38f, alpha * (affordable ? 1f : 0.55f));
            Utils.DrawBorderString(sb, VictorUIStyle.Trim(Lang.GetItemNameValue(type), 15), new Microsoft.Xna.Framework.Vector2(rect.X + 54 + slide, rect.Y + 7),
                (affordable ? CyberwareTheme.TextBright : CyberwareTheme.TextDim) * alpha, 0.52f * CyberwareTheme.FontScale);

            VictorUIStyle.DrawPrice(sb, new Microsoft.Xna.Framework.Vector2(rect.X + 54 + slide, rect.Y + 32), price, alpha, 0.44f * CyberwareTheme.FontScale, rightAlign: false);

            string act = hv > 0.5f ? "▶ BUY" : "BUY";
            float aScale = 0.46f * CyberwareTheme.FontScale;
            float aW = FontAssets.MouseText.Value.MeasureString(act).X * aScale;
            Utils.DrawBorderString(sb, act, new Microsoft.Xna.Framework.Vector2(rect.Right - aW - 10, rect.Y + (rect.Height - 16) / 2f),
                (affordable ? accent : CyberwareTheme.Accent) * (alpha * (0.6f + 0.4f * hv)), aScale);
        }

        private void DrawScrollBar(SpriteBatch sb, Texture2D px, float alpha) {
            float maxScroll = MaxScroll();
            if (maxScroll <= 0f) {
                return;
            }
            float listTop = panelRect.Y + ListTopOffset();
            float view = panelRect.Bottom - PanelPadding - listTop;
            float total = view + maxScroll;
            float barH = Math.Max(24f, view * view / total);
            float barY = listTop + scrollOffset / maxScroll * (view - barH);
            sb.Draw(px, new Rectangle(panelRect.Right - (int)ScrollBarWidth - 2, (int)barY, (int)ScrollBarWidth, (int)barH),
                new Rectangle(0, 0, 1, 1), CyberwareTheme.Accent * (alpha * 0.4f));
        }

        #endregion
    }
}
