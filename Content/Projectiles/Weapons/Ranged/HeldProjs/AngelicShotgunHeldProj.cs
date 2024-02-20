using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class AngelicShotgunHeldProj : BaseGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "AngelicShotgun";
        public override int targetCayItem => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.AngelicShotgun>();
        public override int targetCWRItem => ModContent.ItemType<AngelicShotgun>();
        public override void SetRangedProperty() {
            ControlForce = 0.1f;
            GunPressure = 0.3f;
            Recoil = 7;
            HandDistance = 28;
            HandDistanceY = 3;
        }

        public override void FiringIncident() {
            if (Owner.PressKey()) {
                Owner.direction = ToMouse.X > 0 ? 1 : -1;
                Projectile.rotation = GunOnFireRot;
                Projectile.Center = Owner.Center + Projectile.rotation.ToRotationVector2() * 30 + new Vector2(0, -10) + OffsetPos;
                ArmRotSengsBack = ArmRotSengsFront = (MathHelper.PiOver2 - Projectile.rotation) * DirSign;
                if (HaveAmmo) {
                    onFire = true;
                    Projectile.ai[1]++;
                }
            }
            else {
                onFire = false;
            }
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
            
            _ = UpdateConsumeAmmo();
            _ = CreateRecoil();
        }
    }
}
