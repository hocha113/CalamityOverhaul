using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RChickenCannon : CWRItemOverride
    {
        public override int TargetID => ModContent.ItemType<ChickenCannon>();
        public override void SetDefaults(Item item) {
            item.damage = 220;
            item.useTime = 28;
            item.SetCartridgeGun<ChickenCannonHeldProj>(30);
        }
    }
}
