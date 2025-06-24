using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RDrataliornus : CWRItemOverride
    {
        public override int TargetID => ModContent.ItemType<Drataliornus>();
        public override void SetDefaults(Item item) {
            item.damage = 136;
            item.SetHeldProj<DrataliornusHeldProj>();
        }
    }
}
