using CalamityOverhaul.Content.LegendWeapon.TrialQuests;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend
{
    internal class HalibutData : LegendData
    {
        internal override IReadOnlyList<LegendTrialDefinition> TrialDefinitions => LegendTrialRouteCatalog.HalibutProgression;

        public override int TargetLevel => GetVersionedTrialTargetLevel();

        /// <summary>
        /// 获得武器成长等级
        /// </summary>
        public static int GetLevel() => GetLevel(Main.LocalPlayer.GetItem());

        /// <summary>
        /// 获得武器成长等级
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public static int GetLevel(Item item) {
            if (item.type != HalibutOverride.ID || !item.Alives()) {
                return 0;
            }
            CWRItem cwrItem = item.CWR();
            if (cwrItem == null) {
                return 0;
            }
            if (cwrItem.LegendData == null) {
                return 0;
            }

            return cwrItem.LegendData.Level;
        }

        /// <summary>
        /// 获得本地玩家的领域层数
        /// </summary>
        /// <returns></returns>
        public static int GetDomainLayer() => GetDomainLayer(Main.LocalPlayer);

        /// <summary>
        /// 用于判断和标记历史物品
        /// </summary>
        /// <param name="item"></param>
        /// <param name="tag"></param>
        public static void IsLegacyItem(Item item, TagCompound tag) {
            //需要是曾经的大比目鱼炮
            if (item.type > ItemID.None && item.type == CWRID.Item_HalibutCannon) {
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
                    item.CWR().LegacyItemTranslationID = HalibutOverride.ID;
                }
            }
        }

        /// <summary>
        /// 获得玩家的领域层数
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        public static int GetDomainLayer(Player player) {
            if (player.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                return (int)MathHelper.Max(halibutPlayer.SeaDomainLayers, 1);
            }
            return 1;
        }
    }
}
