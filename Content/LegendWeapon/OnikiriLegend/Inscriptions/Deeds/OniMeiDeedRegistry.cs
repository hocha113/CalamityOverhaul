using System.Collections.Generic;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions.Deeds
{
    /// <summary>
    /// 刀縁目录（Mod.Load 反射注册，键冲突或指向不存在的铭即注册期报错）。<br/>
    /// 按信道分桶，事件只遍历对应那一桶；ushort 网络 ID 与 <see cref="OniMeiRegistry"/> 同口径
    /// </summary>
    internal sealed class OniMeiDeedRegistry : ICWRLoader
    {
        private static readonly List<OniMeiDeed> all = [];
        private static readonly Dictionary<string, OniMeiDeed> byKey = [];
        private static readonly Dictionary<string, OniMeiDeed> byMeiKey = [];
        private static readonly Dictionary<string, ushort> networkIdByKey = [];
        private static readonly Dictionary<OniMeiDeedChannel, List<OniMeiDeed>> byChannel = [];

        public static IReadOnlyList<OniMeiDeed> All => all;

        public static bool TryGet(string key, out OniMeiDeed deed)
            => byKey.TryGetValue(key, out deed);

        /// <summary>某枚铭的刀縁；无縁（出厂所持或赠礼所得）返回 false</summary>
        public static bool TryGetByMei(string meiKey, out OniMeiDeed deed) {
            deed = null;
            return meiKey != null && byMeiKey.TryGetValue(meiKey, out deed);
        }

        /// <summary>该信道上的縁；无縁返回空表</summary>
        internal static List<OniMeiDeed> OfChannel(OniMeiDeedChannel channel)
            => byChannel.TryGetValue(channel, out List<OniMeiDeed> list) ? list : [];

        internal static bool TryGetNetworkId(string key, out ushort id) {
            if (key != null && networkIdByKey.TryGetValue(key, out id)) {
                return true;
            }
            id = ushort.MaxValue;
            return false;
        }

        internal static bool TryGetByNetworkId(ushort id, out OniMeiDeed deed) {
            if (id < all.Count) {
                deed = all[id];
                return true;
            }
            deed = null;
            return false;
        }

        void ICWRLoader.LoadData() {
            List<OniMeiDeed> found = VaultUtils.GetDerivedInstances<OniMeiDeed>();
            found.Sort((a, b) => {
                int order = a.SortOrder.CompareTo(b.SortOrder);
                return order != 0 ? order : string.CompareOrdinal(a.Key, b.Key);
            });

            foreach (OniMeiDeed deed in found) {
                if (string.IsNullOrWhiteSpace(deed.Key) || string.IsNullOrWhiteSpace(deed.MeiKey)) {
                    CWRMod.Instance.Logger.Error(
                        $"[OniMeiDeedRegistry] {deed.GetType().FullName} has an empty Key or MeiKey, skipped");
                    continue;
                }
                if (byKey.ContainsKey(deed.Key)) {
                    CWRMod.Instance.Logger.Error(
                        $"[OniMeiDeedRegistry] duplicate Key '{deed.Key}' from {deed.GetType().FullName}, skipped");
                    continue;
                }
                if (byMeiKey.ContainsKey(deed.MeiKey)) {
                    CWRMod.Instance.Logger.Error(
                        $"[OniMeiDeedRegistry] MeiKey '{deed.MeiKey}' already has a deed, '{deed.Key}' skipped");
                    continue;
                }
                if (all.Count >= ushort.MaxValue) {
                    CWRMod.Instance.Logger.Error("[OniMeiDeedRegistry] network id space exhausted, deed skipped");
                    continue;
                }
                networkIdByKey[deed.Key] = (ushort)all.Count;
                all.Add(deed);
                byKey[deed.Key] = deed;
                byMeiKey[deed.MeiKey] = deed;
                if (!byChannel.TryGetValue(deed.Channel, out List<OniMeiDeed> bucket)) {
                    bucket = [];
                    byChannel[deed.Channel] = bucket;
                }
                bucket.Add(deed);
            }
        }

        void ICWRLoader.SetupData() {
            //铭注册在前，此处才能核对指向；同时挡住"縁指向出厂所持"的设计错
            foreach (OniMeiDeed deed in all) {
                if (!OniMeiRegistry.TryGet(deed.MeiKey, out _)) {
                    CWRMod.Instance.Logger.Error(
                        $"[OniMeiDeedRegistry] deed '{deed.Key}' targets unknown mei '{deed.MeiKey}'");
                }
                else if (OniMeiOwned.IsDefaultOwned(deed.MeiKey)) {
                    CWRMod.Instance.Logger.Error(
                        $"[OniMeiDeedRegistry] deed '{deed.Key}' targets factory-owned mei '{deed.MeiKey}'");
                }
            }
        }

        void ICWRLoader.UnLoadData() {
            all.Clear();
            byKey.Clear();
            byMeiKey.Clear();
            networkIdByKey.Clear();
            byChannel.Clear();
        }
    }
}
