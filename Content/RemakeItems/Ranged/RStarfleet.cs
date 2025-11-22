using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RStarfleet : CWRItemOverride
    {
        public override void SetDefaults(Item item) => item.SetCartridgeGun<StarfleetHeld>(60);
    }
}
