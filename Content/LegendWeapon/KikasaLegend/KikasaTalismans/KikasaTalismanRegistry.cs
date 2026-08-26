using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans
{
    /// <summary>
    /// 唤雨符定义目录（Mod.Load 反射注册，键冲突注册期报错）+ 祈雨绳数据缝：<br/>
    /// 符位表挂玩家（<see cref="KikasaTalismanPlayer.Talismans"/>），展示直读本地玩家，
    /// 写入仅认手中伞、本机落位后推快照；挂/摘/换入口签名对 UI 保持不变
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
        }

        //====展示读取====

        /// <summary>
        /// 展示用符位表（UI 兼容入口，签名不变）：直读本地玩家的玩家侧存储，
        /// 不再随持伞跳变；服务器/菜单 null
        /// </summary>
        public static KikasaTalismanStore DisplayStore {
            get {
                if (Main.dedServ || Main.gameMenu) {
                    return null;
                }
                Player player = Main.LocalPlayer;
                return player != null && player.TryGetModPlayer(out KikasaTalismanPlayer ktp)
                    ? ktp.Talismans : null;
            }
        }

        /// <summary>符位上的定义，空位/未注册 null</summary>
        public static KikasaTalismanDefinition GetHung(KikasaTalismanStore store, int slot) {
            string key = store?.Get(slot);
            return key != null && byKey.TryGetValue(key, out KikasaTalismanDefinition definition) ? definition : null;
        }

        //====写入（仅认手中伞；数据落玩家身上）====

        //回调契约与请求-回执时代一致：校验失败只返回 false 不进回调，
        //成功才回调 true——UI 的"返回值拒绝"与"回调拒绝"两条提示路径不会双触发。
        //迁玩家侧后写入即时落位，回调不再有异步 false

        /// <summary>本机校验并写入玩家侧符位表；须持伞（含鼠标项），成功推快照广播</summary>
        private static bool TryEditRope(Func<KikasaTalismanStore, bool> edit) {
            Player player = Main.LocalPlayer;
            if (Main.dedServ || Main.gameMenu || player == null
                || KikasaData.TryGet(player.GetItem()) == null
                || !player.TryGetModPlayer(out KikasaTalismanPlayer ktp)
                || !edit(ktp.Talismans)) {
                return false;
            }
            KikasaTalismanNet.SendRopeSnapshot(player);
            return true;
        }

        /// <summary>挂符/换符（UI 兼容入口，签名不变）；Key 须已录入符箧。须所持</summary>
        public static bool HangHeld(int slot, string key, Action<bool> completed = null) {
            if (!KikasaTalismanOwned.Owns(Main.LocalPlayer, key)
                || !TryEditRope(store => store.Hang(slot, key))) {
                return false;
            }
            completed?.Invoke(true);
            return true;
        }

        /// <summary>摘符（UI 兼容入口，签名不变）</summary>
        public static bool TakeDownHeld(int slot, Action<bool> completed = null) {
            if (!TryEditRope(store => store.TakeDown(slot))) {
                return false;
            }
            completed?.Invoke(true);
            return true;
        }

        /// <summary>互换两符位（一方为空即挪结；UI 兼容入口，签名不变）。须所持</summary>
        public static bool SwapHeld(int slotA, int slotB, Action<bool> completed = null) {
            if (!TryEditRope(store => store.Swap(slotA, slotB))) {
                return false;
            }
            completed?.Invoke(true);
            return true;
        }
    }
}
