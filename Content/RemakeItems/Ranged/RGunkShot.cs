using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RGunkShot : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<GunkShot>();

        public override void SetDefaults(Item item) => item.SetCartridgeGun<GunkShotHeldProj>(10);
    }
}
