using CalamityOverhaul.Content.ADV.MainMenuOvers;
using CalamityOverhaul.Content.ADV.Scenarios;
using CalamityOverhaul.Content.ADV.Scenarios.SupCal;
using System;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.ADV
{
    /// <summary>
    /// ADV系统专用的玩家存档ModPlayer，负责ADVSave数据和场景数据的保存与加载
    /// </summary>
    internal class ADVSavePlayer : ModPlayer
    {
        public ADVSave ADVSave { get; private set; }

        public override void Initialize() {
            ADVSave = new ADVSave();
        }

        public override ModPlayer Clone(Player newEntity) {
            ADVSavePlayer modPlayer = (ADVSavePlayer)base.Clone(newEntity);
            //深拷贝ADVSave，避免克隆体与原始玩家共享同一数据实例
            modPlayer.ADVSave = ADVSave?.DeepCopy() ?? new ADVSave();
            return modPlayer;
        }

        public override void SaveData(TagCompound tag) {
            try {
                TagCompound temp = [];
                temp["ADVSave"] = ADVSave.SaveData();

                if (ADVSave.Get<SupCalADVData>().EternalBlazingNow) {
                    MenuSave.UnlockEternalBlazingNowPortrait(Player);
                }

                foreach (var scenario in ADVScenarioBase.Instances) {
                    scenario.SaveData(temp);
                }

                foreach (var entry in temp) {
                    tag[entry.Key] = entry.Value;
                }
            } catch (Exception ex) {
                CWRMod.Instance.Logger.Error("ADVSavePlayer.SaveData Error", ex);
            }
        }

        public override void LoadData(TagCompound tag) {
            try {
                if (tag.TryGet<TagCompound>("ADVSave", out var advTag)) {
                    ADVSave.LoadData(advTag);
                }

                if (ADVSave.Get<SupCalADVData>().EternalBlazingNow) {
                    MenuSave.UnlockEternalBlazingNowPortrait(Player);
                }

                foreach (var scenario in ADVScenarioBase.Instances) {
                    scenario.LoadData(tag);
                }
            } catch (Exception ex) {
                CWRMod.Instance.Logger.Error("ADVSavePlayer.LoadData Error", ex);
                ADVSave = new ADVSave();
            }
        }

        /// <summary>
        /// 从旧版HalibutSave迁移ADV数据（向后兼容），委托给<see cref="ADVLegacyMigration"/>处理
        /// </summary>
        internal void MigrateFromLegacy(TagCompound halibutTag) {
            ADVLegacyMigration.TryMigrateFromHalibutSave(halibutTag, Player, ADVSave);
        }
    }
}
