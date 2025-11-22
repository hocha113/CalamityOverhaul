using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class DeepcoreGK2Held : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "DeepcoreGK2";
        public override void SetRangedProperty() {
            HandFireDistanceX = HandIdleDistanceX = 36;
            HandIdleDistanceY = -2;
            HandFireDistanceY = -4;
            ShootPosNorlLengValue = -18;
            ShootPosToMouLengValue = 28;
            AngleFirearmRest = -11;
            CanRightClick = true;
        }

        public override void HanderCaseEjection() {
            if (onFire) {
                CaseEjection(2);
            }
            if (onFireR) {
                CaseEjection();
            }
        }

        public override void HanderSpwanDust() {
            if (onFire) {
                SpawnGunFireDust(ShootPos, dustID1: DustID.YellowStarDust
                    , dustID2: DustID.FireworksRGB, dustID3: DustID.FireworksRGB);
            }
            if (onFireR) {
                SpawnGunFireDust(ShootPos + ShootVelocity, dustID1: DustID.YellowStarDust
                    , dustID2: DustID.FireworkFountain_Red, dustID3: DustID.FireworkFountain_Red);
            }
        }

        public override void SetShootAttribute() {
            if (onFire) {
                Recoil = 1.2f;
                FireTime = 16;
                Item.UseSound = SoundID.Item38;
            }
            if (onFireR) {
                Recoil = 0.3f;
                FireTime = 10;
                Item.UseSound = "CalamityMod/Sounds/Item/LargeWeaponFire".GetSound() with { Volume = 0.3f, Pitch = 0.2f };
            }
        }

        public override void FiringShoot() {
            int proj = Projectile.NewProjectile(Source, ShootPos, ShootVelocity
                , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            Main.projectile[proj].scale *= 3;
            Main.projectile[proj].extraUpdates += 1;
            Main.projectile[proj].SetDeepcoreBullet(true);
            Main.projectile[proj].ArmorPenetration = 10;
        }

        public override void FiringShootR() {
            Projectile.NewProjectile(Source, ShootPos, ShootVelocity, AmmoTypes
                , WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }
    }
}
