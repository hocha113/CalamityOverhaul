using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class InfinityHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Infinity";
        public override int targetCayItem => ModContent.ItemType<Infinity>();
        public override int targetCWRItem => ModContent.ItemType<InfinityEcType>();

        public override void SetRangedProperty() {
            kreloadMaxTime = 90;
            FireTime = 2;
            HandDistance = 25;
            HandDistanceY = 5;
            HandFireDistance = 25;
            HandFireDistanceY = -10;
            ShootPosNorlLengValue = -8;
            ShootPosToMouLengValue = 30;
            RepeatedCartridgeChange = true;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0.1f;
            RangeOfStress = 25;
        }

        public override void PreInOwnerUpdate() {
            if (kreloadTimeValue > 0) {//设置一个特殊的装弹动作，调整转动角度和中心点，让枪身看起来上抬
                Owner.direction = ToMouse.X > 0 ? 1 : -1;//为了防止抽搐，这里额外设置一次玩家朝向
                FeederOffsetRot = -MathHelper.ToRadians(50) * DirSign;
                FeederOffsetPos = new Vector2(DirSign * -3, -25);
            }
        }

        public override void FiringShoot() {
            OffsetPos += ShootVelocity.UnitVector() * 3;
            float sengs = MathF.Sin(Time * 0.1f) * 0.2f;
            Projectile.NewProjectile(Source, Projectile.Center, ShootVelocity.RotatedBy(sengs), Item.shoot, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            Projectile.NewProjectile(Source, Projectile.Center, ShootVelocity.RotatedBy(-sengs), Item.shoot, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }

        public override void PostFiringShoot() {
            base.PostFiringShoot();
            EjectCasing();
        }
    }
}
