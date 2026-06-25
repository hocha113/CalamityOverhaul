using CalamityOverhaul.Content.Narrative.Data.Modules;
using InnoVault.DataModules;
using System;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Narrative.Data
{
    /// <summary>
    /// 旧 ADV 存档 → 新 <see cref="DataModuleStore"/> 的迁移层。兼容三种历史格式：<br/>
    /// v2：<c>ADVSave</c> 内按旧 SaveKey（旧类型名）分模块子标签；<br/>
    /// v1：<c>ADVSave</c> 扁平字段（无 <c>__version</c>，字段直接位于标签内）；<br/>
    /// v0：<c>HalibutSave</c> 内嵌的 <c>ADCSave</c> 扁平字段（由 <c>HalibutSave</c> 解包后传入）。<br/>
    /// 字段改名（如 <c>FristExoMechdusaSum</c>、<c>OldDukeInteraction</c>）由新模块上的
    /// <see cref="DataModuleNameAttribute"/> 别名自动兼容，无需在此特殊处理
    /// </summary>
    internal static class LegacyStorySaveImporter
    {
        /// <summary>一条"旧模块 → 新模块"的迁移映射</summary>
        private sealed class LegacyModuleMap(string legacyKey, string flatProbeKey, Action<TagCompound, DataModuleStore> load)
        {
            /// <summary>v2 分模块格式下该模块的子标签键（即旧 <c>ADVDataModule.SaveKey</c> = 旧类型名）</summary>
            public string LegacyKey { get; } = legacyKey;
            /// <summary>
            /// 扁平格式（v0/v1）下用于判定该模块数据是否存在的探针字段名；<br/>
            /// 旧版会写出模块的全部字段，故任取一个该模块独有的字段即可。<br/>
            /// 为 <see langword="null"/> 表示该模块不参与扁平迁移（字段名与其它模块冲突、无法区分时）
            /// </summary>
            public string FlatProbeKey { get; } = flatProbeKey;
            /// <summary>把给定模块标签加载进 store 中对应的新模块</summary>
            public Action<TagCompound, DataModuleStore> Load { get; } = load;
        }

        private static LegacyModuleMap Map<T>(string legacyKey, string flatProbeKey) where T : DataModule, new()
            => new(legacyKey, flatProbeKey, (tag, store) => store.Get<T>().LoadData(tag, loadedVersion: 0));

        //旧 ADVSave 自动发现全部 ADVDataModule 子类，子标签键即旧类型名。顺序无关紧要，仅 ShepelGiftData
        //与 BossGiftADVData 存在大量同名礼物字段，扁平格式下无法区分归属，故其 flatProbeKey 置空只走分模块迁移
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

        /// <summary>
        /// 尝试从旧存档标签迁移到 <paramref name="store"/>。<paramref name="tag"/> 可以是：<br/>
        /// 旧 ADVSavePlayer 的数据标签（外层含 <c>ADVSave</c> 子标签）、<br/>
        /// 旧 HalibutSave 内嵌的 <c>ADCSave</c> 标签，或已解包的模块根标签
        /// </summary>
        /// <returns>是否检测到并迁移了旧数据</returns>
        public static bool TryImport(TagCompound tag, DataModuleStore store) {
            if (tag == null || store == null) {
                return false;
            }

            TagCompound root = ResolveRoot(tag);
            return root != null && ImportRoot(root, store);
        }

        /// <summary>解包到真正承载模块数据的根标签（外层包装则取出，否则视 <paramref name="tag"/> 自身为根）</summary>
        private static TagCompound ResolveRoot(TagCompound tag) {
            if (tag.TryGet<TagCompound>("ADVSave", out TagCompound advTag)) {
                return advTag;
            }
            if (tag.TryGet<TagCompound>("ADCSave", out TagCompound adcTag)) {
                return adcTag;
            }
            //已是模块根：v0 内嵌 ADCSave 经 HalibutSave 解包后直接传入，或 v1 扁平根
            return tag;
        }

        private static bool ImportRoot(TagCompound root, DataModuleStore store) {
            if (IsSectionedFormat(root)) {
                //v2：每个模块从各自的子标签读取，天然无字段名冲突
                bool imported = false;
                foreach (LegacyModuleMap map in ModuleMaps) {
                    if (root.TryGet<TagCompound>(map.LegacyKey, out TagCompound moduleTag)) {
                        map.Load(moduleTag, store);
                        imported = true;
                    }
                }
                return imported;
            }

            //v0/v1：字段全局唯一并直接位于根标签，仅在探针字段存在时把该模块从扁平根读取
            bool flatImported = false;
            foreach (LegacyModuleMap map in ModuleMaps) {
                if (map.FlatProbeKey != null && root.ContainsKey(map.FlatProbeKey)) {
                    map.Load(root, store);
                    flatImported = true;
                }
            }
            return flatImported;
        }

        /// <summary>是否为 v2 分模块格式（存在任一旧 SaveKey 对应的子标签）</summary>
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
