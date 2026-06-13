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
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Victors.UIs
{
    /// <summary>
    /// Victor 诊所的侧面板：针对当前选中槽位，整合展示「已安装 / 已拥有(可安装) / 在售(可购买)」三段。
    /// <br/>安装=手术，卸载归还背包，购买扣金币并进入背包，从而把"查看义体"和"商店"结合在同一视图
    /// </summary>
    internal class VictorClinicPanel
    {
        #region 常量

        private const float PanelWidth = 300f;
        private const float PanelPadding = 10f;
        private const float HeaderHeight = 44f;
        private const float CapacityBarHeight = 26f;
        private const float InstalledRowHeight = 50f;
        private const float LabelHeight = 20f;
        private const float RowHeight = 52f;
        private const float ScrollBarWidth = 5f;

        //行类型
        private const int KindLabel = 0;
        private const int KindOwned = 1;
        private const int KindShop = 2;
        //标签 id
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
            public readonly int Value = value;//标签id / 背包索引 / 物品类型
        }

        private int boundSlot = -1;
        private float openProgress;
        private Rectangle panelRect;
        private float scrollOffset;
        private int oldScrollWheelValue;
        private bool hasEquippedItem;

        private readonly List<Row> rows = [];
        private CyberwarePlayer cyberPlayer;
        private Item hoveredCyberItem;
        private int hoveredEntryKey = int.MinValue;//-2=卸载区；否则 kind*10000+index

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

            //已拥有（背包内兼容该槽位的义体）
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

            //在售（该槽位的义体，排除已安装/已拥有的型号）
            rows.Add(new Row(KindLabel, LabelShop));
            if (shopBySlot.TryGetValue(boundSlot, out List<int> shopTypes)) {
                foreach (int t in shopTypes) {
                    if (t == installedType || ownedTypes.Contains(t)) {
                        continue;
                    }
                    rows.Add(new Row(KindShop, t));
                }
            }
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

            float target = boundSlot >= 0 ? 1f : 0f;
            openProgress += (target - openProgress) * 0.18f;
            if (boundSlot < 0 && openProgress < 0.01f) {
                openProgress = 0f;
                return;
            }

            bool isLeftSlot = boundSlot is >= 0 and < 6;
            float eased = CWRUtils.EaseOutCubic(Math.Clamp(openProgress, 0f, 1f));
            float actualWidth = PanelWidth * eased;
            panelRect = isLeftSlot
                ? new Rectangle((int)(mainPanelRect.X - actualWidth - 6), mainPanelRect.Y, (int)actualWidth, mainPanelRect.Height)
                : new Rectangle(mainPanelRect.Right + 6, mainPanelRect.Y, (int)actualWidth, mainPanelRect.Height);

            if (openProgress > 0.5f) {
                UpdateInteraction();
            }
        }

        private void UpdateInteraction() {
            hoveredEntryKey = int.MinValue;
            hoveredCyberItem = null;
            Point mouse = new(Main.mouseX, Main.mouseY);
            if (!panelRect.Contains(mouse)) {
                return;
            }

            Main.LocalPlayer.mouseInterface = true;

            //滚轮
            int wheel = Mouse.GetState().ScrollWheelValue;
            int delta = wheel - oldScrollWheelValue;
            oldScrollWheelValue = wheel;
            if (delta != 0) {
                scrollOffset = Math.Clamp(scrollOffset - delta * 0.35f, 0f, MaxScroll());
                Terraria.GameInput.PlayerInput.LockVanillaMouseScroll("CalamityOverhaul/VictorClinic");
            }

            //已安装区（卸载）
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

            //列表区（已有=安装 / 在售=购买）
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

            Item old = cyberPlayer.Unequip(boundSlot);
            if (old != null && !old.IsAir) {
                player.QuickSpawnItem(player.GetSource_Misc("CyberwareUnequip"), old, old.stack);
            }
            cyberPlayer.Equip(item, boundSlot);
            item.TurnToAir();

            SoundEngine.PlaySound(SoundID.Item37);//手术完成音
            RefreshItems(cyberPlayer);
            ActionThisFrame = true;
        }

        private void DoUnequip() {
            Player player = Main.LocalPlayer;
            Item old = cyberPlayer.Unequip(boundSlot);
            if (old != null && !old.IsAir) {
                player.QuickSpawnItem(player.GetSource_Misc("CyberwareUnequip"), old, old.stack);
            }
            SoundEngine.PlaySound(SoundID.Item37);
            RefreshItems(cyberPlayer);
            ActionThisFrame = true;
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
                y += 14f + InstalledRowHeight + 8f;//已安装标签 + 行 + 间距
            }
            return y;
        }

        private Rectangle InstalledRect() {
            float y = panelRect.Y + HeaderHeight + CapacityBarHeight + PanelPadding + 14f;
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

        /// <summary>
        /// 计算列表区当前可见的行及其矩形（含滚动），update 与 draw 共用以保证命中与绘制一致
        /// </summary>
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

            CyberPanelRenderer.DrawShaderBackground(sb, alpha * 0.95f, panelRect, Vector2.Zero, 0f, mode: 1);
            DrawBorder(sb, px, alpha);

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
                CyberTooltipRenderer.DrawTooltip(sb, hoveredCyberItem, new Vector2(Main.mouseX, Main.mouseY));
            }
        }

        private void DrawBorder(SpriteBatch sb, Texture2D px, float alpha) {
            Color c = CyberwareTheme.Accent * (alpha * 0.6f);
            sb.Draw(px, new Rectangle(panelRect.X, panelRect.Y, panelRect.Width, 1), new Rectangle(0, 0, 1, 1), c);
            sb.Draw(px, new Rectangle(panelRect.X, panelRect.Bottom - 1, panelRect.Width, 1), new Rectangle(0, 0, 1, 1), c * 0.5f);
            sb.Draw(px, new Rectangle(panelRect.X, panelRect.Y, 1, panelRect.Height), new Rectangle(0, 0, 1, 1), c * 0.7f);
            sb.Draw(px, new Rectangle(panelRect.Right - 1, panelRect.Y, 1, panelRect.Height), new Rectangle(0, 0, 1, 1), c * 0.7f);
        }

        private void DrawHeader(SpriteBatch sb, Texture2D px, float alpha) {
            Rectangle header = new(panelRect.X, panelRect.Y, panelRect.Width, (int)HeaderHeight);
            sb.Draw(px, header, new Rectangle(0, 0, 1, 1), CyberwareTheme.SectionBg * (alpha * 0.8f));
            sb.Draw(px, new Rectangle(panelRect.X + 4, panelRect.Y + (int)HeaderHeight - 1, panelRect.Width - 8, 1),
                new Rectangle(0, 0, 1, 1), CyberwareTheme.Accent * (alpha * 0.5f));

            string title = VictorClinicUI.Instance?.GetSlotLabel(boundSlot) ?? "CYBERWARE";
            Utils.DrawBorderString(sb, title, new Vector2(panelRect.X + PanelPadding, panelRect.Y + 6),
                CyberwareTheme.Accent * alpha, 0.56f * CyberwareTheme.FontScale);

            //玩家金币余额
            DrawPrice(sb, px, new Vector2(panelRect.Right - PanelPadding, panelRect.Y + 26), CountCoins(Main.LocalPlayer), alpha, rightAlign: true);
        }

        private void DrawCapacityBar(SpriteBatch sb, Texture2D px, float alpha) {
            float barY = panelRect.Y + HeaderHeight + 2;
            Rectangle bg = new(panelRect.X + (int)PanelPadding, (int)barY,
                panelRect.Width - (int)(PanelPadding * 2), (int)CapacityBarHeight);
            sb.Draw(px, bg, new Rectangle(0, 0, 1, 1), CyberwareTheme.SlotEmpty * alpha);

            int used = cyberPlayer.UsedCapacity;
            int max = cyberPlayer.MaxCapacity;
            Utils.DrawBorderString(sb, $"CAPACITY {used}/{max}", new Vector2(bg.X + 4, bg.Y + 2),
                CyberwareTheme.TextNormal * alpha, 0.42f * CyberwareTheme.FontScale);

            float ratio = max > 0 ? (float)used / max : 0f;
            int inner = bg.Width - 4;
            Rectangle fill = new(bg.X + 2, bg.Y + 16, (int)(inner * ratio), 4);
            Color barColor = ratio > 0.85f ? CyberwareTheme.Accent : ratio > 0.6f ? CyberwareTheme.AccentGold : CyberwareTheme.AccentCyan;
            sb.Draw(px, fill, new Rectangle(0, 0, 1, 1), barColor * (alpha * 0.85f));
            sb.Draw(px, new Rectangle(fill.Right, fill.Y, inner - fill.Width, 4), new Rectangle(0, 0, 1, 1), CyberwareTheme.Border * (alpha * 0.4f));
        }

        private void DrawInstalled(SpriteBatch sb, Texture2D px, float alpha) {
            Item eq = cyberPlayer.EquippedCyberwares[boundSlot];
            if (eq == null || eq.IsAir) {
                return;
            }
            Utils.DrawBorderString(sb, "[ INSTALLED ]",
                new Vector2(panelRect.X + PanelPadding, panelRect.Y + HeaderHeight + CapacityBarHeight + PanelPadding),
                CyberwareTheme.AccentGold * (alpha * 0.7f), 0.40f * CyberwareTheme.FontScale);

            Rectangle row = InstalledRect();
            bool hover = hoveredEntryKey == -2;
            Color bg = hover ? Color.Lerp(CyberwareTheme.SlotEmpty, CyberwareTheme.Accent, 0.15f) : CyberwareTheme.SlotEmpty;
            sb.Draw(px, row, new Rectangle(0, 0, 1, 1), bg * (alpha * 0.7f));
            Color border = hover ? CyberwareTheme.Accent : CyberwareTheme.AccentGold;
            sb.Draw(px, new Rectangle(row.X, row.Y, row.Width, 1), new Rectangle(0, 0, 1, 1), border * (alpha * 0.6f));
            sb.Draw(px, new Rectangle(row.X, row.Bottom - 1, row.Width, 1), new Rectangle(0, 0, 1, 1), border * (alpha * 0.3f));

            DrawItemIcon(sb, eq, new Vector2(row.X + 4, row.Y + 4), alpha);
            string name = Trim(eq.Name, 18);
            Utils.DrawBorderString(sb, name, new Vector2(row.X + 46, row.Y + 5), CyberwareTheme.TextBright * alpha, 0.46f * CyberwareTheme.FontScale);
            string hint = hover ? "> UNINSTALL <" : "CLICK TO UNINSTALL";
            Utils.DrawBorderString(sb, hint, new Vector2(row.X + 46, row.Y + 28),
                (hover ? CyberwareTheme.Accent : CyberwareTheme.TextDim) * (alpha * 0.7f), 0.36f * CyberwareTheme.FontScale);
        }

        private void DrawList(SpriteBatch sb, Texture2D px, float alpha) {
            long balance = CountCoins(Main.LocalPlayer);
            int index = 0;
            foreach ((Row row, Rectangle rect) in VisibleRows()) {
                if (row.Kind == KindLabel) {
                    string label = row.Value == LabelOwned ? "OWNED" : "FOR SALE";
                    Color lc = (row.Value == LabelOwned ? CyberwareTheme.AccentCyan : CyberwareTheme.AccentGold) * (alpha * 0.7f);
                    Utils.DrawBorderString(sb, label, new Vector2(rect.X, rect.Y + 3), lc, 0.42f * CyberwareTheme.FontScale);
                    sb.Draw(px, new Rectangle(rect.X + 70, rect.Y + (int)(LabelHeight / 2), rect.Width - 70, 1),
                        new Rectangle(0, 0, 1, 1), lc * 0.4f);
                    index++;
                    continue;
                }

                bool hover = hoveredEntryKey == row.Kind * 10000 + index;
                if (row.Kind == KindOwned) {
                    DrawOwnedRow(sb, px, rect, row.Value, hover, alpha);
                }
                else {
                    DrawShopRow(sb, px, rect, row.Value, hover, alpha, balance);
                }
                index++;
            }

            DrawScrollBar(sb, px, alpha);
        }

        private void DrawOwnedRow(SpriteBatch sb, Texture2D px, Rectangle rect, int invIndex, bool hover, float alpha) {
            Item item = Main.LocalPlayer.inventory[invIndex];
            if (item == null || item.IsAir) {
                return;
            }
            bool canEquip = cyberPlayer.CanEquip(item, boundSlot);
            Color accent = canEquip ? CyberwareTheme.AccentCyan : CyberwareTheme.Accent;

            sb.Draw(px, rect, new Rectangle(0, 0, 1, 1),
                (hover ? Color.Lerp(CyberwareTheme.SlotInnerBg, accent, 0.14f) : CyberwareTheme.SlotInnerBg) * (alpha * 0.72f));
            if (hover) {
                sb.Draw(px, new Rectangle(rect.X, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), accent * alpha);
            }

            DrawItemIcon(sb, item, new Vector2(rect.X + 5, rect.Y + 5), alpha);
            Utils.DrawBorderString(sb, Trim(item.Name, 16), new Vector2(rect.X + 46, rect.Y + 4),
                (canEquip ? CyberwareTheme.TextBright : CyberwareTheme.TextDim) * alpha, 0.44f * CyberwareTheme.FontScale);

            string sub = item.ModItem is BaseCyberware bc ? $"CAP {bc.CapacityCost}" : "";
            Utils.DrawBorderString(sb, sub, new Vector2(rect.X + 46, rect.Y + 28), accent * (alpha * 0.6f), 0.36f * CyberwareTheme.FontScale);
            string act = !canEquip ? "OVER CAP" : hover ? "> INSTALL <" : "INSTALL";
            Utils.DrawBorderString(sb, act, new Vector2(rect.Right - 96, rect.Y + 28),
                (canEquip ? accent : CyberwareTheme.Accent) * (alpha * 0.7f), 0.36f * CyberwareTheme.FontScale);
        }

        private void DrawShopRow(SpriteBatch sb, Texture2D px, Rectangle rect, int type, bool hover, float alpha, long balance) {
            long price = PriceOf(type);
            bool affordable = price <= 0 || balance >= price;
            Color accent = CyberwareTheme.AccentGold;

            sb.Draw(px, rect, new Rectangle(0, 0, 1, 1),
                (hover ? Color.Lerp(CyberwareTheme.SlotInnerBg, accent, 0.14f) : CyberwareTheme.SlotInnerBg) * (alpha * 0.6f));
            if (hover) {
                sb.Draw(px, new Rectangle(rect.X, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), accent * alpha);
            }

            DrawItemIconByType(sb, type, new Vector2(rect.X + 5, rect.Y + 5), alpha * (affordable ? 1f : 0.5f));
            Utils.DrawBorderString(sb, Trim(Lang.GetItemNameValue(type), 16), new Vector2(rect.X + 46, rect.Y + 4),
                (affordable ? CyberwareTheme.TextBright : CyberwareTheme.TextDim) * alpha, 0.44f * CyberwareTheme.FontScale);

            DrawPrice(sb, px, new Vector2(rect.X + 46, rect.Y + 28), price, alpha, rightAlign: false);
            string act = hover ? "> BUY <" : "BUY";
            Utils.DrawBorderString(sb, act, new Vector2(rect.Right - 70, rect.Y + 28),
                (affordable ? accent : CyberwareTheme.Accent) * (alpha * 0.7f), 0.36f * CyberwareTheme.FontScale);
        }

        private void DrawScrollBar(SpriteBatch sb, Texture2D px, float alpha) {
            float maxScroll = MaxScroll();
            if (maxScroll <= 0f) {
                return;
            }
            float listTop = panelRect.Y + ListTopOffset();
            float view = panelRect.Bottom - PanelPadding - listTop;
            float total = view + maxScroll;
            float barH = Math.Max(20f, view * view / total);
            float barY = listTop + scrollOffset / maxScroll * (view - barH);
            sb.Draw(px, new Rectangle(panelRect.Right - (int)ScrollBarWidth - 2, (int)barY, (int)ScrollBarWidth, (int)barH),
                new Rectangle(0, 0, 1, 1), CyberwareTheme.Accent * (alpha * 0.35f));
        }

        #endregion

        #region 工具

        private static string Trim(string s, int max) {
            s ??= "???";
            return s.Length > max ? s[..(max - 1)] + "…" : s;
        }

        private static void DrawItemIcon(SpriteBatch sb, Item item, Vector2 position, float alpha) {
            if (item == null || item.IsAir) {
                return;
            }
            DrawItemIconByType(sb, item.type, position, alpha);
        }

        private static void DrawItemIconByType(SpriteBatch sb, int type, Vector2 position, float alpha) {
            Main.instance.LoadItem(type);
            Texture2D tex = TextureAssets.Item[type]?.Value;
            if (tex == null) {
                return;
            }
            Rectangle frame = Main.itemAnimations[type] != null ? Main.itemAnimations[type].GetFrame(tex) : tex.Bounds;
            float maxDim = Math.Max(frame.Width, frame.Height);
            float scale = maxDim > 38 ? 38f / maxDim : 1f;
            sb.Draw(tex, position + new Vector2(19, 19), frame, Color.White * alpha, 0f, frame.Size() / 2f, scale, SpriteEffects.None, 0f);
        }

        private void DrawPrice(SpriteBatch sb, Texture2D px, Vector2 pos, long value, float alpha, bool rightAlign) {
            float scale = 0.38f * CyberwareTheme.FontScale;
            if (value <= 0) {
                string free = "FREE";
                Vector2 fs = FontAssets.MouseText.Value.MeasureString(free) * scale;
                Utils.DrawBorderString(sb, free, new Vector2(rightAlign ? pos.X - fs.X : pos.X, pos.Y), CyberwareTheme.AccentGold * alpha, scale);
                return;
            }

            int[] amounts = [
                (int)(value / 1000000L),
                (int)(value / 10000L % 100L),
                (int)(value / 100L % 100L),
                (int)(value % 100L),
            ];
            int[] coinItems = [ItemID.PlatinumCoin, ItemID.GoldCoin, ItemID.SilverCoin, ItemID.CopperCoin];

            float totalW = 0f;
            for (int i = 0; i < 4; i++) {
                if (amounts[i] <= 0) {
                    continue;
                }
                Main.instance.LoadItem(coinItems[i]);
                Texture2D coin = TextureAssets.Item[coinItems[i]].Value;
                Vector2 ns = FontAssets.MouseText.Value.MeasureString(amounts[i].ToString()) * scale;
                totalW += ns.X + 2f + coin.Width * 0.6f + 7f;
            }

            float x = rightAlign ? pos.X - totalW : pos.X;
            for (int i = 0; i < 4; i++) {
                if (amounts[i] <= 0) {
                    continue;
                }
                string num = amounts[i].ToString();
                Vector2 ns = FontAssets.MouseText.Value.MeasureString(num) * scale;
                Utils.DrawBorderString(sb, num, new Vector2(x, pos.Y), Color.White * alpha, scale);
                x += ns.X + 2f;
                Main.instance.LoadItem(coinItems[i]);
                Texture2D coin = TextureAssets.Item[coinItems[i]].Value;
                sb.Draw(coin, new Vector2(x, pos.Y - 1f), null, Color.White * alpha, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
                x += coin.Width * 0.6f + 7f;
            }
        }

        private static long CountCoins(Player p) {
            long total = 0;
            void Add(Item[] inv) {
                if (inv == null) {
                    return;
                }
                foreach (Item it in inv) {
                    if (it == null || it.IsAir) {
                        continue;
                    }
                    switch (it.type) {
                        case ItemID.CopperCoin: total += it.stack; break;
                        case ItemID.SilverCoin: total += it.stack * 100L; break;
                        case ItemID.GoldCoin: total += it.stack * 10000L; break;
                        case ItemID.PlatinumCoin: total += it.stack * 1000000L; break;
                    }
                }
            }
            Add(p.inventory);
            Add(p.bank?.item);
            Add(p.bank2?.item);
            Add(p.bank3?.item);
            Add(p.bank4?.item);
            return total;
        }

        #endregion
    }
}
