using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class FetidEmesisHeldProj : BaseGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "FetidEmesis";
        public override int targetCayItem => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.FetidEmesis>();
        public override int targetCWRItem => ModContent.ItemType<FetidEmesis>();
        public override void SetRangedProperty() {
            ControlForce = 0.06f;
            GunPressure = 0.2f;
            Recoil = 1.5f;
            HandDistance = 20;
            HandFireDistance = 20;
            HandFireDistanceY = -3;
        }

        public override void FiringIncident() {
            base.FiringIncident();
        }

        public override void FiringShoot() {
            Vector2 gundir = Projectile.rotation.ToRotationVector2();

            int proj = Projectile.NewProjectile(Owner.parent(), Projectile.Center + gundir * 3
                , ShootVelocityInProjRot.RotatedBy(Main.rand.NextFloat(-0.02f, 0.02f))
                , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            Main.projectile[proj].CWR().SpanTypes = (byte)SpanTypesEnum.FetidEmesis;

            if (++Projectile.ai[2] > 8) {
                Projectile.NewProjectile(Owner.parent(), Projectile.Center, Vector2.Zero
                , ModContent.ProjectileType<FetidEmesisOnSpan>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0, Projectile.whoAmI);
                Projectile.ai[2] = 0;
                _ = CreateRecoil();//执行两次，它会造成两倍的后坐力效果
            }

            _ = UpdateConsumeAmmo();
            _ = CreateRecoil();
        }
    }
}
