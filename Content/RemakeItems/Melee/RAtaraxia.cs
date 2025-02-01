using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RAtaraxia : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<Ataraxia>();
        public override void SetDefaults(Item item) {
            item.damage = 405;
            item.SetKnifeHeld<AtaraxiaHeld>();
        }
    }
}
