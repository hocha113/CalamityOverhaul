using CalamityMod.Items.Weapons.Magic;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Magic
{
    internal class REidolicWail : CWRItemOverride
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
