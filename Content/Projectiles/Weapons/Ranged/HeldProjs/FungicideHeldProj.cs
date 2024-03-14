using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using Terraria.ModLoader;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class FungicideHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Fungicide";
        public override int targetCayItem => ModContent.ItemType<Fungicide>();
        public override int targetCWRItem => ModContent.ItemType<FungicideEcType>();
        public override void SetRangedProperty() {
            FireTime = 30;
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandDistance = 17;
            HandDistanceY = 4;
            HandFireDistance = 15;
            ShootPosNorlLengValue = -10;
            ShootPosToMouLengValue = 15;
            GunPressure = 0.1f;
            ControlForce = 0.05f;
            Recoil = 0.5f;
            RangeOfStress = 28;
            RepeatedCartridgeChange = true;
            kreloadMaxTime = 60;
        }

        public override void PreInOwnerUpdate() {
            LoadingAnimation(30, 0, 13);
        }

        public override void FiringShoot() {
            SpawnGunFireDust(dustID1: DustID.BlueFairy, dustID2: DustID.BlueFairy, dustID3: DustID.BlueFairy);
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity, Item.shoot, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            _ = CreateRecoil();
        }
    }
}
