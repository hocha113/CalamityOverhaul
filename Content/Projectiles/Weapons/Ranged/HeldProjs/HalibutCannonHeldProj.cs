using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class HalibutCannonHeldProj : BaseGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "HalibutCannon";
        public override int targetCayItem => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.HalibutCannon>();
        public override int targetCWRItem => ModContent.ItemType<HalibutCannonEcType>();

        private int level => HalibutCannonEcType.Level;
        public override void SetRangedProperty() {
            ControlForce = 0.05f;
            GunPressure = 0.2f;
            Recoil = 1.1f;
            HandDistance = 40;
            HandDistanceY = 8;
            HandFireDistance = 40;
            HandFireDistanceY = -3;
        }

        public override void FiringIncident() {
            base.FiringIncident();
        }

        private void Shoot(int num) {
            for (int i = 0; i < num; i++) {
                int proj14 = Projectile.NewProjectile(Source, GunShootPos
                    , ShootVelocity.RotatedBy(Main.rand.NextFloat(-0.03f, 0.03f)) * Main.rand.NextFloat(0.9f, 1.32f)
                    , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                Main.projectile[proj14].timeLeft = 90;
            }
        }

        public override void FiringShoot() {
            if (AmmoTypes == ProjectileID.Bullet) {
                AmmoTypes = ModContent.ProjectileType<TorrentialBullet>();
            }

            switch (level) {
                case 0:
                    int proj = Projectile.NewProjectile(Source, GunShootPos, ShootVelocity * Main.rand.NextFloat(0.9f, 1.32f)
                        , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                    Main.projectile[proj].timeLeft = 90;
                    break;
                case 1:
                case 2:
                case 3:
                    Shoot(2);
                    break;
                case 4:
                case 5:
                    Shoot(4);
                    break;
                case 6:
                    Shoot(6);
                    break;
                case 7:
                    Shoot(9);
                    break;
                case 8:
                    Shoot(11);
                    break;
                case 9:
                    Shoot(13);
                    break;
                case 10:
                    Shoot(15);
                    break;
                case 11:
                    Shoot(18);
                    break;
                case 12:
                    Shoot(22);
                    break;
                case 13:
                    Shoot(27);
                    break;
                case 14:
                    Shoot(33);
                    break;
            }

            _ = UpdateConsumeAmmo();
        }
    }
}
