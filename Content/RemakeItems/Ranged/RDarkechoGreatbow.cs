using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RDarkechoGreatbow : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<DarkechoGreatbow>();
        public override int ProtogenesisID => ModContent.ItemType<DarkechoGreatbowEcType>();
        public override string TargetToolTipItemName => "DarkechoGreatbowEcType";
        public override void SetDefaults(Item item) => item.SetHeldProj<DarkechoGreatbowHeldProj>();
    }
}
