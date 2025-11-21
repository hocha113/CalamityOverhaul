using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RChickenCannon : CWRItemOverride
    {
        public override void SetDefaults(Item item) {
            item.damage = 220;
            item.useTime = 28;
            item.SetCartridgeGun<ChickenCannonHeldProj>(30);
        }
    }
}
