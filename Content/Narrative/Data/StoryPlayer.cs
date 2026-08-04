using CalamityOverhaul.Content.MainMenus.Characters;
using CalamityOverhaul.Content.Narrative.Data.Modules;
using InnoVault.DataModules;
using System;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Narrative.Data
{
    internal sealed class StoryPlayer : ModPlayer
    {
        public DataModuleStore StoryData { get; private set; } = new();

        /// <summary>本档已有新格式 <c>StoryData</c>；旧档垫片勿覆盖</summary>
        public bool HasNewFormatData { get; private set; }

        internal string HimayoGiftDelayKey { get; set; }
        internal int HimayoGiftDelayTicks { get; set; }

        public override void Initialize() {
            StoryData = new DataModuleStore();
            HasNewFormatData = false;
            HimayoGiftDelayKey = null;
            HimayoGiftDelayTicks = 0;
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
            HimayoGiftDelayKey = null;
            HimayoGiftDelayTicks = 0;
            try {
                //只读新格式；旧档走 ADVSavePlayer / HalibutSave
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

        /// <summary>剧情进度 → 主菜单立绘等；读档与旧档迁移后调用</summary>
        internal void ApplyMenuUnlocks() {
            if (StoryData.Get<SupCalStoryData>().EternalBlazingNow) {
                MenuSave.UnlockEternalBlazingNowPortrait(Player);
            }
        }
    }
}
