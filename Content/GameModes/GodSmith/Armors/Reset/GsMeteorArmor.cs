using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Reset
{
    /// <summary>
    /// 流星套：套装奖励为暴击 +20%、伤害 +25%、所有武器攻速 +60%、召唤栏 +3；
    /// 代价为最大生命 -300（不足则降至 1）、防御 -25、受到的伤害 +20%
    /// </summary>
    internal class GsMeteorArmor : GsResetArmorScheme
    {
        public override int[] HeadIDs => [ItemID.MeteorHelmet];
        public override int BodyID => ItemID.MeteorSuit;
        public override int LegsID => ItemID.MeteorLeggings;

        protected override string SetBonusLineFallback =>
            "20% increased critical strike chance, 25% increased damage, 60% increased attack speed for all weapons; +3 minion slots; 300 less maximum life (minimum 1), 25 less defense and 20% increased damage taken";

        public override void UpdateSetBonus(Player player, GodSmithArmorPlayer state) {
            player.GetCritChance(DamageClass.Generic) += 20f;
            player.GetDamage(DamageClass.Generic) += 0.25f;
            player.GetAttackSpeed(DamageClass.Generic) += 0.60f;
            player.maxMinions += 3;
            player.statLifeMax2 = Math.Max(1, player.statLifeMax2 - 300);
            player.statDefense -= 25;
        }

        public override void ModifyEndowHurt(Player player, GodSmithArmorPlayer state, ref Player.HurtModifiers modifiers) {
            modifiers.FinalDamage *= 1.2f;
        }
    }
}
