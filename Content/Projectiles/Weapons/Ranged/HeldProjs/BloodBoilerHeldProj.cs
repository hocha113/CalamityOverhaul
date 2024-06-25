using CalamityMod;
using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class BloodBoilerHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "BloodBoiler";
        public override int targetCayItem => ModContent.ItemType<BloodBoiler>();
        public override int targetCWRItem => ModContent.ItemType<BloodBoilerEcType>();

        private bool shotReturn = false;
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
            Recoil = 0.5f;
            RangeOfStress = 28;
            RepeatedCartridgeChange = true;
            FiringDefaultSound = false;
            kreloadMaxTime = 90;
            loadTheRounds = CWRSound.Liquids_Fill_0 with { Pitch = -0.8f };
            ForcedConversionTargetAmmoFunc = () => true;
            ToTargetAmmo = ModContent.ProjectileType<BloodBoilerFire>();
            LoadingAA_None.loadingAA_None_Roting = 30;
            LoadingAA_None.loadingAA_None_X = 0;
            LoadingAA_None.loadingAA_None_Y = 13;
        }

        public override void SetShootAttribute() {
            if (++fireIndex > 5) {
                SoundEngine.PlaySound(BloodBoiler.Heartbeat, GunShootPos);
                fireIndex = 0;
            }
            shotReturn = !shotReturn;
        }

        public override void FiringShoot() {
            if (Main.rand.NextFloat() > 0.60f) {
                Owner.statLife -= 1;
            }
            if (Owner.statLife <= 0) {
                PlayerDeathReason pdr = PlayerDeathReason.ByCustomReason(CalamityUtils.GetText("Status.Death.BloodBoiler" + Main.rand.Next(1, 2 + 1)).Format(Owner.name));
                Owner.KillMe(pdr, 1000.0, 0, false);
                return;
            }
            Vector2 newVel = ShootVelocity.RotatedBy(shotReturn ? 0.03f : -0.03f);
            Projectile.NewProjectile(Source, GunShootPos, newVel, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0, 0, shotReturn ? 5 : 0);
        }
    }
}
