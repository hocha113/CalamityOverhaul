using CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Magic
{
    internal class RNanoPurge : CWRItemOverride
    {
        public override void SetDefaults(Item item) => item.SetHeldProj<NanoPurgeHeldProj>();
    }
}
