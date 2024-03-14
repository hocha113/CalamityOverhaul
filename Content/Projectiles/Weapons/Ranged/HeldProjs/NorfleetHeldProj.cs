using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class NorfleetHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Norfleet";
        public override int targetCayItem => ModContent.ItemType<Norfleet>();
        public override int targetCWRItem => ModContent.ItemType<NorfleetEcType>();

        public override void SetRangedProperty() {
            kreloadMaxTime = 90;
            FireTime = 30;
            HandDistance = 0;
            HandDistanceY = -6;
            HandFireDistance = 0;
            HandFireDistanceY = -10;
            ShootPosNorlLengValue = -12;
            ShootPosToMouLengValue = 30;
            RepeatedCartridgeChange = true;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 1.2f;
            RangeOfStress = 25;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 22;
        }

        public override void PreInOwnerUpdate() {
            LoadingAnimation(50, -13, -15);
        }

        public override void PostInOwnerUpdate() {
            base.PostInOwnerUpdate();
        }

        public override void FiringShoot() {
            base.FiringShoot();
            Vector2 vr = ShootVelocity;
            AmmoTypes = ModContent.ProjectileType<NorfleetComet>();
            for (int i = 0; i < 6; i++) {
                vr.X += Main.rand.NextFloat(-1.5f, 1.5f);
                vr.Y += Main.rand.NextFloat(-1.5f, 1.5f);
                Projectile.NewProjectile(Source, GunShootPos, vr, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                vr.X += Main.rand.NextFloat(-1.5f, 1.5f);
                vr.Y += Main.rand.NextFloat(-1.5f, 1.5f);
                Projectile.NewProjectile(Source, GunShootPos, vr, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            }
            for (int i = 0; i < 300; i++) {
                Vector2 dustVel = (Projectile.rotation + Main.rand.NextFloat(-0.1f, 0.1f)).ToRotationVector2() * -Main.rand.Next(26, 117);
                float scale = Main.rand.NextFloat(0.5f, 1.5f);
                int type = DustID.YellowStarDust;
                Vector2 pos2 = GunShootPos + ShootVelocity.UnitVector() * -130;
                if (Main.rand.NextBool()) {
                    type = DustID.FireworkFountain_Yellow;
                    pos2 += dustVel * 6;
                }
                Dust.NewDust(pos2, 5, 5, type, dustVel.X, dustVel.Y, 0, default, scale);
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
