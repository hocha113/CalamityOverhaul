using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RClaretCannon : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<ClaretCannon>();
        public override void SetDefaults(Item item) => item.SetCartridgeGun<ClaretCannonHeldProj>(48);
    }
}
