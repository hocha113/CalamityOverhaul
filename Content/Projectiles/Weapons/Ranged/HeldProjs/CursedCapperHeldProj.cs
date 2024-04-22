﻿using CalamityOverhaul.Common;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using Terraria.Audio;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class CursedCapperHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "CursedCapper";
        public override int targetCayItem => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.CursedCapper>();
        public override int targetCWRItem => ModContent.ItemType<CursedCapperEcType>();
        public override void SetRangedProperty() {
            FireTime = 8;
            ControlForce = 0.1f;
            GunPressure = 0.2f;
            Recoil = 1.2f;
            HandDistance = 25;
            HandDistanceY = 5;
            HandFireDistance = 25;
            HandFireDistanceY = -8;
            LoadingAmmoAnimation = LoadingAmmoAnimationEnum.Handgun;
            LoadingAA_Handgun.feederOffsetRot = -18;
            LoadingAA_Handgun.loadingAmmoStarg_x = 1;
            LoadingAA_Handgun.loadingAmmoStarg_y = -9;
        }

        public override void FiringShoot() {
            Projectile.NewProjectile(Source, Projectile.Center, ShootVelocity, ProjectileID.CursedBullet, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            CaseEjection();
        }
    }
}
