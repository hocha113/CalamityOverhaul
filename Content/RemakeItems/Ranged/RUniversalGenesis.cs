using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RUniversalGenesis : CWRItemOverride
    {
        public override void SetDefaults(Item item) => item.SetCartridgeGun<UniversalGenesisHeld>(80);
    }
}
