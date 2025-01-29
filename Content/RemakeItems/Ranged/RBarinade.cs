using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RBarinade : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<Barinade>();
        public override void SetDefaults(Item item) => item.SetHeldProj<BarinadeHeldProj>();
    }
}
