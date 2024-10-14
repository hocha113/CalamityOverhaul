using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class SulphuricAcidCannonEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "SulphuricAcidCannon";
        public override void SetDefaults() {
            Item.SetItemCopySD<SulphuricAcidCannon>();
            Item.useAmmo = AmmoID.Bullet;
            Item.SetCartridgeGun<SulphuricAcidCannonHeldProj>(55);
        }
    }
}
