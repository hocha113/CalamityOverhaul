using System;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Narrative.Data
{
    /// <summary>
    /// 旧档迁移垫片。类名/键 <c>ADVSave</c> 对齐重构前 ModPlayer，读档后交给
    /// <see cref="StoryPlayer"/> + <see cref="LegacyStorySaveImporter"/>。<br/>
    /// 只读不写（不重写 SaveData），再存档后旧条目自然消失
    /// </summary>
    internal sealed class ADVSavePlayer : ModPlayer
    {
        public override void LoadData(TagCompound tag) {
            //失败勿上抛，否则 PlayerIO 整档 CustomModDataException
            try {
                StoryPlayer storyPlayer = Player.GetModPlayer<StoryPlayer>();
                //新旧并存时新格式优先
                if (storyPlayer.HasNewFormatData) {
                    return;
                }
                if (LegacyStorySaveImporter.TryImport(tag, storyPlayer.StoryData)) {
                    storyPlayer.ApplyMenuUnlocks();
                }
            } catch (Exception ex) {
                CWRMod.Instance.Logger.Error("ADVSavePlayer legacy migration failed", ex);
            }
        }

        public override void SaveData(TagCompound tag) {

        }
    }
}
