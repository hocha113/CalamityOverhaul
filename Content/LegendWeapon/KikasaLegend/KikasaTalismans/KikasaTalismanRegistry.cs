using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans
{
    /// <summary>
    /// 唤雨符定义目录（Mod.Load 反射注册，键冲突注册期报错）+ 祈雨绳数据缝：<br/>
    /// 展示读取缓存上一份（未持伞不跳零），写入仅认手中伞并走字段级确认
    /// </summary>
    internal sealed class KikasaTalismanRegistry : ICWRLoader
    {
        private static readonly List<KikasaTalismanDefinition> all = [];
        private static readonly Dictionary<string, KikasaTalismanDefinition> byKey = [];
        private static readonly Dictionary<string, ushort> networkIdByKey = [];

        /// <summary>全部定义，SortOrder 再 Key</summary>
        public static IReadOnlyList<KikasaTalismanDefinition> All => all;

        public static bool TryGet(string key, out KikasaTalismanDefinition definition) {
            if (!string.IsNullOrEmpty(key) && byKey.TryGetValue(key, out definition)) {
                return true;
            }
            definition = null;
            return false;
        }

        internal static bool TryGetNetworkId(string key, out ushort id) {
            if (key != null && networkIdByKey.TryGetValue(key, out id)) {
                return true;
            }
            id = ushort.MaxValue;
            return false;
        }

        internal static bool TryGetByNetworkId(ushort id, out KikasaTalismanDefinition definition) {
            if (id < all.Count) {
                definition = all[id];
                return true;
            }
            definition = null;
            return false;
        }

        void ICWRLoader.LoadData() {
            List<KikasaTalismanDefinition> found = VaultUtils.GetDerivedInstances<KikasaTalismanDefinition>();
            found.Sort((a, b) => {
                int order = a.SortOrder.CompareTo(b.SortOrder);
                return order != 0 ? order : string.CompareOrdinal(a.Key, b.Key);
            });

            foreach (KikasaTalismanDefinition definition in found) {
                if (string.IsNullOrWhiteSpace(definition.Key)) {
                    CWRMod.Instance.Logger.Error($"[KikasaTalismanRegistry] {definition.GetType().FullName} has an empty Key, skipped");
                    continue;
                }
                if (byKey.ContainsKey(definition.Key)) {
                    CWRMod.Instance.Logger.Error($"[KikasaTalismanRegistry] duplicate Key '{definition.Key}' from {definition.GetType().FullName}, skipped");
                    continue;
                }
                if (all.Count >= ushort.MaxValue) {
                    CWRMod.Instance.Logger.Error("[KikasaTalismanRegistry] network id space exhausted, definition skipped");
                    continue;
                }
                ushort networkId = (ushort)all.Count;
                all.Add(definition);
                byKey[definition.Key] = definition;
                networkIdByKey[definition.Key] = networkId;
                //字形随符走：定义自带笔画，注册期收进中央缓存（null 保持伞形 fallback）
                KikasaTalismanGlyph.Register(definition.Key, definition.BuildGlyph());
                KikasaTalismanItem.TryBindLocalization(definition);
            }
        }

        void ICWRLoader.SetupData() {
            foreach (KikasaTalismanDefinition definition in all) {
                if (KikasaTalismanItem.TryBindLocalization(definition) && definition.HasLocalization) {
                    continue;
                }
                CWRMod.Instance.Logger.Error(
                    $"[KikasaTalismanRegistry] no talisman item localization source registered for Key '{definition.Key}'");
            }
        }

        void ICWRLoader.UnLoadData() {
            all.Clear();
            byKey.Clear();
            networkIdByKey.Clear();
            KikasaTalismanGlyph.ClearRegistry();
            displayStore = null;
        }

        //====展示读取====

        //未持伞保留上一份：湖心景淡出期间读数不跳零
        private static KikasaTalismanStore displayStore;

        /// <summary>本地持伞数据，服务器/菜单/未持 null</summary>
        private static KikasaData ResolveLocalData() {
            if (Main.dedServ || Main.gameMenu) {
                return null;
            }
            return KikasaData.TryGet(Main.LocalPlayer?.GetItem());
        }

        /// <summary>展示用符位表，未持伞回落上一份，可为 null（从未持过伞）</summary>
        public static KikasaTalismanStore DisplayStore {
            get {
                KikasaData data = ResolveLocalData();
                if (data != null) {
                    displayStore = data.Talismans;
                }
                return displayStore;
            }
        }

        /// <summary>符位上的定义，空位/未注册 null</summary>
        public static KikasaTalismanDefinition GetHung(KikasaTalismanStore store, int slot) {
            string key = store?.Get(slot);
            return key != null && byKey.TryGetValue(key, out KikasaTalismanDefinition definition) ? definition : null;
        }

        //====写入（仅认手中伞）====

        /// <summary>挂符/换符到手中伞，成功推物品同步；须所持</summary>
        public static bool HangHeld(int slot, string key, Action<bool> completed = null) {
            Player player = Main.LocalPlayer;
            Item item = player?.GetItem();
            return KikasaTalismanNet.TryChangeTalisman(player, item, slot, key, completed);
        }

        /// <summary>摘符手中伞，成功推物品同步</summary>
        public static bool TakeDownHeld(int slot, Action<bool> completed = null) {
            Player player = Main.LocalPlayer;
            Item item = player?.GetItem();
            return KikasaTalismanNet.TryChangeTalisman(player, item, slot, null, completed);
        }
    }
}
