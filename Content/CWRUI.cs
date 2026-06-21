using CalamityOverhaul.Content.EntrustManager;
using CalamityOverhaul.Content.QuestLogs;
using CalamityOverhaul.Content.Scenarios.SupCal.End.EternalBlazingNow.Enchants;
using InnoVault.GameSystem;
using System;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content
{
    public class CWRUI : ModSystem
    {
        public static bool DontSetHoverItem;
        public static Item HoverItem = new Item();

        public override void PreUpdateEntities() {
            if (!DontSetHoverItem) {
                HoverItem = Main.HoverItem;
            }
            DontSetHoverItem = false;
        }

        public override void LoadWorldData(TagCompound tag) {
            tag.TryGet("placeholder", out bool _);
            SaveMod.DoLoad<UIDataSave>();
        }

        public override void SaveWorldData(TagCompound tag) {
            tag["placeholder"] = false;
            SaveMod.DoSave<UIDataSave>();
        }
    }

    internal class UIDataSave : SaveMod
    {
        public override void SaveData(TagCompound tag) {
            try {
                TagCompound temp = [];
                QuestLog.Instance.SaveUIData(temp);
                EntrustTrackerWidget.Instance?.SaveUIData(temp);
                EnchantUI.Instance?.SaveUIData(temp);

                foreach (var entry in temp) {
                    tag[entry.Key] = entry.Value;
                }
            } catch (Exception ex) {
                CWRMod.Instance.Logger.Error("UIDataSave.SaveData Error", ex);
            }
        }

        public override void LoadData(TagCompound tag) {
            try {
                QuestLog.Instance.LoadUIData(tag);
                EntrustTrackerWidget.Instance?.LoadUIData(tag);
                EnchantUI.Instance?.LoadUIData(tag);
            } catch (Exception ex) {
                CWRMod.Instance.Logger.Error("UIDataSave.LoadData Error", ex);
            }
        }
    }
}
