using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RAstralRepeater : CWRItemOverride
    {
        public override void SetDefaults(Item item) => item.SetHeldProj<AstralRepeaterHeldProj>();
    }
}
