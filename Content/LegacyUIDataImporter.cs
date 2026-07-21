using CalamityOverhaul.Content.EntrustManager;
using CalamityOverhaul.Content.QuestLogs;
using CalamityOverhaul.Content.Scenarios.SupCal.End.EternalBlazingNow.Enchants;
using InnoVault.GameSystem;
using System;
using System.IO;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content
{
    /// <summary>
    /// 旧 UIDataSave 扁平键 → InnoVault UI 存档的一次性迁移。
    /// <see cref="EnchantUI"/> 曾 new 遮蔽基类，数据只在旧条目；导入后删旧键防覆盖
    /// </summary>
    internal class LegacyUIDataImporter : ModSystem
    {
        private const string LegacyEntryKey = "SaveMod:UIDataSave";
        private string ModEntryKey => $"mod:{Mod.Name}";
        private string LegacySavePath => Path.Combine(VaultSave.RootPath, "ModDatas", $"mod_{Mod.Name}.nbt");

        public override void OnWorldLoad() {
            //MP 客户端不落盘，等 SP/服务器再迁
            if (VaultUtils.isClient) {
                return;
            }
            try {
                Import();
            } catch (Exception ex) {
                Mod.Logger.Error("LegacyUIDataImporter: import failed", ex);
            }
        }

        private void Import() {
            if (!SaveMod.TryLoadRootTag(LegacySavePath, out TagCompound rootTag)
                || !rootTag.TryGet(ModEntryKey, out TagCompound modTag)
                || !modTag.TryGet(LegacyEntryKey, out TagCompound flatTag)) {
                return;
            }

            if (flatTag.Count > 0) {
                //扁平键可直接喂各 UI 的 LoadUIData
                QuestLog.Instance.LoadUIData(flatTag);
                EntrustTrackerWidget.Instance.LoadUIData(flatTag);
                EnchantUI.Instance.LoadUIData(flatTag);
            }

            //只摘本条目，其余 SaveMod 原样写回
            modTag.Remove(LegacyEntryKey);
            if (modTag.Count == 0) {
                rootTag.Remove(ModEntryKey);
            }
            SaveMod.SaveTagToFile(rootTag, LegacySavePath);
        }
    }
}
