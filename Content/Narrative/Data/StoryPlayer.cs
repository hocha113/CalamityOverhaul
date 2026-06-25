using CalamityOverhaul.Content.Narrative.Data.Modules;
using CalamityOverhaul.Content.UIs.MainMenuOvers;
using InnoVault.DataModules;
using System;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Narrative.Data
{
    internal sealed class StoryPlayer : ModPlayer
    {
        public DataModuleStore StoryData { get; private set; } = new();

        /// <summary>本次读档是否已载入新格式（<c>StoryData</c>）数据；用于让旧档迁移垫片避免覆盖更权威的新数据</summary>
        public bool HasNewFormatData { get; private set; }

        public override void Initialize() {
            StoryData = new DataModuleStore();
            HasNewFormatData = false;
        }

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
            HasNewFormatData = false;
            try {
                //仅负责新格式；旧 ADVSavePlayer / 内嵌 ADCSave 的迁移分别由 ADVSavePlayer 垫片与 HalibutSave 触发
                if (tag.TryGet<TagCompound>("StoryData", out TagCompound storyTag)) {
                    StoryData.LoadData(storyTag);
                    HasNewFormatData = true;
                }
                ApplyMenuUnlocks();
            } catch (Exception ex) {
                CWRMod.Instance.Logger.Error("StoryPlayer.LoadData Error", ex);
                StoryData = new DataModuleStore();
            }
        }

        /// <summary>根据剧情进度解锁主菜单立绘等全局解锁项；读档与旧档迁移后均调用，保持与旧 ADVSavePlayer 行为一致</summary>
        internal void ApplyMenuUnlocks() {
            if (StoryData.Get<SupCalStoryData>().EternalBlazingNow) {
                MenuSave.UnlockEternalBlazingNowPortrait(Player);
            }
        }
    }
}
