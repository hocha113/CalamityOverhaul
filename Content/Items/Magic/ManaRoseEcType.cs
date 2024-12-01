using CalamityMod.Items.Weapons.Magic;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic
{
    internal class ManaRoseEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Magic + "ManaRose";
        public override void SetDefaults() {
            Item.SetItemCopySD<ManaRose>();
            Item.SetHeldProj<ManaRoseHeld>();
        }
    }

    internal class RManaRose : BaseRItem
    {
        public override bool DrawingInfo => false;
        public override int TargetID => ModContent.ItemType<ManaRose>();
        public override int ProtogenesisID => ModContent.ItemType<ManaRoseEcType>();
        public override void SetDefaults(Item item) => item.SetHeldProj<ManaRoseHeld>();
    }

    internal class ManaRoseHeld : BaseMagicStaff<ManaRose>
    {
        public override void SetStaffProperty() => ShootPosToMouLengValue = 48;
    }
}
