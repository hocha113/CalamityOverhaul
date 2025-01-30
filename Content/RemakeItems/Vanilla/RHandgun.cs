using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    internal class RHandgun : ItemOverride
    {
        public override int TargetID => ItemID.Handgun;
        public override bool FormulaSubstitution => false;
        public override bool IsVanilla => true;

        public override void SetDefaults(Item item) {
            item.damage = 20;
            item.SetCartridgeGun<HandgunHeldProj>(15);
        }
    }
}
