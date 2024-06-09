using CalamityMod;
using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class EternalBlizzardHeldProj : BaseGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "EternalBlizzard";
        public override int targetCayItem => ModContent.ItemType<EternalBlizzard>();
        public override int targetCWRItem => ModContent.ItemType<EternalBlizzardEcType>();

        private int fireIndex;
        public override void SetRangedProperty() {
            HandDistance = 30;
            HandDistanceY = 6;
            HandFireDistance = 30;
            ShootPosToMouLengValue = 10;
            ShootPosNorlLengValue = -5;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0;
            IsCrossbow = true;
            CanRightClick = true;
            DrawCrossArrowNorlMode = 3;
            ForcedConversionTargetAmmoFunc = () => true;
            ToTargetAmmo = ModContent.ProjectileType<IcicleArrowProj>();
        }

        public override void SetShootAttribute() {
            Item.useTime = 10;
        }

        public override void FiringShoot() {
            for (int i = 0; i < fireIndex + 1; i++) {
                int proj = Projectile.NewProjectile(Source, GunShootPos, ShootVelocity.RotatedByRandom(0.12f)
                    , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                Main.projectile[proj].SetArrowRot();
                if (Main.rand.NextBool(2)) {
                    Main.projectile[proj].damage /= 2;
                }
            }

            _ = UpdateConsumeAmmo();
        }

        public override void PostFiringShoot() {
            if (onFire) {
                if (++fireIndex > 3) {
                    Item.useTime = 36;
                    fireIndex = 0;
                }
            }
        }

        public override void FiringShootR() {
            int proj = Projectile.NewProjectile(Source, GunShootPos, ShootVelocity
                , AmmoTypes, (int)(WeaponDamage * 0.6f), WeaponKnockback, Owner.whoAmI, 0);
            Main.projectile[proj].SetArrowRot();
            Main.projectile[proj].Calamity().allProjectilesHome = true;
            Main.projectile[proj].ArmorPenetration = 5;
            Main.projectile[proj].netUpdate = true;
            _ = UpdateConsumeAmmo();
        }
    }
}
