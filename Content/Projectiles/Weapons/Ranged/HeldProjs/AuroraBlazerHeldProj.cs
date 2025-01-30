using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class AuroraBlazerHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "AuroraBlazer";
        public override int TargetID => ModContent.ItemType<AuroraBlazer>();
        private int soundPma;
        public override void SetRangedProperty() {
            kreloadMaxTime = 90;
            HandIdleDistanceX = 25;
            HandIdleDistanceY = 5;
            HandFireDistanceX = 25;
            HandFireDistanceY = -10;
            ShootPosNorlLengValue = 0;
            ShootPosToMouLengValue = 0;
            RepeatedCartridgeChange = true;
            CanCreateCaseEjection = CanCreateSpawnGunDust = false;
            RangeOfStress = 25;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0.2f;
            FireTime = 5;
        }

        public override void HanderPlaySound() {
            soundPma++;
            if (soundPma > 5) {
                SoundEngine.PlaySound(Item.UseSound, Projectile.Center);
                soundPma = 0;
            }
        }

        public override void FiringShoot() {
            Projectile.NewProjectile(Source, ShootPos, ShootVelocity, Item.shoot
                , WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }
    }
}
