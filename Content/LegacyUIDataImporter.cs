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
    /// 旧版 CWR 手动 UI 存档（扁平键，位于 mod_CalamityOverhaul.nbt 的 SaveMod:UIDataSave 条目）
    /// 到 InnoVault 自动 UI 存档管线的一次性迁移。QuestLog 等 override 型 UI 早已被 InnoVault 双写，
    /// 唯 <see cref="EnchantUI"/> 曾以 new 遮蔽基类方法，其数据（含炼铸槽内物品）只存在于旧条目中；
    /// 导入后立即删除旧条目，防止旧值反复覆盖 InnoVault 侧的新数据
    /// </summary>
    internal class LegacyUIDataImporter : ModSystem
    {
        private const string LegacyEntryKey = "SaveMod:UIDataSave";
        private string ModEntryKey => $"mod:{Mod.Name}";
        private string LegacySavePath => Path.Combine(VaultSave.RootPath, "ModDatas", $"mod_{Mod.Name}.nbt");

        public override void OnWorldLoad() {
            //MP客户端不触发世界存档，迁移后的内存值无法经 InnoVault 落盘，留待SP或服务器环境再迁移
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
                //旧格式把三个UI的键平铺在同一标签里，键名与各UI的LoadUIData期望一致，可整体喂入
                QuestLog.Instance.LoadUIData(flatTag);
                EntrustTrackerWidget.Instance.LoadUIData(flatTag);
                EnchantUI.Instance.LoadUIData(flatTag);
            }

            //同文件还承载 MenuSave 等其他 SaveMod 条目，只摘除本条目后原样写回
            modTag.Remove(LegacyEntryKey);
            if (modTag.Count == 0) {
                rootTag.Remove(ModEntryKey);
            }
            SaveMod.SaveTagToFile(rootTag, LegacySavePath);
        }
    }
}
