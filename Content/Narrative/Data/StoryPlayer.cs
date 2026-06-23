using InnoVault.DataModules;
using System;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Narrative.Data
{
    internal sealed class StoryPlayer : ModPlayer
    {
        public DataModuleStore StoryData { get; private set; } = new();

        public override void Initialize() => StoryData = new DataModuleStore();

        public T Get<T>() where T : DataModule, new() => StoryData.Get<T>();

        public override ModPlayer Clone(Terraria.Player newEntity) {
            StoryPlayer clone = (StoryPlayer)base.Clone(newEntity);
            clone.StoryData = StoryData?.Clone() ?? new DataModuleStore();
            return clone;
        }

        public override void SaveData(TagCompound tag) {
            try {
                TagCompound storyTag = [];
                StoryData.SaveData(storyTag);
                tag["StoryData"] = storyTag;
            } catch (Exception ex) {
                CWRMod.Instance.Logger.Error("StoryPlayer.SaveData Error", ex);
            }
        }

        public override void LoadData(TagCompound tag) {
            StoryData = new DataModuleStore();
            try {
                if (tag.TryGet<TagCompound>("StoryData", out TagCompound storyTag)) {
                    StoryData.LoadData(storyTag);
                    return;
                }

                LegacyStorySaveImporter.TryImport(tag, StoryData);
            } catch (Exception ex) {
                CWRMod.Instance.Logger.Error("StoryPlayer.LoadData Error", ex);
                StoryData = new DataModuleStore();
            }
        }
    }
}
