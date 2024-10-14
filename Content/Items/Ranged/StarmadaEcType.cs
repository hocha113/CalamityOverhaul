using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class StarmadaEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Starmada";
        public override void SetDefaults() {
            Item.SetItemCopySD<Starmada>();
            Item.SetCartridgeGun<StarmadaHeldProj>(180);
        }
    }
}
