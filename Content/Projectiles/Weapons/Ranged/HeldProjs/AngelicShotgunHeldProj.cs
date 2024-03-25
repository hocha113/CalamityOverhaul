using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class AngelicShotgunHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "AngelicShotgun";
        public override int targetCayItem => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.AngelicShotgun>();
        public override int targetCWRItem => ModContent.ItemType<AngelicShotgunEcType>();
        public override void SetRangedProperty() {
            FireTime = 18;
            EnableRecoilRetroEffect = true;
            ControlForce = 0.1f;
            GunPressure = 0.3f;
            Recoil = 2;
            HandDistance = 28;
            HandDistanceY = 3;
        }

        public override void PreInOwnerUpdate() {
            LoadingAnimation(50, 3, 25);
        }

        public override void FiringShoot() {
            if (AmmoTypes == ProjectileID.Bullet) {
                AmmoTypes = ModContent.ProjectileType<IlluminatedBullet>();
            }
            for (int i = 0; i < 6; i++) {
                int proj = Projectile.NewProjectile(Owner.parent(), Projectile.Center, ShootVelocity.RotatedBy(Main.rand.NextFloat(-0.1f, 0.1f))
                    , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                Main.projectile[proj].CWR().SpanTypes = (byte)SpanTypesEnum.AngelicShotgun;
            }
        }
    }
}
