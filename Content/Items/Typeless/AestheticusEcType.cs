using CalamityMod.Items.Weapons.Typeless;
using CalamityOverhaul.Content.Projectiles.Weapons.Typeless;

namespace CalamityOverhaul.Content.Items.Typeless
{
    /// <summary>
    /// 美学魔杖
    /// </summary>
    internal class AestheticusEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Typeless + "Aestheticus";
        public override void SetDefaults() {
            Item.SetItemCopySD<Aestheticus>();
            Item.SetHeldProj<AestheticusHeldProj>();
        }
    }
}
