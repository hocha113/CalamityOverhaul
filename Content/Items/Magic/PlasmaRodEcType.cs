using CalamityMod.Items.Weapons.Magic;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic
{
    internal class PlasmaRodEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Magic + "PlasmaRod";
        public override void SetDefaults() {
            Item.SetItemCopySD<PlasmaRod>();
            Item.SetHeldProj<PlasmaRodHeld>();
        }
    }

    internal class RPlasmaRod : BaseRItem
    {
        public override bool DrawingInfo => false;
        public override int TargetID => ModContent.ItemType<PlasmaRod>();
        public override int ProtogenesisID => ModContent.ItemType<PlasmaRodEcType>();
        public override void SetDefaults(Item item) => item.SetHeldProj<PlasmaRodHeld>();
    }

    internal class PlasmaRodHeld : BaseMagicStaff<PlasmaRod>
    {
        public override void SetStaffProperty() => ShootPosToMouLengValue = -30;
    }
}
