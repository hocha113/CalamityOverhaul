using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RStellarCannon : CWRItemOverride
    {
        public override void SetDefaults(Item item) {
            item.damage = 115;
            item.useAmmo = AmmoID.FallenStar;
            item.SetCartridgeGun<StellarCannonHeldProj>(50);
        }
    }
}
