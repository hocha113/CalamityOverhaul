using CalamityMod.Items.Weapons.Magic;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic
{
    /// <summary>
    /// 海水杖
    /// </summary>
    internal class AquamarineStaffEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Magic + "AquamarineStaff";
        public override void SetDefaults() {
            Item.SetItemCopySD<AquamarineStaff>();
            Item.SetHeldProj<AquamarineStaffHeld>();
        }
    }

    internal class RAquamarineStaff : BaseRItem
    {
        public override bool DrawingInfo => false;
        public override int TargetID => ModContent.ItemType<AquamarineStaff>();
        public override int ProtogenesisID => ModContent.ItemType<AquamarineStaffEcType>();
        public override void SetDefaults(Item item) => item.SetHeldProj<AquamarineStaffHeld>();
    }

    internal class AquamarineStaffHeld : BaseMagicStaff<AquamarineStaff>
    {
        public override void SetStaffProperty() => ShootPosToMouLengValue = 34;
    }
}
