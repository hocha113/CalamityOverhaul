using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RBrimstoneFury : CWRItemOverride
    {
        public override void SetDefaults(Item item) => item.SetHeldProj<BrimstoneFuryHeldProj>();
    }
}
