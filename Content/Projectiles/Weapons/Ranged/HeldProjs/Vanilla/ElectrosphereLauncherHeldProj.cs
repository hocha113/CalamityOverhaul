﻿using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class ElectrosphereLauncherHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.ElectrosphereLauncher].Value;
        public override int targetCayItem => ItemID.ElectrosphereLauncher;
        public override int targetCWRItem => ItemID.ElectrosphereLauncher;
        public override void SetRangedProperty() {
            FireTime = 48;
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandDistance = 15;
            HandDistanceY = 0;
            GunPressure = 0f;
            ControlForce = 0f;
            RepeatedCartridgeChange = true;
            Recoil = 0f;
            kreloadMaxTime = 60;
        }

        public override void PreInOwnerUpdate() {
            LoadingAnimation(30, 0, 13);
        }

        public override bool KreLoadFulfill() {
            return true;
        }

        public override void FiringShoot() {
            //火箭弹药特判，电圈特判
            Item ammoItem = Item.CWR().MagazineContents[0];
            AmmoTypes = ProjectileID.ElectrosphereMissile;
            for (int i = 0; i < 3; i++) {
                int proj = Projectile.NewProjectile(Source, GunShootPos, ShootVelocity.RotatedBy(MathHelper.Lerp(-0.2f, 0.2f, i / 2f)), AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                Main.projectile[proj].extraUpdates += 5;
            }
        }
    }
}