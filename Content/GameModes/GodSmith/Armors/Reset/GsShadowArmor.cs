using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Reset
{
    /// <summary>
    /// 暗影套（含远古暗影，三件可混搭）：头盔暴击 +15%，鳞甲移速 +5%，护胫移速 +10%；
    /// 套装奖励为所有武器攻速 +15%、召唤栏 +1
    /// </summary>
    internal class GsShadowArmor : GsResetArmorScheme
    {
        public override int[] HeadIDs => [ItemID.ShadowHelmet, ItemID.AncientShadowHelmet];
        public override int BodyID => ItemID.ShadowScalemail;
        public override int LegsID => ItemID.ShadowGreaves;
        public override int[] BodyIDs => [ItemID.ShadowScalemail, ItemID.AncientShadowScalemail];
        public override int[] LegsIDs => [ItemID.ShadowGreaves, ItemID.AncientShadowGreaves];

        protected override string HeadLineFallback => "15% increased critical strike chance";
        protected override string BodyLineFallback => "5% increased movement speed";
        protected override string LegsLineFallback => "10% increased movement speed";
        protected override string SetBonusLineFallback => "15% increased attack speed for all weapons; +1 minion slot";

        public override void UpdateHead(Player player, Item item) => player.GetCritChance(DamageClass.Generic) += 15f;

        public override void UpdateBody(Player player, Item item) => player.moveSpeed += 0.05f;

        public override void UpdateLegs(Player player, Item item) => player.moveSpeed += 0.10f;

        public override void UpdateSetBonus(Player player, GodSmithArmorPlayer state) {
            player.GetAttackSpeed(DamageClass.Generic) += 0.15f;
            player.maxMinions += 1;
        }
    }
}
