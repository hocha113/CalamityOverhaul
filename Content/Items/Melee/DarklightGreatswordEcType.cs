using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using Terraria;

namespace CalamityOverhaul.Content.Items.Melee
{
    internal class DarklightGreatswordEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "DarklightGreatsword";
        public override void SetDefaults() {
            Item.SetItemCopySD<DarklightGreatsword>();
            Item.SetKnifeHeld<DarklightGreatswordHeld>();
        }
    }
}
