using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RAstralBlade : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<AstralBlade>();
        public override int ProtogenesisID => ModContent.ItemType<AstralBladeEcType>();
        public override string TargetToolTipItemName => "AstralBladeEcType";
        public override void SetDefaults(Item item) => AstralBladeEcType.SetDefaultsFunc(item);
        public override bool? On_ModifyWeaponCrit(Item item, Player player, ref float crit) {
            crit += 10;
            return false;
        }
    }
}
