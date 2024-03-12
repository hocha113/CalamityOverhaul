using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using Terraria.ModLoader;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class SvantechnicalHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Svantechnical";
        public override int targetCayItem => ModContent.ItemType<Svantechnical>();
        public override int targetCWRItem => ModContent.ItemType<SvantechnicalEcType>();

        public override void SetRangedProperty() {
            kreloadMaxTime = 90;
            FireTime = 15;
            HandDistance = 25;
            HandDistanceY = 5;
            HandFireDistance = 25;
            HandFireDistanceY = -10;
            ShootPosNorlLengValue = 0;
            ShootPosToMouLengValue = 30;
            RepeatedCartridgeChange = true;
            GunPressure = 0.1f;
            ControlForce = 0.05f;
            Recoil = 1.2f;
            RangeOfStress = 25;
            AmmoTypeAffectedByMagazine = false;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 6;
        }

        public override void PreInOwnerUpdate() {
            if (kreloadTimeValue > 0) {//设置一个特殊的装弹动作，调整转动角度和中心点，让枪身看起来上抬
                Owner.direction = ToMouse.X > 0 ? 1 : -1;//为了防止抽搐，这里额外设置一次玩家朝向
                FeederOffsetRot = -MathHelper.ToRadians(50) * DirSign;
                FeederOffsetPos = new Vector2(DirSign * -3, -25);
            }
        }

        public override void PostInOwnerUpdate() {
            base.PostInOwnerUpdate();
        }

        public override void FiringShoot() {
            SpawnGunFireDust(GunShootPos, ShootVelocity, 1, dustID1: DustID.FireworkFountain_Blue, dustID2: DustID.FireworkFountain_Blue, dustID3: DustID.FireworkFountain_Blue);
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity * 1.1f, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity * 1.2f, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            Vector2 spanPos = GunShootPos + ShootVelocity.UnitVector() * GunShootPos.To(Main.MouseWorld).Length();
            for (int i = 0; i < 23; i++) {
                int proj = Projectile.NewProjectile(Source, spanPos, ShootVelocity.RotatedByRandom(0.12f), AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                Main.projectile[proj].timeLeft = 90;
                Main.projectile[proj].maxPenetrate *= 2;
                Main.projectile[proj].usesLocalNPCImmunity = true;
                Main.projectile[proj].localNPCHitCooldown = 5;
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
