using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjs;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RForbiddenOathblade : CWRItemOverride
    {
        public override void SetDefaults(Item item) => item.SetKnifeHeld<ForbiddenOathbladeHeld>();
    }
}
