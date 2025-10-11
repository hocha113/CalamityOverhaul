using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend
{
    internal class HalibutData : LegendData
    {
<<<<<<< Updated upstream
        public override int TargetLevel => InWorldBossPhase.Halibut_Level();
=======
        public override int TargetLevle => InWorldBossPhase.Halibut_Level();
        public static int GetLevel() => GetLevel(Main.LocalPlayer.GetItem());

        /// <summary>
        /// 获得成长等级
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public static int GetLevel(Item item) {
            if (item.type != ModContent.ItemType<HalibutCannon>()) {
                return 0;
            }
            CWRItem cwrItem = item.CWR();
            if (cwrItem == null) {
                return 0;
            }
            if (cwrItem.LegendData == null) {
                return 0;
            }
            if (!CWRServerConfig.Instance.WeaponEnhancementSystem) {
                return 12;
            }
            return cwrItem.LegendData.Level;
        }
    }
}
