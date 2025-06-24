using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    internal class RDartRifle : CWRItemOverride
    {
        public override int TargetID => ItemID.DartRifle;
        public override bool IsVanilla => true;

        public override void SetDefaults(Item item) {
            item.SetCartridgeGun<DartRifleHeldProj>(40);
            item.damage = 50;
        }
    }
}
