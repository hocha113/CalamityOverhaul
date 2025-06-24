using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RPridefulHuntersPlanarRipper : CWRItemOverride
    {
        public override int TargetID => ModContent.ItemType<PridefulHuntersPlanarRipper>();
        public override void SetDefaults(Item item) {
            item.damage = 45;
            item.SetCartridgeGun<PridefulHuntersPlanarRipperHeldProj>(280);
        }
    }
}
