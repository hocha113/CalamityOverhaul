using CalamityOverhaul.Content.Narrative.Data.Modules;
using InnoVault.DataModules;
using System;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Narrative.Data
{
    /// <summary>
    /// 旧 ADV 存档 → 新 <see cref="DataModuleStore"/>。三档历史格式<br/>
    /// v2 <c>ADVSave</c> 按旧 SaveKey 分模块子标签；v1 扁平字段无 <c>__version</c>；
    /// v0 <c>HalibutSave</c> 内嵌 <c>ADCSave</c> 扁平。<br/>
    /// 字段改名靠 <see cref="DataModuleNameAttribute"/> 别名
    /// </summary>
    internal static class LegacyStorySaveImporter
    {
        /// <summary>旧模块 → 新模块</summary>
        private sealed class LegacyModuleMap(string legacyKey, string flatProbeKey, Action<TagCompound, DataModuleStore> load)
        {
            /// <summary>v2 子标签键，旧 <c>ADVDataModule.SaveKey</c></summary>
            public string LegacyKey { get; } = legacyKey;
            /// <summary>v0/v1 探针字段；null 表示只走分模块（字段与他模块冲突）</summary>
            public string FlatProbeKey { get; } = flatProbeKey;
            public Action<TagCompound, DataModuleStore> Load { get; } = load;
        }

        private static LegacyModuleMap Map<T>(string legacyKey, string flatProbeKey) where T : DataModule, new()
            => new(legacyKey, flatProbeKey, (tag, store) => store.Get<T>().LoadData(tag, loadedVersion: 0));

        //ShepelGiftData / BossGiftADVData 同名礼物字段多，flatProbeKey 置空只走分模块
        private static readonly LegacyModuleMap[] ModuleMaps = [
            Map<HalibutStoryData>("HalibutADVData", "HasCaughtHalibut"),
            Map<SupCalStoryData>("SupCalADVData", "FirstMetSupCal"),
            Map<DraedonStoryData>("DraedonADVData", "DeploySignaltowerQuestAccepted"),
            Map<OldDukeStoryData>("OldDukeADVData", "OldDukeInteraction"),
            Map<BossGiftStoryData>("BossGiftADVData", "QueenBeeGift"),
            Map<ShepelStoryData>("ShepelADVData", "IdleVariantSeed"),
            Map<ShepelGiftStoryData>("ShepelGiftData", flatProbeKey: null),
            Map<EntrustGuideData>("EntrustGuideModule", "GuideSeen"),
        ];

        /// <summary>从旧 ADVSave / ADCSave / 已解包根标签迁入 store</summary>
        /// <returns>是否迁到了旧数据</returns>
        public static bool TryImport(TagCompound tag, DataModuleStore store) {
            if (tag == null || store == null) {
                return false;
            }

            TagCompound root = ResolveRoot(tag);
            return root != null && ImportRoot(root, store);
        }

        /// <summary>解包到模块根；无外层包装则原样返回</summary>
        private static TagCompound ResolveRoot(TagCompound tag) {
            if (tag.TryGet<TagCompound>("ADVSave", out TagCompound advTag)) {
                return advTag;
            }
            if (tag.TryGet<TagCompound>("ADCSave", out TagCompound adcTag)) {
                return adcTag;
            }
            //已是模块根（v0 解包后或 v1 扁平）
            return tag;
        }

        private static bool ImportRoot(TagCompound root, DataModuleStore store) {
            bool sectioned = IsSectionedFormat(root);
            bool imported = false;

            foreach (LegacyModuleMap map in ModuleMaps) {
                //v2 读子标签；v0/v1 按探针从扁平根取
                TagCompound moduleTag;
                if (sectioned) {
                    if (!root.TryGet(map.LegacyKey, out moduleTag)) {
                        continue;
                    }
                }
                else if (map.FlatProbeKey != null && root.ContainsKey(map.FlatProbeKey)) {
                    moduleTag = root;
                }
                else {
                    continue;
                }

                //单模块异常不中断其余、不抛回读档
                try {
                    map.Load(moduleTag, store);
                    imported = true;
                } catch (Exception ex) {
                    CWRMod.Instance.Logger.Error($"Legacy ADV migration: module '{map.LegacyKey}' skipped due to load error.", ex);
                }
            }

            return imported;
        }

        /// <summary>v2，存在任一旧 SaveKey 子标签</summary>
        private static bool IsSectionedFormat(TagCompound root) {
            foreach (LegacyModuleMap map in ModuleMaps) {
                if (root.TryGet<TagCompound>(map.LegacyKey, out _)) {
                    return true;
                }
            }
            return false;
        }
    }
}
