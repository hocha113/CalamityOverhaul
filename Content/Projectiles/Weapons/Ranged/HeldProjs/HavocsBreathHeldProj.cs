using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class HavocsBreathHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "HavocsBreath";
        public override int targetCayItem => ModContent.ItemType<HavocsBreath>();
        public override int targetCWRItem => ModContent.ItemType<HavocsBreathEcType>();

        private int fireIndex;
        public override void SetRangedProperty() {
            FireTime = 3;
            HandDistance = 25;
            HandDistanceY = 4;
            HandFireDistance = 25;
            HandFireDistanceY = -5;
            ShootPosNorlLengValue = -3;
            ShootPosToMouLengValue = 0;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0;
            RangeOfStress = 28;
            RepeatedCartridgeChange = true;
            FiringDefaultSound = false;
            kreloadMaxTime = 90;
            CanCreateCaseEjection = CanCreateSpawnGunDust = false;
            loadTheRounds = CWRSound.Liquids_Fill_0 with { Pitch = -0.8f };
            ForcedConversionTargetAmmoFunc = () => true;
            ToTargetAmmo = ModContent.ProjectileType<BrimstoneFireFriendly>();
            LoadingAA_None.Roting = 30;
            LoadingAA_None.gunBodyX = 0;
            LoadingAA_None.gunBodyY = 13;
        }

        public override void SetShootAttribute() {
            if (++fireIndex > 8) {
                SoundEngine.PlaySound(Item.UseSound, GunShootPos);
                fireIndex = 0;
            }
        }

        public override void FiringShoot() {
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity.RotatedByRandom(0.16f)
                , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity.RotatedByRandom(0.26f)
                * Main.rand.NextFloat(1.05f, 1.2f), AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }
    }
}
