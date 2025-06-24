using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RAtaraxia : CWRItemOverride
    {
        public override int TargetID => ModContent.ItemType<Ataraxia>();
        public override bool DrawingInfo => false;
        public override void SetDefaults(Item item) => item.SetKnifeHeld<AtaraxiaHeld>();
    }
}
