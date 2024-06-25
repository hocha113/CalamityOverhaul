using CalamityMod;
using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Sounds;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class DeepcoreGK2HeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "DeepcoreGK2";
        public override int targetCayItem => ModContent.ItemType<DeepcoreGK2>();
        public override int targetCWRItem => ModContent.ItemType<DeepcoreGK2EcType>();
        public override void SetRangedProperty() {
            HandFireDistance = HandDistance = 36;
            HandDistanceY = -2;
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
                SpawnGunFireDust(GunShootPos, dustID1: DustID.YellowStarDust
                    , dustID2: DustID.FireworksRGB, dustID3: DustID.FireworksRGB);
            }
            if (onFireR) {
                SpawnGunFireDust(GunShootPos + ShootVelocity, dustID1: DustID.YellowStarDust
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
                Item.UseSound = CommonCalamitySounds.LargeWeaponFireSound with { Volume = 0.3f, Pitch = 0.2f };
            }
        }

        public override void FiringShoot() {
            int proj = Projectile.NewProjectile(Source, GunShootPos, ShootVelocity
                , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            Main.projectile[proj].scale *= 3;
            Main.projectile[proj].extraUpdates += 1;
            Main.projectile[proj].Calamity().deepcoreBullet = true;
            Main.projectile[proj].ArmorPenetration = 10;
        }

        public override void FiringShootR() {
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity, AmmoTypes
                , WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }
    }
}
