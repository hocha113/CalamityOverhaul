using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RVirulence : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<Virulence>();
        public override void SetDefaults(Item item) => item.SetKnifeHeld<VirulenceHeld>();
    }
}
