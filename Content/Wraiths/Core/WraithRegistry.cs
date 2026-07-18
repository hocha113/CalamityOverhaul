using CalamityOverhaul.Content.Wraiths.Runtime;
using System;
using System.Collections.Generic;

namespace CalamityOverhaul.Content.Wraiths.Core
{
    /// <summary>
    /// 厉鬼定义目录。Mod.Load 期反射扫描全部 <see cref="WraithDefinition"/> 子类自动注册，
    /// 不存在手工名单；键冲突与实体类复用在注册期直接报错暴露
    /// </summary>
    internal sealed class WraithRegistry : ICWRLoader
    {
        private static readonly List<WraithDefinition> all = [];
        private static readonly Dictionary<string, WraithDefinition> byKey = [];
        private static readonly Dictionary<Type, WraithDefinition> byActorType = [];

        /// <summary>全部定义，按 SortOrder 再按 Key 排序</summary>
        public static IReadOnlyList<WraithDefinition> All => all;

        public static int Count => all.Count;

        public static bool TryGet(string key, out WraithDefinition definition)
            => byKey.TryGetValue(key, out definition);

        /// <summary>实体类型反查定义，WraithActor 借此确定自己的身份</summary>
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
                all.Add(definition);
                byKey[definition.Key] = definition;
            }
        }

        void ICWRLoader.UnLoadData() {
            all.Clear();
            byKey.Clear();
            byActorType.Clear();
        }
    }
}
