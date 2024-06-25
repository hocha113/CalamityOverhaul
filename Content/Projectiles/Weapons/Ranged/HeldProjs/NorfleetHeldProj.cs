using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
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
            kreloadMaxTime = 110;
            FireTime = 30;
            HandDistance = 0;
            HandDistanceY = -6;
            HandFireDistance = 0;
            HandFireDistanceY = -10;
            ShootPosNorlLengValue = -2;
            ShootPosToMouLengValue = 30;
            RepeatedCartridgeChange = true;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 1.2f;
            RangeOfStress = 25;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 22;
            LoadingAA_None.loadingAA_None_Roting = -50;
            LoadingAA_None.loadingAA_None_X = -13;
            LoadingAA_None.loadingAA_None_Y = -15;
        }

        public override void FiringShoot() {
            base.FiringShoot();
            Vector2 vr = ShootVelocity;
            AmmoTypes = ModContent.ProjectileType<NorfleetComet>();
            for (int i = 0; i < 3; i++) {
                vr.X += Main.rand.NextFloat(-1.5f, 1.5f);
                vr.Y += Main.rand.NextFloat(-1.5f, 1.5f);
                Projectile.NewProjectile(Source, GunShootPos, vr, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                vr.X += Main.rand.NextFloat(-1.5f, 1.5f);
                vr.Y += Main.rand.NextFloat(-1.5f, 1.5f);
                int proj = Projectile.NewProjectile(Source, GunShootPos, vr, AmmoTypes, (int)(WeaponDamage * 0.7f), WeaponKnockback, Owner.whoAmI, 0);
                Main.projectile[proj].ArmorPenetration = 5;
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
    }
}
