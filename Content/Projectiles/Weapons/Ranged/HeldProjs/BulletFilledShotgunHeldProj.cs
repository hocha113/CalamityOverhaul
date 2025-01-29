using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class BulletFilledShotgunHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "BulletFilledShotgun";
        public override int TargetID => ModContent.ItemType<BulletFilledShotgun>();
        public override void SetRangedProperty() {
            FireTime = 20;
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandIdleDistanceX = 17;
            HandIdleDistanceY = 4;
            ShootPosNorlLengValue = -6;
            ShootPosToMouLengValue = 15;
            GunPressure = 0.1f;
            ControlForce = 0.05f;
            Recoil = 0.8f;
            RangeOfStress = 28;
            kreloadMaxTime = 20;
            ArmRotSengsBackNoFireOffset = 30;
            RepeatedCartridgeChange = true;
            LoadingAmmoAnimation = LoadingAmmoAnimationEnum.Shotgun;
            if (!MagazineSystem) {
                FireTime += kreloadMaxTime;
            }
        }

        public override void FiringShoot() {
            int bulletAmt = Main.rand.Next(25, 35);
            for (int i = 0; i < bulletAmt; i++) {
                float newSpeedX = ShootVelocity.X + Main.rand.NextFloat(-15f, 15f);
                float newSpeedY = ShootVelocity.Y + Main.rand.NextFloat(-15f, 15f);
                int proj = Projectile.NewProjectile(Source, ShootPos, new Vector2(newSpeedX, newSpeedY), Item.shoot, WeaponDamage, WeaponKnockback, Owner.whoAmI);
                Main.projectile[proj].extraUpdates += 1;
            }
        }
    }
}
