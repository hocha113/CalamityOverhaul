using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    internal class RDartPistol : CWRItemOverride
    {
        public override int TargetID => ItemID.DartPistol;
        public override bool IsVanilla => true;

        public override void SetDefaults(Item item) {
            item.SetCartridgeGun<DartPistolHeldProj>(20);
            item.damage = 30;
        }
    }
}
