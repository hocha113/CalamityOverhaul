using CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Quest.FindCampsites;
using CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Quest.Findfragments;
using CalamityOverhaul.Content.ADV.Scenarios.Draedons.Quest.DeploySignaltowers;
using CalamityOverhaul.Content.ADV.Scenarios.SupCal.End.EternalBlazingNows.Enchants;
using InnoVault.GameSystem;
using InnoVault.UIHandles;
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
                UIHandleLoader.GetUIHandleOfType<EnchantUI>().SaveUIData(tag);
                UIHandleLoader.GetUIHandleOfType<DeploySignaltowerTrackerUI>().SaveUIData(tag);
                UIHandleLoader.GetUIHandleOfType<FindCampsiteUI>().SaveUIData(tag);
                UIHandleLoader.GetUIHandleOfType<FindFragmentUI>().SaveUIData(tag);
            } catch { }
        }
        public override void LoadData(TagCompound tag) {
            try {
                UIHandleLoader.GetUIHandleOfType<EnchantUI>().LoadUIData(tag);
                UIHandleLoader.GetUIHandleOfType<DeploySignaltowerTrackerUI>().LoadUIData(tag);
                UIHandleLoader.GetUIHandleOfType<FindCampsiteUI>().LoadUIData(tag);
                UIHandleLoader.GetUIHandleOfType<FindFragmentUI>().LoadUIData(tag);
            } catch { }
        }
    }
}
