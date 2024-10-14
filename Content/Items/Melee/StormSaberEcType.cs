using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using Terraria;

namespace CalamityOverhaul.Content.Items.Melee
{
    internal class StormSaberEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "StormSaber";
        public override void SetDefaults() {
            Item.SetItemCopySD<StormSaber>();
            Item.SetKnifeHeld<StormSaberHeld>();
        }
    }
}
