using CalamityMod;
using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using Mono.Cecil;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class PlagueTaintedSMGHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "PlagueTaintedSMG";
        public override int targetCayItem => ModContent.ItemType<PlagueTaintedSMG>();
        public override int targetCWRItem => ModContent.ItemType<PlagueTaintedSMGEcType>();

        public override void SetRangedProperty() {
            kreloadMaxTime = 90;
            FireTime = 10;
            HandDistance = 25;
            HandDistanceY = 5;
            HandFireDistance = 25;
            HandFireDistanceY = -10;
            ShootPosNorlLengValue = -8;
            ShootPosToMouLengValue = 30;
            RepeatedCartridgeChange = true;
            GunPressure = 0.1f;
            ControlForce = 0.03f;
            Recoil = 0.2f;
            RangeOfStress = 25;
            CanRightClick = true;
            FiringDefaultSound = false;
        }

        public override void PreInOwnerUpdate() {
            if (kreloadTimeValue > 0) {//设置一个特殊的装弹动作，调整转动角度和中心点，让枪身看起来上抬
                Owner.direction = ToMouse.X > 0 ? 1 : -1;//为了防止抽搐，这里额外设置一次玩家朝向
                FeederOffsetRot = -MathHelper.ToRadians(50) * DirSign;
                FeederOffsetPos = new Vector2(DirSign * -3, -25);
            }
        }

        public override void FiringShoot() {
            FireTime = 10;
            GunPressure = 0.1f;
            Recoil = 0.2f;
            SpawnGunFireDust();
            SoundEngine.PlaySound(Item.UseSound, Projectile.Center);
            OffsetPos -= ShootVelocity.UnitVector() * 4;
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }

        public override void FiringShootR() {
            FireTime = 45;
            GunPressure = 0.5f;
            Recoil = 1.2f;
            SoundEngine.PlaySound(SoundID.Item61, Projectile.Center);
            OffsetPos -= ShootVelocity.UnitVector() * 6;
            for (int i = 0; i < 3; i++) {
                Projectile.NewProjectile(Source, GunShootPos, ShootVelocity.RotatedBy(-0.15f * (i + 1))
                    , ModContent.ProjectileType<PlagueTaintedDrone>(), WeaponDamage, WeaponKnockback
                    , Owner.whoAmI, 1f, Owner.Calamity().alchFlask || Owner.Calamity().spiritOrigin ? 1f : 0f);
                Projectile.NewProjectile(Source, GunShootPos, ShootVelocity.RotatedBy(0.15f * (i + 1))
                    , ModContent.ProjectileType<PlagueTaintedDrone>(), WeaponDamage, WeaponKnockback
                    , Owner.whoAmI, 1f, Owner.Calamity().alchFlask || Owner.Calamity().spiritOrigin ? 1f : 0f);
            }
        }

        public override void PostFiringShoot() {
            base.PostFiringShoot();
            EjectCasing();
        }
    }
}
