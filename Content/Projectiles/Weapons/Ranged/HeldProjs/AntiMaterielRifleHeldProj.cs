﻿using CalamityMod.Items.Weapons.Ranged;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class AntiMaterielRifleHeldProj : TyrannysEndHeldProj
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "AntiMaterielRifle";
        public override int TargetID => ModContent.ItemType<AntiMaterielRifle>();
        public override void SetRangedProperty() {
            KreloadMaxTime = 120;
            FireTime = 60;
            ControlForce = 0.04f;
            GunPressure = 0.25f;
            Recoil = 3.5f;
            HandIdleDistanceX = 35;
            HandFireDistanceX = 30;
            HandFireDistanceY = -2;
            ShootPosToMouLengValue = 30;
            ShootPosNorlLengValue = -5;
            RangeOfStress = 25;
            SpwanGunDustData.splNum = 3.3f;
        }
        public override void FiringShoot() {
            Projectile.NewProjectile(Source, ShootPos, ShootVelocity
                    , ModContent.ProjectileType<BMGBullet>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0, 1);
        }
    }
}
