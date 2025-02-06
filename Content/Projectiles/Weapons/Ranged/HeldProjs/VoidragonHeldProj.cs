using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class VoidragonHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Voidragon";
        public override int TargetID => ModContent.ItemType<Voidragon>();
        private int chargeIndex;
        public override void SetRangedProperty() {
            kreloadMaxTime = 90;
            FireTime = 6;
            HandIdleDistanceX = 35;
            HandIdleDistanceY = 5;
            HandFireDistanceX = 35;
            HandFireDistanceY = -6;
            ShootPosNorlLengValue = -8;
            ShootPosToMouLengValue = 30;
            RepeatedCartridgeChange = true;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0.5f;
            RangeOfStress = 25;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 5;
            SpwanGunDustMngsData.dustID1 = 173;
            SpwanGunDustMngsData.dustID2 = 173;
            SpwanGunDustMngsData.dustID3 = 173;
            LoadingAA_None.Roting = 30;
            LoadingAA_None.gunBodyX = 0;
            LoadingAA_None.gunBodyY = 13;
        }

        public override void FiringShoot() {
            Recoil = 0.5f;
            GunPressure = 0;
            ControlForce = 0.03f;
            int proj = Projectile.NewProjectile(Source, ShootPos, ShootVelocity
                , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            Main.projectile[proj].CWR().SpanTypes = (byte)SpanTypesEnum.Voidragon;
            chargeIndex++;
            if (chargeIndex > 6) {
                if (BulletNum <= 5) {
                    Recoil = 0.5f;
                    GunPressure = 0;
                    ControlForce = 0.03f;
                    chargeIndex = 0;
                    return;
                }
                Recoil = 2.5f;
                GunPressure = 0.12f;
                ControlForce = 0.03f;
                SoundEngine.PlaySound(SoundID.Item92 with { MaxInstances = 100 }, Projectile.position);
                for (int i = 0; i < 20; i++) {
                    Projectile.NewProjectile(Source, ShootPos, ShootVelocity.RotatedByRandom(0.12f) * Main.rand.NextFloat(0.8f, 1.2f)
                        , ModContent.ProjectileType<VoidragonOrb>(), WeaponDamage / 3, WeaponKnockback, Owner.whoAmI, 0);
                }
                for (int i = 0; i < 5; i++) {
                    UpdateMagazineContents();
                }
                chargeIndex = 0;
            }
        }
    }
}
