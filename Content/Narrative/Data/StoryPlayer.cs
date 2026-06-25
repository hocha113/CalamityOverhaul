using CalamityOverhaul.Content.Narrative.Data.Modules;
using CalamityOverhaul.Content.UIs.MainMenuOvers;
using InnoVault.DataModules;
using System;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Narrative.Data
{
    //旧 ADV 存档存于名为 "ADVSavePlayer" 的 ModPlayer。重构改名后必须声明旧名，
    //否则 tModLoader 按 (mod, name) 匹配不到本类，旧数据会落入 UnloadedPlayer 而丢失
    [LegacyName("ADVSavePlayer")]
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
                //新格式直接读取；否则尝试迁移旧 ADVSavePlayer / 内嵌 ADCSave 存档
                if (tag.TryGet<TagCompound>("StoryData", out TagCompound storyTag)) {
                    StoryData.LoadData(storyTag);
                }
                else {
                    LegacyStorySaveImporter.TryImport(tag, StoryData);
                }

                //与旧 ADVSavePlayer 行为一致：永燃之刻结局达成则解锁主菜单立绘
                if (StoryData.Get<SupCalStoryData>().EternalBlazingNow) {
                    MenuSave.UnlockEternalBlazingNowPortrait(Player);
                }
            } catch (Exception ex) {
                CWRMod.Instance.Logger.Error("StoryPlayer.LoadData Error", ex);
                StoryData = new DataModuleStore();
            }
        }
    }
}
