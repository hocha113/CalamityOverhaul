using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using Terraria.ModLoader;
using Terraria;
using Terraria.Audio;
using CalamityMod.Projectiles.Ranged;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class AcesHighHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "AcesHigh";
        public override int targetCayItem => ModContent.ItemType<AcesHigh>();
        public override int targetCWRItem => ModContent.ItemType<AcesHighEcType>();
        int fireIndex;
        public override void SetRangedProperty() {
            kreloadMaxTime = 90;
            FireTime = 20;
            HandDistance = 25;
            HandDistanceY = 5;
            HandFireDistance = 25;
            HandFireDistanceY = -5;
            ShootPosNorlLengValue = -2;
            ShootPosToMouLengValue = 10;
            RepeatedCartridgeChange = true;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 1.2f;
            RangeOfStress = 25;
            AmmoTypeAffectedByMagazine = false;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 7;
            FiringDefaultSound = false;
        }

        public override void PreInOwnerUpdate() {
            LoadingAnimation(50, 3, 25);
        }

        public override void PostInOwnerUpdate() {
            base.PostInOwnerUpdate();
        }

        public override void FiringShoot() {
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0;
            RecoilRetroForceMagnitude = 0;
            FireTime = 1;
            if (fireIndex > 3) {
                Recoil = 1.2f;
                RecoilRetroForceMagnitude = 7;
                FireTime = 15;
                fireIndex = 0;
                SoundEngine.PlaySound(Item.UseSound, Projectile.Center);
            }
            AmmoTypes = Utils.SelectRandom(Main.rand, new int[]
            {
                ModContent.ProjectileType<CardHeart>(),
                ModContent.ProjectileType<CardSpade>(),
                ModContent.ProjectileType<CardDiamond>(),
                ModContent.ProjectileType<CardClub>()
            });
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            fireIndex++;
        }

        public override void FiringShootR() {
            base.FiringShootR();
        }

        public override void PostFiringShoot() {
            base.PostFiringShoot();
        }
    }
}
