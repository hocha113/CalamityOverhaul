using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions.Deeds;
using CalamityOverhaul.Content.Scenarios.Himayo.Gifts;
using System;
using System.Collections.Generic;
using System.Text;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI
{
    /// <summary>一枚铭的来路</summary>
    internal enum OniMeiCodexSource : byte
    {
        /// <summary>出厂即在刀上</summary>
        Factory,
        /// <summary>绯真夜的首领赠礼</summary>
        Gift,
        /// <summary>手持鬼切自证的刀縁</summary>
        Deed,
        /// <summary>名册里有、却无路可循（设计漏配时的兜底）</summary>
        Unknown,
    }

    /// <summary>
    /// 图鉴里的一行：一枚铭在本机玩家视角下的全部可显状态。<br/>
    /// 全部由现有 API 推导，不落任何存档字段
    /// </summary>
    internal readonly struct OniMeiCodexRow
    {
        internal readonly OniMeiDefinition Definition;
        /// <summary>已得则展全文，未得只给线索</summary>
        internal readonly bool Owned;
        internal readonly OniMeiCodexSource Source;
        /// <summary>此刻正凿在手中那把刀上</summary>
        internal readonly bool Engraved;
        /// <summary>刀縁累计与需求；非刀縁铭为 0</summary>
        internal readonly int DeedValue;
        internal readonly int DeedNeed;
        /// <summary>Count 型刀縁才有可读的分数进度</summary>
        internal readonly bool DeedCountable;

        internal OniMeiCodexRow(OniMeiDefinition definition, bool owned, OniMeiCodexSource source,
            bool engraved, int deedValue, int deedNeed, bool deedCountable) {
            Definition = definition;
            Owned = owned;
            Source = source;
            Engraved = engraved;
            DeedValue = deedValue;
            DeedNeed = deedNeed;
            DeedCountable = deedCountable;
        }

        internal string Key => Definition?.Key ?? "";
        internal OniMeiSlotKind Slot => Definition?.SlotKind ?? OniMeiSlotKind.Nakago;
        internal bool Gold => Definition?.IsGoldTier ?? false;
        internal string Name => Definition?.DisplayName?.Value ?? Key;

        /// <summary>0~1 刀縁进度；不可计数或已得一律满</summary>
        internal float DeedRatio => Owned || !DeedCountable || DeedNeed <= 0
            ? 1f
            : MathHelper.Clamp(DeedValue / (float)DeedNeed, 0f, 1f);
    }

    /// <summary>某一槽或全册的收集度</summary>
    internal readonly struct OniMeiCodexTally(int owned, int total)
    {
        internal readonly int Owned = owned;
        internal readonly int Total = total;

        internal float Ratio => Total <= 0 ? 0f : MathHelper.Clamp(Owned / (float)Total, 0f, 1f);
    }

    /// <summary>
    /// 铭谱的数据面：把名册、所持、刀縁进度与赠礼来路合成图鉴行。<br/>
    /// 与改铭台扇骨的口径**有意不同**，扇骨会藏掉"无縁又未持"的赠礼铭（挂上去也点不动），
    /// 而图鉴正是要让人看见自己还差什么，故三十六枚一枚不落
    /// </summary>
    internal static class OniMeiCodexData
    {
        /// <summary>按槽位或全册取行；slot 为 null 即全册，顺序照名册 SortOrder</summary>
        internal static void Build(Player player, OniMeiSlotKind? slot, List<OniMeiCodexRow> into) {
            into.Clear();
            if (into.Capacity < OniMeiRegistry.All.Count) {
                into.Capacity = OniMeiRegistry.All.Count;
            }
            OniMeiStore store = OniMeiRegistry.DisplayStore;
            foreach (OniMeiDefinition definition in OniMeiRegistry.All) {
                if (slot.HasValue && definition.SlotKind != slot.Value) {
                    continue;
                }
                into.Add(Resolve(player, definition, store));
            }
        }

        private static OniMeiCodexRow Resolve(Player player, OniMeiDefinition definition,
            OniMeiStore store) {
            bool owned = OniMeiOwned.Owns(player, definition.Key);
            bool engraved = store != null
                && OniMeiRegistry.GetEngraved(store, definition.SlotKind)?.Key == definition.Key;

            int value = 0;
            int need = 0;
            bool countable = false;
            OniMeiCodexSource source;
            if (OniMeiDeedRegistry.TryGetByMei(definition.Key, out OniMeiDeed deed)) {
                source = OniMeiCodexSource.Deed;
                need = Math.Max(1, deed.NeedCount);
                countable = deed.ProgressKind == OniMeiDeedProgressKind.Count;
                if (player != null && player.TryGetModPlayer(out OnikiriPlayer okp)) {
                    value = Math.Clamp(okp.Deeds.Get(deed.Key), 0, need);
                }
            }
            else if (HimayoGiftCatalog.TryGet(definition.Key, out _)) {
                source = OniMeiCodexSource.Gift;
            }
            else if (OniMeiOwned.IsDefaultOwned(definition.Key)) {
                source = OniMeiCodexSource.Factory;
            }
            else {
                source = OniMeiCodexSource.Unknown;
            }

            //出厂白名单与赠礼有重叠（同一枚铭既随刀又能再赠一份拓本），
            //既然已在手就没有"还要去拿"这回事，来路一律读作出厂
            if (source == OniMeiCodexSource.Gift && OniMeiOwned.IsDefaultOwned(definition.Key)) {
                source = OniMeiCodexSource.Factory;
            }

            return new OniMeiCodexRow(definition, owned, source, engraved, value, need, countable);
        }

        /// <summary>全册收集度</summary>
        internal static OniMeiCodexTally Tally(Player player) {
            int owned = 0;
            foreach (OniMeiDefinition definition in OniMeiRegistry.All) {
                if (OniMeiOwned.Owns(player, definition.Key)) {
                    owned++;
                }
            }
            return new OniMeiCodexTally(owned, OniMeiRegistry.All.Count);
        }

        /// <summary>某槽收集度</summary>
        internal static OniMeiCodexTally Tally(Player player, OniMeiSlotKind slot) {
            int owned = 0;
            int total = 0;
            foreach (OniMeiDefinition definition in OniMeiRegistry.All) {
                if (definition.SlotKind != slot) {
                    continue;
                }
                total++;
                if (OniMeiOwned.Owns(player, definition.Key)) {
                    owned++;
                }
            }
            return new OniMeiCodexTally(owned, total);
        }

        /// <summary>来路一句：已得写"从哪来"，未得写"往哪取"</summary>
        internal static string SourceLine(in OniMeiCodexRow row) => row.Source switch {
            OniMeiCodexSource.Factory => OniMeiCodexUI.SourceFactory?.Value ?? "",
            OniMeiCodexSource.Gift => GiftLine(row.Key),
            OniMeiCodexSource.Deed => OniMeiCodexUI.SourceDeed?.Value ?? "",
            _ => OniMeiCodexUI.SourceUnknown?.Value ?? "",
        };

        /// <summary>赠礼来路：报出该赠礼系于哪一位首领</summary>
        private static string GiftLine(string key) {
            if (!HimayoGiftCatalog.TryGet(key, out HimayoGiftEntry entry)) {
                return OniMeiCodexUI.SourceGiftUnknown?.Value ?? "";
            }
            string bosses = DescribeBosses(entry);
            return string.IsNullOrEmpty(bosses)
                ? OniMeiCodexUI.SourceGiftUnknown?.Value ?? ""
                : OniMeiCodexUI.SourceGiftFormat?.Format(bosses) ?? bosses;
        }

        /// <summary>至多报两位，多于两位只说前两位，牌面写不下，也没必要</summary>
        private static string DescribeBosses(HimayoGiftEntry entry) {
            int[] ids = entry.TargetBossIds;
            if (ids == null || ids.Length == 0) {
                return "";
            }
            StringBuilder builder = new();
            int listed = 0;
            for (int i = 0; i < ids.Length && listed < 2; i++) {
                if (ids[i] <= 0) {
                    continue;
                }
                string name = Lang.GetNPCNameValue(ids[i]);
                if (string.IsNullOrWhiteSpace(name)) {
                    continue;
                }
                if (listed > 0) {
                    builder.Append(OniMeiCodexUI.SourceGiftJoin?.Value ?? " / ");
                }
                builder.Append(name);
                listed++;
            }
            return builder.ToString();
        }

        /// <summary>未得时的进度读法；已得则无进度可言</summary>
        internal static string ProgressLine(in OniMeiCodexRow row) {
            if (row.Owned) {
                return OniMeiCodexUI.ProgressSettled?.Value ?? "";
            }
            if (row.Source != OniMeiCodexSource.Deed) {
                return OniMeiCodexUI.ProgressWaiting?.Value ?? "";
            }
            return OniMeiDeedRegistry.TryGetByMei(row.Key, out OniMeiDeed deed)
                ? OniMeiDeedText.DescribeProgress(deed, row.DeedValue)
                : OniMeiDeedText.LockedUnknown?.Value ?? "";
        }

        /// <summary>未得时顶替"出处"的那一句线索</summary>
        internal static string AcquireLine(in OniMeiCodexRow row) {
            if (row.Source == OniMeiCodexSource.Deed) {
                string hint = row.Definition?.DeedHint?.Value;
                if (!string.IsNullOrEmpty(hint)) {
                    return hint;
                }
            }
            return SourceLine(in row);
        }
    }
}
