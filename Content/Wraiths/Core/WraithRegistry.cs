using System.Collections.Generic;

namespace CalamityOverhaul.Content.Wraiths.Core
{
    /// <summary>稳定厉鬼目录与网络编号。</summary>
    internal sealed class WraithRegistry : ICWRLoader
    {
        private static readonly List<WraithDefinition> all = [];
        private static readonly List<WraithDefinition> usable = [];
        private static readonly Dictionary<string, WraithDefinition> byKey = [];
        private static readonly Dictionary<string, ushort> networkIdByKey = [];
        private static readonly Dictionary<ushort, WraithDefinition> byNetworkId = [];

        public static IReadOnlyList<WraithDefinition> All => all;
        public static IReadOnlyList<WraithDefinition> Usable => usable;

        public static bool TryGet(string key, out WraithDefinition definition) {
            if (!string.IsNullOrEmpty(key) && byKey.TryGetValue(key, out definition)) {
                return true;
            }
            definition = null;
            return false;
        }

        internal static bool TryGetUsable(string key, out WraithDefinition definition) {
            if (TryGet(key, out definition) && definition.CanEquip) {
                return true;
            }
            definition = null;
            return false;
        }

        internal static bool TryGetNetworkId(string key, out ushort id) {
            if (!string.IsNullOrEmpty(key) && networkIdByKey.TryGetValue(key, out id)) {
                return true;
            }
            id = ushort.MaxValue;
            return false;
        }

        internal static bool TryGetByNetworkId(ushort id, out WraithDefinition definition) {
            return byNetworkId.TryGetValue(id, out definition);
        }

        void ICWRLoader.LoadData() {
            List<WraithDefinition> found = VaultUtils.GetDerivedInstances<WraithDefinition>();
            found.Sort((a, b) => {
                int order = a.SortOrder.CompareTo(b.SortOrder);
                return order != 0 ? order : string.CompareOrdinal(a.Key, b.Key);
            });

            foreach (WraithDefinition definition in found) {
                if (string.IsNullOrWhiteSpace(definition.Key) || byKey.ContainsKey(definition.Key)
                    || definition.NetworkId == ushort.MaxValue
                    || byNetworkId.ContainsKey(definition.NetworkId)) {
                    CWRMod.Instance.Logger.Error(
                        $"[WraithRegistry] invalid or duplicate identity '{definition.Key}'/{definition.NetworkId}");
                    continue;
                }
                definition.LoadLocalization();
                all.Add(definition);
                byKey[definition.Key] = definition;
                networkIdByKey[definition.Key] = definition.NetworkId;
                byNetworkId[definition.NetworkId] = definition;
                if (definition.CanEquip) {
                    usable.Add(definition);
                }
            }
        }

        void ICWRLoader.UnLoadData() {
            all.Clear();
            usable.Clear();
            byKey.Clear();
            networkIdByKey.Clear();
            byNetworkId.Clear();
            WraithSynergy.Unload();
        }
    }
}
