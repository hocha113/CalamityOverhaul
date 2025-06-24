using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RUniversalGenesis : CWRItemOverride
    {
        public override int TargetID => ModContent.ItemType<UniversalGenesis>();
        public override void SetDefaults(Item item) => item.SetCartridgeGun<UniversalGenesisHeldProj>(80);
    }
}
