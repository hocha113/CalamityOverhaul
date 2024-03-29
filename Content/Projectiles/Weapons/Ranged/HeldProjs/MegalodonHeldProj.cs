﻿using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class MegalodonHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Megalodon";
        public override int targetCayItem => ModContent.ItemType<Megalodon>();
        public override int targetCWRItem => ModContent.ItemType<MegalodonEcType>();
        int fireIndex;
        public override void SetRangedProperty() {
            kreloadMaxTime = 90;
            FireTime = 4;
            HandDistance = 22;
            HandDistanceY = 5;
            HandFireDistance = 22;
            HandFireDistanceY = -8;
            ShootPosNorlLengValue = -5;
            ShootPosToMouLengValue = 20;
            RepeatedCartridgeChange = true;
            MustConsumeAmmunition = false;
            GunPressure = 0.06f;
            ControlForce = 0.05f;
            Recoil = 0.22f;
            RangeOfStress = 25;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 4;
        }

        public override void PreInOwnerUpdate() {
            LoadingAnimation(50, 3, 25);
        }

        public override void FiringShoot() {
            SpawnGunFireDust();
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            if (fireIndex > 2) {
                int proj = Projectile.NewProjectile(Source, GunShootPos, ShootVelocity, ModContent.ProjectileType<MiniSharkron>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                Main.projectile[proj].MaxUpdates *= 2;
                fireIndex = 0;
            }
            fireIndex++;
        }

        public override void PostFiringShoot() {
            base.PostFiringShoot();
            EjectCasing();
        }
    }
}
