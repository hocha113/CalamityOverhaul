using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RDrataliornus : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<Drataliornus>();
        public override int ProtogenesisID => ModContent.ItemType<DrataliornusEcType>();
        public override string TargetToolTipItemName => "DrataliornusEcType";
        public override void SetDefaults(Item item) => item.SetHeldProj<DrataliornusHeldProj>();
    }
}
