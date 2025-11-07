using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RAuralis : CWRItemOverride
    {
        public override void SetDefaults(Item item) {
            item.SetCartridgeGun<AuralisHeldProj>(18);
            item.CWR().Scope = true;
        }
    }
}
