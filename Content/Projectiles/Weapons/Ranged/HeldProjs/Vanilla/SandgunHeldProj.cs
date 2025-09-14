﻿using CalamityOverhaul.Content.RangedModify.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    /// <summary>
    /// 沙枪
    /// </summary>
    internal class SandgunHeldProj : BaseGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.Sandgun].Value;
        public override int TargetID => ItemID.Sandgun;
        public override void SetRangedProperty() {
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandIdleDistanceX = 20;
            HandIdleDistanceY = 5;
            GunPressure = 0.2f;
            ControlForce = 0.05f;
            Recoil = 0.8f;
            CanRightClick = true;
            CanCreateCaseEjection = false;
            SpwanGunDustData.dustID1 = DustID.Sand;
            SpwanGunDustData.dustID2 = DustID.Sand;
        }

        public override void PostInOwner() {
            if (onFireR) {
                Item.useTime = 64;
                Recoil = 1.2f;
                RangeOfStress = 5;
            }
            else {
                Item.useTime = 16;
                Recoil = 0.6f;
                RangeOfStress = 5;
            }
        }

        public override void FiringShootR() {
            for (int i = 0; i < 6; i++) {
                Vector2 ver = ShootVelocity.RotatedBy(Main.rand.NextFloat(-0.12f, 0.12f)) * Main.rand.NextFloat(0.6f, 1.52f) * 0.3f;
                _ = Projectile.NewProjectile(Source2, ShootPos, ver, AmmoTypes, WeaponDamage, WeaponKnockback * 1.5f, Owner.whoAmI, 0);
                _ = UpdateConsumeAmmo();
            }
        }
    }
}
