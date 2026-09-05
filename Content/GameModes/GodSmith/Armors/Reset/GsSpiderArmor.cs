using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Reset
{
    /// <summary>
    /// 蜘蛛套：单件沿用原版；套装奖励为伤害 +15%、暴击 +15%、移速 +15%、召唤栏 +2，用魔时 40% 概率不消耗魔力
    /// </summary>
    internal class GsSpiderArmor : GsResetArmorScheme
    {
        public override int[] HeadIDs => [ItemID.SpiderMask];
        public override int BodyID => ItemID.SpiderBreastplate;
        public override int LegsID => ItemID.SpiderGreaves;
        public override bool OverridesPieceStats => false;
        protected override string SetBonusLineFallback =>
            "15% increased damage, critical strike chance and movement speed, +2 minion slots; 40% chance not to consume mana";

        public override void UpdateSetBonus(Player player, GodSmithArmorPlayer state) {
            player.GetDamage(DamageClass.Generic) += 0.15f;
            player.GetCritChance(DamageClass.Generic) += 15f;
            player.moveSpeed += 0.15f;
            player.maxMinions += 2;
        }

        public override void OnEndowConsumeMana(Player player, GodSmithArmorPlayer state, Item item, int manaConsumed) {
            //魔力归本地玩家权威，只在本端退还
            if (player.whoAmI != Main.myPlayer || manaConsumed <= 0 || Main.rand.Next(100) >= 40) {
                return;
            }
            player.statMana = Math.Min(player.statManaMax2, player.statMana + manaConsumed);
        }
    }
}
