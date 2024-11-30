using CalamityMod.Items.Weapons.Typeless;
using CalamityOverhaul.Content.Items.Typeless;
using CalamityOverhaul.Content.Projectiles.Weapons.Typeless;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Typeless
{
    /// <summary>
    /// 星光之眼
    /// </summary>
    internal class RLunicEye : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<LunicEye>();
        public override int ProtogenesisID => ModContent.ItemType<LunicEyeEcType>();
        public override string TargetToolTipItemName => "LunicEyeEcType";
        public override void SetDefaults(Item item) => item.SetHeldProj<LunicEyeHeldProj>();
    }
}
