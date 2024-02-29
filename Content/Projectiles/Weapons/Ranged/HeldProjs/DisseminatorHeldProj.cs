using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class DisseminatorHeldProj : BaseGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Disseminator";
        public override int targetCayItem => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.Disseminator>();
        public override int targetCWRItem => ModContent.ItemType<Disseminator>();
        public override void SetRangedProperty() {
            ShootPosToMouLengValue = 16;
            ShootPosNorlLengValue = -13;
            ControlForce = 0.1f;
            GunPressure = 0.2f;
            Recoil = 2;
            HandDistance = 20;
            HandDistanceY = 5;
            HandFireDistance = 20;
            HandFireDistanceY = -8;
            CanRightClick = true;//可以右键使用
        }

        public override void FiringIncident() {
            base.FiringIncident();
            if (onFireR) {
                GunPressure = 0.2f;
                Recoil = 1.1f;
                heldItem.useTime = 4;
            }
            else {
                GunPressure = 0.5f;
                Recoil = 2;
                heldItem.useTime = 24;
            }
        }

        public override void FiringShoot() {
            for (int i = 0; i < 4; i++) {
                Projectile.NewProjectile(Owner.parent(), GunShootPos,
                    ShootVelocity.RotatedBy(Main.rand.NextFloat(-0.1f, 0.1f)) * Main.rand.NextFloat(0.8f, 1.1f)
                    , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            }
            Vector2 pos = Owner.Center + new Vector2(MathHelper.Lerp(Main.MouseWorld.To(Owner.Center).X, 0, 1.3f), 780);
            Vector2 vr = pos.To(Main.MouseWorld).UnitVector() * ScaleFactor;
            for (int i = 0; i < 4; i++) {
                Vector2 vr2 = vr.RotatedBy(Main.rand.NextFloat(-0.1f, 0.1f)) * Main.rand.NextFloat(0.8f, 1.1f);
                int proj = Projectile.NewProjectile(Owner.parent(), pos, vr2, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                Main.projectile[proj].tileCollide = false;
            }  
            CaseEjection();
            _ = UpdateConsumeAmmo();
            _ = CreateRecoil();
        }

        public override void FiringShootR() {
            Projectile.NewProjectile(Owner.parent(), GunShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            Vector2 pos = Owner.Center + new Vector2(MathHelper.Lerp(Main.MouseWorld.To(Owner.Center).X, 0, 0.9f), -780);
            Vector2 vr = pos.To(Main.MouseWorld).UnitVector() * ScaleFactor;
            Projectile.NewProjectile(Owner.parent(), pos, vr, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            CaseEjection();
            _ = UpdateConsumeAmmo();
            _ = CreateRecoil();
        }
    }
}
