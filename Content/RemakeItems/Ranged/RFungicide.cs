using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RFungicide : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<Fungicide>();

        public override void SetDefaults(Item item) {
            item.SetCartridgeGun<FungicideHeldProj>(16);
            item.damage = 22;
        }

    }
}
