using CalamityOverhaul.Content.Wraiths.Core;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions
{
    /// <summary>
    /// 铭文定义目录（Mod.Load 反射注册，键冲突注册期报错）+ 改铭台数据缝：<br/>
    /// 展示读取缓存上一份（未持刀不跳零），写入仅认手中刀并推 <see cref="WraithVessels.SyncSlot"/>
    /// </summary>
    internal sealed class OniMeiRegistry : ICWRLoader
    {
        private static readonly List<OniMeiDefinition> all = [];
        private static readonly Dictionary<string, OniMeiDefinition> byKey = [];

        /// <summary>全部定义，SortOrder 再 Key</summary>
        public static IReadOnlyList<OniMeiDefinition> All => all;

        public static bool TryGet(string key, out OniMeiDefinition definition)
            => byKey.TryGetValue(key, out definition);

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
                definition.LoadLocalization();
                all.Add(definition);
                byKey[definition.Key] = definition;
            }
        }

        void ICWRLoader.UnLoadData() {
            all.Clear();
            byKey.Clear();
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

        /// <summary>凿铭/改铭手中刀，成功推物品同步</summary>
        public static bool EngraveHeld(OniMeiSlotKind slot, string key) {
            Player player = Main.LocalPlayer;
            Item item = player?.GetItem();
            OnikiriData data = OnikiriData.TryGet(item);
            if (data == null || !data.Mei.Engrave(slot, key)) {
                return false;
            }
            WraithVessels.SyncSlot(player, item);
            return true;
        }

        /// <summary>除铭手中刀，成功推物品同步</summary>
        public static bool EraseHeld(OniMeiSlotKind slot) {
            Player player = Main.LocalPlayer;
            Item item = player?.GetItem();
            OnikiriData data = OnikiriData.TryGet(item);
            if (data == null || !data.Mei.Erase(slot)) {
                return false;
            }
            WraithVessels.SyncSlot(player, item);
            return true;
        }
    }
}
