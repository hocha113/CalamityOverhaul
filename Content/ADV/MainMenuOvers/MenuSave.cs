using InnoVault.GameSystem;
using Terraria;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.ADV.MainMenuOvers
{
    internal class MenuSave : SaveMod
    {
        public static bool ADV_SupCal_EBN;
        public override void SetStaticDefaults() {
            if (!HasSave) {
                DoSave<MenuSave>();
            }
            DoLoad<MenuSave>();
        }

        public override void SaveData(TagCompound tag) {
            tag["ADV_SupCal_EBN"] = ADV_SupCal_EBN;
        }

        public override void LoadData(TagCompound tag) {
            if (!tag.TryGet("ADV_SupCal_EBN", out ADV_SupCal_EBN)) {
                ADV_SupCal_EBN = false;
            }
        }

        public static void SaveByADV(ADVSave save, Player player) {
            if (save.EternalBlazingNow) {
                ADV_SupCal_EBN = true;
                DoSave<MenuSave>();
            }
        }

        public static void LoadByADV(ADVSave save, Player player) {
            if (save.EternalBlazingNow) {
                ADV_SupCal_EBN = true;
                DoSave<MenuSave>();
            }
        }

        public static bool UsePortraitUI() {
            return ADV_SupCal_EBN;
        }
    }
}
