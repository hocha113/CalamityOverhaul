using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Reset
{
    /// <summary>仙人掌套：单件沿用原版（无属性），套装奖励改为移速 +15%</summary>
    internal class GsCactusArmor : GsResetArmorScheme
    {
        public override int[] HeadIDs => [ItemID.CactusHelmet];
        public override int BodyID => ItemID.CactusBreastplate;
        public override int LegsID => ItemID.CactusLeggings;
        public override bool OverridesPieceStats => false;
        protected override string SetBonusLineFallback => "15% increased movement speed";

        public override void UpdateSetBonus(Player player, GodSmithArmorPlayer state) => player.moveSpeed += 0.15f;
    }
}
