using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RHellkite : CWRItemOverride
    {
        public override int TargetID => ModContent.ItemType<Hellkite>();
        public override void SetDefaults(Item item) => item.SetKnifeHeld<HellkiteHeld>();
    }
}
