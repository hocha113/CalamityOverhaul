using CalamityMod.Items.Weapons.Magic;
using CalamityOverhaul.Content.Items.Magic;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Magic
{
    internal class RWingman : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<Wingman>();
        public override int ProtogenesisID => ModContent.ItemType<WingmanEcType>();
        public override string TargetToolTipItemName => "WingmanEcType";
        public override bool CanLoad() => false;//TODO:在当前版本暂时移除
        public override void SetDefaults(Item item) => item.SetHeldProj<WingmanHeldProj>();
    }
}
