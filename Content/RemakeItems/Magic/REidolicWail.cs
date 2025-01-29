using CalamityMod.Items.Weapons.Magic;
using CalamityOverhaul.Content.Items.Magic;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Magic
{
    internal class REidolicWail : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<EidolicWail>();
        public override void SetDefaults(Item item) {
            item.useTime = 95;
            item.damage = 285;
            item.mana = 52;
            item.SetHeldProj<EidolicWailHeldProj>();
        }
    }
}
