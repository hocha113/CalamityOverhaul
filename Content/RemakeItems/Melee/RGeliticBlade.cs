using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RGeliticBlade : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<GeliticBlade>();
        public override void SetDefaults(Item item) => item.SetKnifeHeld<GeliticBladeHeld>();
    }
}
