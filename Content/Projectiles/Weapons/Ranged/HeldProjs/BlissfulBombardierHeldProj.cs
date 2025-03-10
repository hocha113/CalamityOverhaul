﻿using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class BlissfulBombardierHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "BlissfulBombardier";
        public override int TargetID => ModContent.ItemType<BlissfulBombardier>();
        public override void SetRangedProperty() {
            KreloadMaxTime = 130;
            FireTime = 12;
            HandIdleDistanceX = 24;
            HandIdleDistanceY = 2;
            HandFireDistanceX = 24;
            HandFireDistanceY = -2;
            ShootPosNorlLengValue = -10;
            ShootPosToMouLengValue = 30;
            RepeatedCartridgeChange = true;
            GunPressure = 0.15f;
            ControlForce = 0.03f;
            Recoil = 1.2f;
            RangeOfStress = 25;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 13;
            EjectCasingProjSize = 1.4f;
        }

        public override void FiringShoot() {
            Projectile.NewProjectile(Source, ShootPos, ShootVelocity, ModContent.ProjectileType<Nuke>()
                , WeaponDamage, WeaponKnockback, Owner.whoAmI, AmmoTypes);
        }
    }
}
