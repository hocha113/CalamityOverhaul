using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class VoidragonHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Voidragon";
        public override int targetCayItem => ModContent.ItemType<Voidragon>();
        public override int targetCWRItem => ModContent.ItemType<VoidragonEcType>();
        int chargeIndex;
        public override void SetRangedProperty() {
            kreloadMaxTime = 90;
            FireTime = 5;
            HandDistance = 35;
            HandDistanceY = 5;
            HandFireDistance = 35;
            HandFireDistanceY = -6;
            ShootPosNorlLengValue = -8;
            ShootPosToMouLengValue = 30;
            RepeatedCartridgeChange = true;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0.2f;
            RangeOfStress = 25;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 5;
        }

        public override void PreInOwnerUpdate() {
            LoadingAnimation(30, 0, 13);
        }

        public override void PostInOwnerUpdate() {
            base.PostInOwnerUpdate();
        }

        public override void FiringShoot() {
            SpawnGunFireDust(GunShootPos, ShootVelocity, 1, 173, 173, 173);
            int proj = Projectile.NewProjectile(Owner.parent(), GunShootPos, ShootVelocity
                , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            Main.projectile[proj].CWR().SpanTypes = (byte)SpanTypesEnum.Voidragon;
            chargeIndex++;
            if (chargeIndex > 5) {
                SoundEngine.PlaySound(SoundID.Item92 with { MaxInstances = 100 }, Projectile.position);
                for (int i = 0; i < 23; i++) {
                    Projectile.NewProjectile(Owner.parent(), GunShootPos, ShootVelocity.RotatedByRandom(0.12f) * Main.rand.NextFloat(0.8f, 1.2f)
                        , ModContent.ProjectileType<VoidragonOrb>(), WeaponDamage / 2, WeaponKnockback, Owner.whoAmI, 0);
                }
                chargeIndex = 0;
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
