using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjs;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RSeashineSword : CWRItemOverride
    {
        public override void SetDefaults(Item item) => item.SetKnifeHeld<SeashineSwordHeld>();
    }
}
