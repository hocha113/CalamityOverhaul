using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    internal class RQuadBarrelShotgun : BaseRItem
    {
        public override int TargetID => ItemID.QuadBarrelShotgun;
        public override bool FormulaSubstitution => false;
        public override void On_PostSetDefaults(Item item) => item.SetHeldProj<QuadBarrelShotgunHeldProj>();
    }
}
