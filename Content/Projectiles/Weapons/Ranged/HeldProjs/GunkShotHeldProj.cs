﻿using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class GunkShotHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "GunkShot";
        public override int targetCayItem => ModContent.ItemType<GunkShot>();
        public override int targetCWRItem => ModContent.ItemType<GunkShotEcType>();

        public override void SetRangedProperty() {
            kreloadMaxTime = 90;
            FireTime = 25;
            HandDistance = 25;
            HandDistanceY = 5;
            HandFireDistance = 25;
            HandFireDistanceY = -10;
            ShootPosNorlLengValue = -12;
            ShootPosToMouLengValue = 30;
            RepeatedCartridgeChange = true;
            GunPressure = 0.3f;
            ControlForce = 0.05f;
            Recoil = 2.2f;
            RangeOfStress = 25;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 7;
        }

        public override void PreInOwnerUpdate() {
            LoadingAnimation(50, 3, 25);
        }

        public override void PostInOwnerUpdate() {
            base.PostInOwnerUpdate();
        }

        public override void FiringShoot() {
            SpawnGunFireDust(GunShootPos, ShootVelocity);
            for (int i = 0; i < 3; i++) {
                Projectile.NewProjectile(Source, GunShootPos, ShootVelocity.RotatedByRandom(0.1f), AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            }
        }

        public override void FiringShootR() {
            base.FiringShootR();
        }

        public override void PostFiringShoot() {
            base.PostFiringShoot();
        }
    }
}
