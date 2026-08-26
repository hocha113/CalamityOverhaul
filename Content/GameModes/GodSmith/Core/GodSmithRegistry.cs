using System.Collections.Generic;
using System.Linq;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Core
{
    /// <summary>
    /// 饰品重铸与词缀神赋的装配器：PostSetupContent（SetupData）反射扫描本程序集
    /// 全部非抽象子类实例化，注册本地化并建索引。
    /// 内容 agent 只建自己的类文件即接入，零共享文件改动。卸载清空全部静态注册表
    /// </summary>
    internal sealed class GodSmithRegistry : ICWRLoader
    {
        void ICWRLoader.SetupData() {
            SetupAccEffects();
            SetupEndows();
        }

        private static void SetupAccEffects() {
            //按全名排序保证注册顺序确定性
            List<GodSmithAccEffect> found = [.. VaultUtils.GetDerivedInstances<GodSmithAccEffect>()
                .OrderBy(effect => effect.FullName)];
            Dictionary<int, GodSmithAccEffect> byItem = [];
            foreach (GodSmithAccEffect effect in found) {
                effect.LoadLocalization();
                int[] ids = effect.TargetItemIDs;
                if (ids == null || ids.Length == 0) {
                    CWRMod.Instance.Logger.Error($"[GodSmith] 饰品效果 {effect.FullName} 未声明 TargetItemIDs，跳过");
                    continue;
                }
                foreach (int id in ids) {
                    if (!byItem.TryAdd(id, effect)) {
                        CWRMod.Instance.Logger.Error(
                            $"[GodSmith] 饰品 {id} 被重复认领：{byItem[id].FullName} 与 {effect.FullName}，后者生效");
                        byItem[id] = effect;
                    }
                }
            }
            GodSmithAccEffect.ByItemID = byItem;
        }

        private static void SetupEndows() {
            List<GodSmithEndow> found = [.. VaultUtils.GetDerivedInstances<GodSmithEndow>()
                .OrderBy(endow => endow.FullName)];
            Dictionary<string, GodSmithEndow> byKey = [];
            Dictionary<int, List<GodSmithEndow>> pools = [];
            foreach (GodSmithEndow endow in found) {
                endow.LoadLocalization();
                if (!byKey.TryAdd(endow.Key, endow)) {
                    CWRMod.Instance.Logger.Error(
                        $"[GodSmith] 神赋键 {endow.Key} 被重复注册：{byKey[endow.Key].FullName} 与 {endow.FullName}，后者生效");
                    byKey[endow.Key] = endow;
                }
                //对全部词缀（含模组词缀）求值一次建池；词缀 0 = 无词缀，不参与
                bool coversAny = false;
                for (int prefixId = 1; prefixId < PrefixLoader.PrefixCount; prefixId++) {
                    if (!endow.AppliesTo(prefixId)) {
                        continue;
                    }
                    coversAny = true;
                    if (!pools.TryGetValue(prefixId, out List<GodSmithEndow> pool)) {
                        pools[prefixId] = pool = [];
                    }
                    pool.Add(endow);
                }
                if (!coversAny) {
                    CWRMod.Instance.Logger.Error(
                        $"[GodSmith] 神赋 {endow.FullName} 未覆盖任何词缀，检查 CoveredPrefixes/AppliesTo");
                }
            }
            GodSmithEndow.ByKey = byKey;
            GodSmithEndow.PoolByPrefix = pools;
        }

        void ICWRLoader.UnLoadData() {
            GodSmithAccEffect.ByItemID = [];
            GodSmithEndow.ByKey = [];
            GodSmithEndow.PoolByPrefix = [];
        }
    }
}
