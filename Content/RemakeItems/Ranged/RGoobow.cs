using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RGoobow : CWRItemOverride
    {
        public override void SetDefaults(Item item) => item.SetHeldProj<GoobowHeldProj>();
    }
}
