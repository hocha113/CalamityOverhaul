using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjs;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RDarklightGreatsword : CWRItemOverride
    {
        public override void SetDefaults(Item item) {
            item.UseSound = null;
            item.SetKnifeHeld<DarklightGreatswordHeld>();
        }
    }
}
