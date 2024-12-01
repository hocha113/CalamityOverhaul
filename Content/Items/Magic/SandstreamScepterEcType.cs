using CalamityMod.Items.Weapons.Magic;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic
{
    internal class SandstreamScepterEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Magic + "SandstreamScepter";
        public override void SetDefaults() {
            Item.SetItemCopySD<SandstreamScepter>();
            Item.SetHeldProj<SandstreamScepterHeld>();
        }
    }

    internal class RSandstreamScepter : BaseRItem
    {
        public override bool DrawingInfo => false;
        public override int TargetID => ModContent.ItemType<SandstreamScepter>();
        public override int ProtogenesisID => ModContent.ItemType<SandstreamScepterEcType>();
        public override void SetDefaults(Item item) => item.SetHeldProj<SandstreamScepterHeld>();
    }

    internal class SandstreamScepterHeld : BaseMagicStaff<SandstreamScepter>
    {
        public override void SetStaffProperty() => ShootPosToMouLengValue = 48;
    }
}
