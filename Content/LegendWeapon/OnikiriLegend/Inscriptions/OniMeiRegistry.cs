using CalamityOverhaul.Content.Wraiths.Core;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions
{
    /// <summary>
    /// 铭文定义目录（Mod.Load 反射注册，键冲突注册期报错）+ 改铭台数据缝：<br/>
    /// 展示读取缓存上一份（未持刀不跳零），写入仅认手中刀并走字段级确认
    /// </summary>
    internal sealed class OniMeiRegistry : ICWRLoader
    {
        private static readonly List<OniMeiDefinition> all = [];
        private static readonly Dictionary<string, OniMeiDefinition> byKey = [];
        private static readonly Dictionary<string, ushort> networkIdByKey = [];

        /// <summary>全部定义，SortOrder 再 Key</summary>
        public static IReadOnlyList<OniMeiDefinition> All => all;

        public static bool TryGet(string key, out OniMeiDefinition definition)
            => byKey.TryGetValue(key, out definition);

        internal static bool TryGetNetworkId(string key, out ushort id) {
            if (networkIdByKey.TryGetValue(key, out id)) {
                return true;
            }
            id = ushort.MaxValue;
            return false;
        }

        internal static bool TryGetByNetworkId(ushort id, out OniMeiDefinition definition) {
            if (id < all.Count) {
                definition = all[id];
                return true;
            }
            definition = null;
            return false;
        }

        /// <summary>某铭位的候选名册（保持 SortOrder 序）</summary>
        public static List<OniMeiDefinition> GetBySlot(OniMeiSlotKind slot) {
            List<OniMeiDefinition> list = [];
            foreach (OniMeiDefinition definition in all) {
                if (definition.SlotKind == slot) {
                    list.Add(definition);
                }
            }
            return list;
        }

        void ICWRLoader.LoadData() {
            List<OniMeiDefinition> found = VaultUtils.GetDerivedInstances<OniMeiDefinition>();
            found.Sort((a, b) => {
                int order = a.SortOrder.CompareTo(b.SortOrder);
                return order != 0 ? order : string.CompareOrdinal(a.Key, b.Key);
            });

            foreach (OniMeiDefinition definition in found) {
                if (string.IsNullOrWhiteSpace(definition.Key)) {
                    CWRMod.Instance.Logger.Error($"[OniMeiRegistry] {definition.GetType().FullName} has an empty Key, skipped");
                    continue;
                }
                if (byKey.ContainsKey(definition.Key)) {
                    CWRMod.Instance.Logger.Error($"[OniMeiRegistry] duplicate Key '{definition.Key}' from {definition.GetType().FullName}, skipped");
                    continue;
                }
                if (all.Count >= ushort.MaxValue) {
                    CWRMod.Instance.Logger.Error("[OniMeiRegistry] network id space exhausted, definition skipped");
                    continue;
                }
                ushort networkId = (ushort)all.Count;
                all.Add(definition);
                byKey[definition.Key] = definition;
                networkIdByKey[definition.Key] = networkId;
                OniMeiRubbingItem.TryBindLocalization(definition);
            }
        }

        void ICWRLoader.SetupData() {
            foreach (OniMeiDefinition definition in all) {
                if (OniMeiRubbingItem.TryBindLocalization(definition) && definition.HasLocalization) {
                    continue;
                }
                CWRMod.Instance.Logger.Error(
                    $"[OniMeiRegistry] no rubbing localization source registered for Key '{definition.Key}'");
            }
        }

        void ICWRLoader.UnLoadData() {
            all.Clear();
            byKey.Clear();
            networkIdByKey.Clear();
            displayStore = null;
        }

        //====展示读取====

        //未持刀保留上一份：改铭台淡出期间读数不跳零
        private static OniMeiStore displayStore;

        /// <summary>本地持刀数据，服务器/菜单/未持 null</summary>
        private static OnikiriData ResolveLocalData() {
            if (Main.dedServ || Main.gameMenu) {
                return null;
            }
            return OnikiriData.TryGet(Main.LocalPlayer?.GetItem());
        }

        /// <summary>展示用铭位表，未持刀回落上一份，可为 null（从未持过刀）</summary>
        public static OniMeiStore DisplayStore {
            get {
                OnikiriData data = ResolveLocalData();
                if (data != null) {
                    displayStore = data.Mei;
                }
                return displayStore;
            }
        }

        /// <summary>铭位上的定义，空位/未注册 null</summary>
        public static OniMeiDefinition GetEngraved(OniMeiStore store, OniMeiSlotKind slot) {
            string key = store?.Get(slot);
            return key != null && byKey.TryGetValue(key, out OniMeiDefinition definition) ? definition : null;
        }

        /// <summary>当前刀铭（茎铭位），空铭回落「鬼切」；供右缘大字与题名</summary>
        public static OniMeiDefinition CurrentBladeName(OniMeiStore store) {
            OniMeiDefinition engraved = GetEngraved(store, OniMeiSlotKind.Nakago);
            if (engraved != null) {
                return engraved;
            }
            return byKey.TryGetValue(nameof(MeiOnikiri), out OniMeiDefinition fallback) ? fallback : null;
        }

        //====写入（仅认手中刀）====

        /// <summary>凿铭/改铭手中刀，成功推物品同步；须所持</summary>
        public static bool EngraveHeld(OniMeiSlotKind slot, string key, Action<bool> completed = null) {
            Player player = Main.LocalPlayer;
            Item item = player?.GetItem();
            return OnikiriNet.TryChangeMei(player, item, slot, key, completed);
        }

        /// <summary>除铭手中刀，成功推物品同步</summary>
        public static bool EraseHeld(OniMeiSlotKind slot, Action<bool> completed = null) {
            Player player = Main.LocalPlayer;
            Item item = player?.GetItem();
            return OnikiriNet.TryChangeMei(player, item, slot, null, completed);
        }
    }
}
