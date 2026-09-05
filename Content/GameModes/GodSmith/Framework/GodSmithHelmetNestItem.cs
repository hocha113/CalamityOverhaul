using CalamityOverhaul.Content.GameModes.GodSmith.Core;
using CalamityOverhaul.Content.GameModes.UI;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.UI;
using Terraria.UI.Chat;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Framework
{
    /// <summary>
    /// 头盔镶嵌数据（逐实例 GlobalItem），只挂在接管方案认领过的头盔上。<br/>
    /// <see cref="Chain"/> 记录镶嵌进来的头盔物品 ID，从直接镶嵌的那顶起逐级向内；
    /// 穿好整套后 <see cref="GodSmithArmorPlayer"/> 沿链把各级头盔所属方案的套装奖励一并派发。
    /// 链在配方成品回调里写入（见 Armors 侧的镶嵌配方系统），随 SaveData/NetSend 持久与同步；
    /// 数组视为不可变，改动一律整数组替换，克隆时共享引用无害。<br/>
    /// tooltip 追加镶嵌行，悬停时在 tooltip 旁画一块面板列出链上头盔与各自的套装奖励；
    /// 未镶嵌时按 <see cref="Slots"/> 登记的档位关系提示玩家能镶什么、能镶进哪里
    /// </summary>
    internal class GodSmithHelmetNestItem : GlobalItem
    {
        public override bool InstancePerEntity => true;

        public override bool AppliesToEntity(Item entity, bool lateInstantiation)
            => lateInstantiation && GodSmithArmorScheme.SchemeByHead.ContainsKey(entity.type);

        /// <summary>镶嵌链：从直接镶嵌的头盔起逐级向内；空数组 = 未镶嵌。视为不可变，克隆共享引用即可</summary>
        [CloneByReference]
        internal int[] Chain = [];

        //==================== 档位登记（镶嵌配方系统注册配方时填写，tooltip 提示读取） ====================

        /// <summary>
        /// 一顶头盔的镶嵌档位。每组是同一矿石的全部头盔 ID，首项为代表：
        /// <see cref="Lower"/> = 合成这顶头盔时可一并放入的低一档头盔组；<see cref="Upper"/> = 这顶头盔可镶进的高一档成品头盔组
        /// </summary>
        internal sealed class NestSlot
        {
            public readonly List<int[]> Lower = [];
            public readonly List<int[]> Upper = [];
        }

        /// <summary>头盔 ID → 镶嵌档位；只有在档位表里的矿石头盔才有条目</summary>
        internal static Dictionary<int, NestSlot> Slots { get; } = [];

        /// <summary>取（或建）该头盔的档位条目</summary>
        internal static NestSlot SlotFor(int headType) {
            if (!Slots.TryGetValue(headType, out NestSlot slot)) {
                slot = new NestSlot();
                Slots[headType] = slot;
            }
            return slot;
        }

        /// <summary>一组同矿头盔的显示名：多顶时与配方组同款「任意 X」，单顶直接用物品名</summary>
        internal static string GroupName(int[] heads)
            => heads.Length > 1
                ? $"{Language.GetTextValue("LegacyMisc.37")} {Lang.GetItemNameValue(heads[0])}"
                : Lang.GetItemNameValue(heads[0]);

        private static string JoinGroups(List<int[]> groups) {
            StringBuilder names = new();
            for (int i = 0; i < groups.Count; i++) {
                if (i > 0) {
                    names.Append(GameModeText.GodSmithNestOr.Value);
                }
                names.Append(GroupName(groups[i]));
            }
            return names.ToString();
        }

        /// <summary>取物品的镶嵌链；未镶嵌或非头盔返回 false</summary>
        public static bool TryGetChain(Item item, out int[] chain) {
            chain = null;
            if (item == null || item.IsAir || !item.TryGetGlobalItem(out GodSmithHelmetNestItem data) || data.Chain.Length == 0) {
                return false;
            }
            chain = data.Chain;
            return true;
        }

        /// <summary>把 inner 头盔（连同它自己的链）镶进 outer 头盔</summary>
        public static void Embed(Item outer, Item inner) {
            if (outer == null || inner == null || !outer.TryGetGlobalItem(out GodSmithHelmetNestItem data)) {
                return;
            }
            int[] innerChain = TryGetChain(inner, out int[] found) ? found : [];
            int[] chain = new int[innerChain.Length + 1];
            chain[0] = inner.type;
            innerChain.CopyTo(chain, 1);
            data.Chain = chain;
        }

        //==================== 数据往返（存物品名防 ID 漂移；网络走 int） ====================

        public override void SaveData(Item item, TagCompound tag) {
            if (Chain.Length == 0) {
                return;
            }
            List<string> names = new(Chain.Length);
            for (int i = 0; i < Chain.Length; i++) {
                names.Add(ItemID.Search.GetName(Chain[i]));
            }
            tag["GsNest"] = names;
        }

        public override void LoadData(Item item, TagCompound tag) {
            Chain = [];
            if (!tag.ContainsKey("GsNest")) {
                return;
            }
            List<int> types = [];
            foreach (string name in tag.GetList<string>("GsNest")) {
                if (ItemID.Search.TryGetId(name, out int type)) {
                    types.Add(type);
                }
            }
            Chain = [.. types];
        }

        public override void NetSend(Item item, BinaryWriter writer) {
            writer.Write((byte)Chain.Length);
            for (int i = 0; i < Chain.Length; i++) {
                writer.Write(Chain[i]);
            }
        }

        public override void NetReceive(Item item, BinaryReader reader) {
            int count = reader.ReadByte();
            int[] chain = new int[count];
            for (int i = 0; i < count; i++) {
                chain[i] = reader.ReadInt32();
            }
            Chain = chain;
        }

        //==================== tooltip ====================

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips) {
            if (Chain.Length > 0) {
                GodSmithTooltip.EnsureTitle(tooltips);
                StringBuilder names = new();
                for (int i = 0; i < Chain.Length; i++) {
                    if (i > 0) {
                        names.Append(" > ");
                    }
                    names.Append(Lang.GetItemNameValue(Chain[i]));
                }
                tooltips.Add(new TooltipLine(CWRMod.Instance, "CWR_GodSmithNestChain",
                    GameModeText.GodSmithNestChain.Format(names.ToString())) { OverrideColor = GodSmithTooltip.BodyGold });
            }

            //镶嵌配方只在神匠模式下存在，提示也只在模式开启时给
            if (!GameModeSystem.GodSmithActive || !Slots.TryGetValue(item.type, out NestSlot slot)) {
                return;
            }
            GodSmithTooltip.EnsureTitle(tooltips);
            //镶嵌发生在合成这顶头盔的那一刻，已镶嵌的成品不再提示能镶什么
            if (Chain.Length == 0 && slot.Lower.Count > 0) {
                GodSmithTooltip.AddBodyLines(tooltips, "CWR_GodSmithNestReceive",
                    GameModeText.GodSmithNestReceiveHint.Format(JoinGroups(slot.Lower)));
            }
            if (slot.Upper.Count > 0) {
                GodSmithTooltip.AddBodyLines(tooltips, "CWR_GodSmithNestGive",
                    GameModeText.GodSmithNestGiveHint.Format(JoinGroups(slot.Upper)));
            }
        }

        //==================== 悬停面板 ====================

        private const int PanelWidth = 300;
        private const int PanelPad = 12;
        private const int IconBox = 30;
        private const int RowGap = 8;

        public override void PostDrawTooltip(Item item, ReadOnlyCollection<DrawableTooltipLine> lines) {
            if (Chain.Length == 0 || lines.Count == 0 || !GameModeSystem.GodSmithActive) {
                return;
            }
            DynamicSpriteFont font = FontAssets.MouseText?.Value;
            if (font == null) {
                return;
            }

            //tooltip 外框：各行位置与宽度求左、上、右三边
            int left = int.MaxValue, top = int.MaxValue, right = int.MinValue;
            foreach (DrawableTooltipLine line in lines) {
                Vector2 size = ChatManager.GetStringSize(line.Font ?? font, line.Text, line.BaseScale);
                left = Math.Min(left, line.X);
                top = Math.Min(top, line.Y);
                right = Math.Max(right, line.X + (int)size.X);
            }

            //先排版量高，再决定落在 tooltip 右侧还是左侧
            List<(int type, GodSmithArmorScheme scheme, List<string> desc)> rows = new(Chain.Length);
            float textWidth = PanelWidth - PanelPad * 2 - IconBox - 8;
            int height = PanelPad + 26;
            for (int i = 0; i < Chain.Length; i++) {
                GodSmithArmorScheme.SchemeByHead.TryGetValue(Chain[i], out GodSmithArmorScheme scheme);
                List<string> desc = scheme?.SetBonusLine != null
                    ? VaultUtils.WrapText(scheme.SetBonusLine.Value, font, textWidth, 0.8f)
                    : [];
                rows.Add((Chain[i], scheme, desc));
                int rowH = 22 + desc.Count * 20;
                height += Math.Max(rowH, IconBox + 4) + RowGap;
            }
            bool worn = IsWornBy(Main.LocalPlayer, item);
            string footer = worn ? GameModeText.GodSmithNestActive.Value : GameModeText.GodSmithNestHint.Value;
            List<string> footerLines = VaultUtils.WrapText(footer, font, PanelWidth - PanelPad * 2, 0.8f);
            height += footerLines.Count * 20 + PanelPad;

            int x = right + 22;
            if (x + PanelWidth > GameModeTheme.UIScreenW - 6) {
                x = left - 22 - PanelWidth;
            }
            if (x < 6) {
                x = 6;
            }
            int y = top - 8;
            if (y + height > GameModeTheme.UIScreenH - 6) {
                y = (int)GameModeTheme.UIScreenH - 6 - height;
            }
            if (y < 6) {
                y = 6;
            }

            SpriteBatch sb = Main.spriteBatch;
            Utils.DrawInvBG(sb, new Rectangle(x, y, PanelWidth, height), new Color(23, 25, 81, 255) * 0.925f);

            float cy = y + PanelPad;
            Utils.DrawBorderString(sb, GameModeText.GodSmithNestTitle.Value, new Vector2(x + PanelPad, cy), GodSmithTooltip.TitleGold, 0.9f);
            cy += 26;

            foreach ((int type, GodSmithArmorScheme scheme, List<string> desc) in rows) {
                //左侧物品图标，右侧名字与套装奖励
                if (ContentSamples.ItemsByType.TryGetValue(type, out Item sample) && sample != null) {
                    ItemSlot.DrawItemIcon(sample, ItemSlot.Context.InWorld, sb,
                        new Vector2(x + PanelPad + IconBox * 0.5f, cy + IconBox * 0.5f), 1f, IconBox - 4, Color.White);
                }
                float tx = x + PanelPad + IconBox + 8;
                Utils.DrawBorderString(sb, Lang.GetItemNameValue(type), new Vector2(tx, cy), Color.White, 0.85f);
                float ty = cy + 22;
                foreach (string piece in desc) {
                    Utils.DrawBorderString(sb, piece, new Vector2(tx, ty), GodSmithTooltip.BodyGold, 0.8f);
                    ty += 20;
                }
                int rowH = 22 + desc.Count * 20;
                cy += Math.Max(rowH, IconBox + 4) + RowGap;
            }

            Color footerColor = worn ? GameModeTheme.GodSmithEmber : GameModeTheme.TextBody;
            foreach (string piece in footerLines) {
                Utils.DrawBorderString(sb, piece, new Vector2(x + PanelPad, cy), footerColor, 0.8f);
                cy += 20;
            }
        }

        /// <summary>悬停的头盔是否就是本地玩家戴着且整套命中的那顶（同类型同链即视为同一顶）</summary>
        private bool IsWornBy(Player player, Item hovered) {
            if (player == null || hovered == null) {
                return false;
            }
            Item head = player.armor[0];
            if (head.IsAir || head.type != hovered.type || !TryGetChain(head, out int[] wornChain)
                || wornChain.Length != Chain.Length) {
                return false;
            }
            for (int i = 0; i < Chain.Length; i++) {
                if (wornChain[i] != Chain[i]) {
                    return false;
                }
            }
            return GodSmithArmorScheme.SchemeByHead.TryGetValue(hovered.type, out GodSmithArmorScheme scheme)
                && scheme.Matches(player);
        }
    }
}
