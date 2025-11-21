using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RFlakKraken : CWRItemOverride
    {
        public override void SetDefaults(Item item) {
            item.damage = 84;
            item.SetCartridgeGun<FlakKrakenHeldProj>(80);
            item.CWR().Scope = true;
        }
    }
}
