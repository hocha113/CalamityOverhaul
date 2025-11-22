using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RArcherfish : CWRItemOverride
    {
        public override void SetDefaults(Item item) => item.SetCartridgeGun<ArcherfishHeld>(82);
    }
}
