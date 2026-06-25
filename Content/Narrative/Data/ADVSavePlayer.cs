using System;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Narrative.Data
{
    /// <summary>
    /// 旧存档迁移垫片：重构前 ADV 数据保存在名为 <c>ADVSavePlayer</c> 的 ModPlayer 下（键 <c>ADVSave</c>）。<br/>
    /// 本类沿用同一类名，从而被 tModLoader 按 <c>(mod, name)</c> 匹配到旧存档条目，
    /// 读取后转交给 <see cref="StoryPlayer"/>，再由 <see cref="LegacyStorySaveImporter"/> 写入新的数据模块。<br/>
    /// <b>本类只读不写</b>：不重写 <see cref="ModPlayer.SaveData"/>，因此再次存档时不会写出 <c>ADVSavePlayer</c> 条目，
    /// 旧条目随之自然消失，迁移即一次性完成；新存档不含本条目时本钩子不会被调用，故对新档零影响
    /// </summary>
    internal sealed class ADVSavePlayer : ModPlayer
    {
        public override void LoadData(TagCompound tag) {
            //迁移失败不得向上抛出，否则 PlayerIO 会包装成 CustomModDataException 影响整个玩家读档
            try {
                StoryPlayer storyPlayer = Player.GetModPlayer<StoryPlayer>();
                //正常情况下新旧条目互斥；若存档异常地同时含两者，新格式更权威，跳过迁移以免回退数据
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
    }
}
