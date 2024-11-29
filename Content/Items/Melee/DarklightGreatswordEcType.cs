using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using Terraria;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 巨剑夜光
    /// </summary>
    internal class DarklightGreatswordEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "DarklightGreatsword";
        public override void SetDefaults() {
            Item.SetItemCopySD<DarklightGreatsword>();
            Item.UseSound = null;
            Item.SetKnifeHeld<DarklightGreatswordHeld>();
        }
    }
}
