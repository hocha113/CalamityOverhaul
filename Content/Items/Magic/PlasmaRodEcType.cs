using CalamityMod.Items.Weapons.Magic;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic
{
    internal class RPlasmaRod : BaseRItem
    {
        public override bool DrawingInfo => false;
        public override int TargetID => ModContent.ItemType<PlasmaRod>();
        public override bool FormulaSubstitution => true;
        public override void SetDefaults(Item item) => item.SetHeldProj<PlasmaRodHeld>();
    }

    internal class PlasmaRodHeld : BaseMagicStaff<PlasmaRod>
    {
        public override void SetStaffProperty() => ShootPosToMouLengValue = -30;
    }
}
