using CalamityOverhaul.Content.ADV.MainMenuOvers;
using CalamityOverhaul.Content.ADV.Scenarios;
using CalamityOverhaul.Content.ADV.Scenarios.SupCal;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.ADV
{
    /// <summary>
    /// ADV 旧存档迁移（v0/v1 扁平 → v2 分模块）
    /// v0：HalibutSave 内 ADCSave 嵌套；v1：ADVSave 扁平；v2：__version=2 分模块 TagCompound
    /// </summary>
    internal static class ADVLegacyMigration
    {
        /// <summary>
        /// v0 格式 HalibutSave 内 ADV 数据键
        /// </summary>
        private const string LegacyADCSaveKey = "ADCSave";

        /// <summary>
        /// 是否为旧版扁平格式（无 __version，v0/v1）
        /// </summary>
        public static bool IsLegacyFormat(TagCompound tag) {
            return !tag.ContainsKey(ADVSave.VersionKey);
        }

        /// <summary>
        /// 从扁平 TagCompound 加载各模块（v0/v1）
        /// 字段名全局唯一，各模块直接提取自身字段
        /// </summary>
        /// <returns>是否检测到旧版格式</returns>
        public static bool TryLoadFromFlatFormat(TagCompound tag, IEnumerable<ADVDataModule> modules) {
            if (!IsLegacyFormat(tag)) {
                return false;
            }
            foreach (var module in modules) {
                module.LoadFields(tag);
            }
            return true;
        }

        /// <summary>
        /// 从 HalibutSave 迁移 v0 ADV 数据（模块 + 场景）
        /// </summary>
        /// <param name="halibutTag">HalibutSave TagCompound</param>
        /// <param name="player">当前玩家</param>
        /// <param name="advSave">目标 ADVSave</param>
        /// <returns>是否完成迁移</returns>
        public static bool TryMigrateFromHalibutSave(TagCompound halibutTag, Player player, ADVSave advSave) {
            if (!halibutTag.TryGet<TagCompound>(LegacyADCSaveKey, out var adcTag)) {
                return false;
            }

            // v0 扁平 tag，LoadData 走旧版路径
            advSave.LoadData(adcTag);

            // 迁移后解锁肖像等
            if (advSave.Get<SupCalADVData>().EternalBlazingNow) {
                MenuSave.UnlockEternalBlazingNowPortrait(player);
            }

            // v0 场景数据同在 HalibutSave tag
            foreach (var scenario in ADVScenarioBase.Instances) {
                scenario.LoadData(halibutTag);
            }

            return true;
        }
    }
}
