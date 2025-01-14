using CalamityMod.Items.Weapons.Magic;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic
{
    internal class RAquamarineStaff : BaseRItem
    {
        public override bool DrawingInfo => false;
        public override int TargetID => ModContent.ItemType<AquamarineStaff>();
        public override bool FormulaSubstitution => true;
        public override void SetDefaults(Item item) => item.SetHeldProj<AquamarineStaffHeld>();
    }

    internal class AquamarineStaffHeld : BaseMagicStaff<AquamarineStaff>
    {
        public override void SetStaffProperty() => ShootPosToMouLengValue = 34;
    }
}
