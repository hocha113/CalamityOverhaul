using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RStarmada : CWRItemOverride
    {
        public override int TargetID => ModContent.ItemType<Starmada>();

        public override void SetDefaults(Item item) => item.SetCartridgeGun<StarmadaHeldProj>(180);
    }
}
