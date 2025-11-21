using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RMineralMortar : CWRItemOverride
    {
        public override void SetDefaults(Item item) => item.SetCartridgeGun<MineralMortarHeldProj>(8);
    }
}
