using CalamityMod.Items.Weapons.Typeless;
using CalamityOverhaul.Content.Items.Typeless;
using CalamityOverhaul.Content.Projectiles.Weapons.Typeless;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Typeless
{
    /// <summary>
    /// 马格努斯之眼
    /// </summary>
    internal class REyeofMagnus : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<EyeofMagnus>();
        public override int ProtogenesisID => ModContent.ItemType<EyeofMagnusEcType>();
        public override string TargetToolTipItemName => "EyeofMagnusEcType";
        public override void SetDefaults(Item item) => item.SetHeldProj<EyeofMagnusHeldProj>();
    }
}
