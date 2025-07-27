using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    internal class RHandgun : CWRItemOverride
    {
        public override int TargetID => ItemID.Handgun;

        public override bool IsVanilla => true;

        public override void SetDefaults(Item item) {
            item.damage = 20;
            item.SetCartridgeGun<HandgunHeldProj>(15);
        }
    }
}
