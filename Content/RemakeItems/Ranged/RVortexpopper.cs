using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RVortexpopper : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<Vortexpopper>();
        public override int ProtogenesisID => ModContent.ItemType<VortexpopperEcType>();
        public override string TargetToolTipItemName => "VortexpopperEcType";
        public override void SetDefaults(Item item) => item.SetCartridgeGun<VortexpopperHeldProj>(85);
    }
}
