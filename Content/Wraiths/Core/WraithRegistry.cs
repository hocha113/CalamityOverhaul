using CalamityOverhaul.Content.Wraiths.Runtime;
using System;
using System.Collections.Generic;

namespace CalamityOverhaul.Content.Wraiths.Core
{
    /// <summary>定义目录，Mod.Load 反射注册；键冲突与实体类复用在注册期报错</summary>
    internal sealed class WraithRegistry : ICWRLoader
    {
        private static readonly List<WraithDefinition> all = [];
        private static readonly Dictionary<string, WraithDefinition> byKey = [];
        private static readonly Dictionary<string, ushort> networkIdByKey = [];
        private static readonly Dictionary<Type, WraithDefinition> byActorType = [];

        /// <summary>全部定义，SortOrder 再 Key</summary>
        public static IReadOnlyList<WraithDefinition> All => all;

        public static int Count => all.Count;

        public static bool TryGet(string key, out WraithDefinition definition)
            => byKey.TryGetValue(key, out definition);

        internal static bool TryGetNetworkId(string key, out ushort id) {
            if (networkIdByKey.TryGetValue(key, out id)) {
                return true;
            }
            id = ushort.MaxValue;
            return false;
        }

        internal static bool TryGetByNetworkId(ushort id, out WraithDefinition definition) {
            if (id < all.Count) {
                definition = all[id];
                return true;
            }
            definition = null;
            return false;
        }

        /// <summary>实体类型反查定义</summary>
        public static WraithDefinition FindByActorType(Type actorType)
            => byActorType.TryGetValue(actorType, out WraithDefinition definition) ? definition : null;

        void ICWRLoader.LoadData() {
            List<WraithDefinition> found = VaultUtils.GetDerivedInstances<WraithDefinition>();
            found.Sort((a, b) => {
                int order = a.SortOrder.CompareTo(b.SortOrder);
                return order != 0 ? order : string.CompareOrdinal(a.Key, b.Key);
            });

            foreach (WraithDefinition definition in found) {
                if (string.IsNullOrWhiteSpace(definition.Key)) {
                    CWRMod.Instance.Logger.Error($"[WraithRegistry] {definition.GetType().FullName} has an empty Key, skipped");
                    continue;
                }
                if (byKey.ContainsKey(definition.Key)) {
                    CWRMod.Instance.Logger.Error($"[WraithRegistry] duplicate Key '{definition.Key}' from {definition.GetType().FullName}, skipped");
                    continue;
                }
                if (all.Count >= ushort.MaxValue) {
                    CWRMod.Instance.Logger.Error("[WraithRegistry] network id space exhausted, definition skipped");
                    continue;
                }
                if (definition.ActorType != null) {
                    if (!typeof(WraithActor).IsAssignableFrom(definition.ActorType)) {
                        CWRMod.Instance.Logger.Error($"[WraithRegistry] '{definition.Key}' ActorType {definition.ActorType.Name} is not a WraithActor, skipped");
                        continue;
                    }
                    if (byActorType.ContainsKey(definition.ActorType)) {
                        CWRMod.Instance.Logger.Error($"[WraithRegistry] actor type {definition.ActorType.Name} reused by '{definition.Key}', skipped");
                        continue;
                    }
                    byActorType[definition.ActorType] = definition;
                }

                definition.LoadLocalization();
                ushort networkId = (ushort)all.Count;
                all.Add(definition);
                byKey[definition.Key] = definition;
                networkIdByKey[definition.Key] = networkId;
            }
        }

        void ICWRLoader.UnLoadData() {
            all.Clear();
            byKey.Clear();
            networkIdByKey.Clear();
            byActorType.Clear();
        }
    }
}
