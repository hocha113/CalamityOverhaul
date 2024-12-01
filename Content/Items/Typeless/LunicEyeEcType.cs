using CalamityMod.Items.Weapons.Typeless;
using CalamityOverhaul.Content.Projectiles.Weapons.Typeless;

namespace CalamityOverhaul.Content.Items.Typeless
{
    /// <summary>
    /// 星光之眼
    /// </summary>
    internal class LunicEyeEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Typeless + "LunicEye";
        public override void SetDefaults() {
            Item.SetItemCopySD<LunicEye>();
            Item.SetHeldProj<LunicEyeHeldProj>();
        }
    }
}
