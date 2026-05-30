using CalamityOverhaul.Content.LegendWeapon.TrialQuests;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend
{
    internal class SHPCData : LegendData
    {
        //六个改件槽位索引常量，0=BARREL 1=OPTIC 2=POWER 3=STOCK 4=GRIP 5=FRAME
        public const int SlotCount = 6;

        internal override IReadOnlyList<LegendTrialDefinition> TrialDefinitions => LegendTrialRouteCatalog.SHPCProgression;

        public override int TargetLevel => GetVersionedTrialTargetLevel();

        /// <summary>
        /// 从 Item 上的 LegendData 取出 SHPCData，找不到时返回 null
        /// </summary>
        public static SHPCData TryGet(Item item) {
            if (item == null || item.IsAir) {
                return null;
            }
            return item.CWR()?.LegendData as SHPCData;
        }

        /// <summary>
        /// 用于判断和标记历史物品
        /// </summary>
        /// <param name="item"></param>
        /// <param name="tag"></param>
        public static void IsLegacyItem(Item item, TagCompound tag) {
            //需要是曾经的SHPC
            if (item.type > ItemID.None && item.type == CWRID.Item_SHPC) {
                bool isOldSave = false;
                if (tag.ContainsKey("LegendData:Level")) {
                    isOldSave = true;
                }
                if (tag.ContainsKey("LegendData:UpgradeWorldName")) {
                    isOldSave = true;
                }
                if (tag.ContainsKey("LegendData:UpgradeWorldFullName")) {
                    isOldSave = true;
                }
                //标记为历史版本中存在过的传奇
                if (isOldSave) {
                    item.CWR().LegacyItemTranslationID = SHPCOverride.ID;
                }
            }
        }
    }
}
