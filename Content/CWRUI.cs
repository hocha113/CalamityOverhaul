using CalamityOverhaul.Content.ADV.ADVQuestTracker;
using CalamityOverhaul.Content.ADV.EntrustManager;
using CalamityOverhaul.Content.ADV.Scenarios.SupCal.End.EternalBlazingNows.Enchants;
using CalamityOverhaul.Content.QuestLogs;
using InnoVault.GameSystem;
using InnoVault.UIHandles;
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
                if (CWRRef.Has) {
                    EnchantUI.Instance.SaveUIData(temp);
                }
                QuestLog.Instance.SaveUIData(temp);
                EntrustTrackerWidget.Instance?.SaveUIData(temp);
                foreach (var ui in UIHandleLoader.UIHandles) {
                    if (ui is BaseQuestTrackerUI trackerUI) {
                        trackerUI.SaveUIData(temp);
                    }
                }

                foreach (var entry in temp) {
                    tag[entry.Key] = entry.Value;
                }
            } catch (Exception ex) {
                CWRMod.Instance.Logger.Error("UIDataSave.SaveData Error", ex);
            }
        }
        public override void LoadData(TagCompound tag) {
            try {
                if (CWRRef.Has) {
                    EnchantUI.Instance.LoadUIData(tag);
                }
                QuestLog.Instance.LoadUIData(tag);
                EntrustTrackerWidget.Instance?.LoadUIData(tag);
                foreach (var ui in UIHandleLoader.UIHandles) {
                    if (ui is BaseQuestTrackerUI trackerUI) {
                        trackerUI.LoadUIData(tag);
                    }
                }
            } catch (Exception ex) {
                CWRMod.Instance.Logger.Error("UIDataSave.LoadData Error", ex);
            }
        }
    }
}
