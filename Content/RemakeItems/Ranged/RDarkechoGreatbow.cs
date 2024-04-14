using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.ModLoader;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RDarkechoGreatbow : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.DarkechoGreatbow>();
        public override int ProtogenesisID => ModContent.ItemType<DarkechoGreatbowEcType>();
        public override string TargetToolTipItemName => "DarkechoGreatbowEcType";
        public override void SetDefaults(Item item) => item.SetHeldProj<DarkechoGreatbowHeldProj>();
    }
}
