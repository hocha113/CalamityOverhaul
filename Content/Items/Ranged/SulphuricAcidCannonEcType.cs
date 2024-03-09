using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class SulphuricAcidCannonEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "SulphuricAcidCannon";
        public override void SetDefaults() {
            Item.SetCalamitySD<SulphuricAcidCannon>();
            Item.ammo = AmmoID.Bullet;
            Item.SetCartridgeGun<SulphuricAcidCannonHeldProj>(55);
        }
    }
}
