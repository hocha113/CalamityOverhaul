using CalamityOverhaul.Content.Narrative.Data.Modules;
using InnoVault.DataModules;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Narrative.Data
{
    internal static class LegacyStorySaveImporter
    {
        public static bool TryImport(TagCompound playerTag, DataModuleStore store) {
            if (playerTag.TryGet<TagCompound>("ADVSave", out TagCompound advTag)) {
                ImportADVSave(advTag, store);
                return true;
            }

            if (playerTag.TryGet<TagCompound>("ADCSave", out TagCompound adcTag)) {
                ImportADVSave(adcTag, store);
                return true;
            }

            return false;
        }

        private static void ImportADVSave(TagCompound legacyTag, DataModuleStore store) {
            ImportModule<HalibutStoryData>(legacyTag, "HalibutADVData", store);
            ImportModule<SupCalStoryData>(legacyTag, "SupCalADVData", store);
            ImportModule<DraedonStoryData>(legacyTag, "DraedonADVData", store);
            ImportModule<OldDukeStoryData>(legacyTag, "OldDukeADVData", store);
            ImportModule<BossGiftStoryData>(legacyTag, "BossGiftADVData", store);
            ImportModule<ShepelStoryData>(legacyTag, "ShepelADVData", store);
            ImportModule<ShepelGiftStoryData>(legacyTag, "ShepelGiftData", store);
            ImportModule<EntrustGuideData>(legacyTag, "EntrustGuideModule", store);
        }

        private static void ImportModule<T>(TagCompound source, string legacyKey, DataModuleStore store)
            where T : DataModule, new() {
            if (source.TryGet<TagCompound>(legacyKey, out TagCompound moduleTag)) {
                store.Get<T>().LoadData(moduleTag, loadedVersion: 0);
            }
        }
    }
}
