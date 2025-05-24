﻿using CalamityMod.Items.Weapons.Magic;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs
{
    internal class AcidGunHeldProj : BaseMagicGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Magic + "AcidGun";
        public override int TargetID => ModContent.ItemType<AcidGun>();
        public override void SetMagicProperty() {
            ShootPosToMouLengValue = -20;
            ShootPosNorlLengValue = 0;
            HandFireDistanceX = 16;
            HandFireDistanceY = -4;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0;
            Onehanded = true;
            InOwner_HandState_AlwaysSetInFireRoding = true;
        }

        public override void SetShootAttribute() {
            Item.useTime = 6;
            if (++fireIndex > 6) {
                Item.useTime = 45;
                fireIndex = 0;
            }
        }

        public override void FiringShoot() {
            Projectile.NewProjectile(Source, ShootPos, ShootVelocity.RotatedByRandom(0.2f)
                , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }
    }
}
