using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RVortexpopper : CWRItemOverride
    {
        public override int TargetID => ModContent.ItemType<Vortexpopper>();
        public override void SetDefaults(Item item) => item.SetCartridgeGun<VortexpopperHeldProj>(85);
    }
}
