﻿using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend
{
    internal class HalibutData : LegendData
    {
        public override int TargetLevel => InWorldBossPhase.Halibut_Level();

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

        /// <summary>
        /// 获得本地玩家的领域层数
        /// </summary>
        /// <returns></returns>
        public static int GetDomainLayer() => GetDomainLayer(Main.LocalPlayer);

        /// <summary>
        /// 获得玩家的领域层数
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        public static int GetDomainLayer(Player player) {
            if (player.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                return halibutPlayer.SeaDomainLayers;
            }
            return 0;
        }
    }
}
