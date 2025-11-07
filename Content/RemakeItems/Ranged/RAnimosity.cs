using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RAnimosity : CWRItemOverride
    {
        public override void SetDefaults(Item item) => item.SetCartridgeGun<AnimosityHeldProj>(55);
    }
}
