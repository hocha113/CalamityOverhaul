using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class GrenadeLauncherHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.GrenadeLauncher].Value;
        public override int targetCayItem => ItemID.GrenadeLauncher;
        public override int targetCWRItem => ItemID.GrenadeLauncher;
        public override void SetRangedProperty() {
            FireTime = 20;
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandDistance = 15;
            HandDistanceY = 0;
            GunPressure = 1.2f;
            ControlForce = 0.1f;
            RepeatedCartridgeChange = true;
            Recoil = 1.6f;
            RangeOfStress = 5;
            kreloadMaxTime = 60;
            EnableRecoilRetroEffect = true;
            CanCreateCaseEjection = false;
            RecoilRetroForceMagnitude = 7;
            RecoilOffsetRecoverValue = 0.7f;
            CanCreateSpawnGunDust = false;
            LoadingAA_None.loadingAA_None_Roting = 30;
            LoadingAA_None.loadingAA_None_X = 0;
            LoadingAA_None.loadingAA_None_Y = 13;
        }

        public override void PostInOwnerUpdate() {
            if (onFire && kreloadTimeValue <= 0) {
                float minRot = MathHelper.ToRadians(50);
                float maxRot = MathHelper.ToRadians(130);
                Projectile.rotation = MathHelper.Clamp(ToMouseA + MathHelper.Pi, minRot, maxRot) - MathHelper.Pi;
                if (ToMouseA + MathHelper.Pi > MathHelper.ToRadians(270)) {
                    Projectile.rotation = minRot - MathHelper.Pi;
                }
                Projectile.Center = Owner.Center + Projectile.rotation.ToRotationVector2() * HandFireDistance + OffsetPos;
                ArmRotSengsBack = ArmRotSengsFront = (MathHelper.PiOver2 - (Projectile.rotation + 0.5f * DirSign)) * DirSign;
                SetCompositeArm();
            }
        }

        public override bool KreLoadFulfill() {
            return true;
        }

        public override void FiringShoot() {
            Item ammoItem = GetSelectedBullets();
            if (ammoItem.type == ItemID.RocketI) {
                AmmoTypes = ProjectileID.GrenadeI;
            }
            if (ammoItem.type == ItemID.RocketII) {
                AmmoTypes = ProjectileID.GrenadeII;
            }
            if (ammoItem.type == ItemID.RocketIII) {
                AmmoTypes = ProjectileID.GrenadeIII;
            }
            if (ammoItem.type == ItemID.RocketIV) {
                AmmoTypes = ProjectileID.GrenadeIV;
            }
            if (ammoItem.type == ItemID.ClusterRocketI) {
                AmmoTypes = ProjectileID.ClusterGrenadeI;
            }
            if (ammoItem.type == ItemID.ClusterRocketII) {
                AmmoTypes = ProjectileID.ClusterGrenadeII;
            }
            if (ammoItem.type == ItemID.DryRocket) {
                AmmoTypes = ProjectileID.DryGrenade;
            }
            if (ammoItem.type == ItemID.WetRocket) {
                AmmoTypes = ProjectileID.WetGrenade;
            }
            if (ammoItem.type == ItemID.HoneyRocket) {
                AmmoTypes = ProjectileID.HoneyGrenade;
            }
            if (ammoItem.type == ItemID.LavaRocket) {
                AmmoTypes = ProjectileID.LavaGrenade;
            }
            if (ammoItem.type == ItemID.MiniNukeI) {
                AmmoTypes = ProjectileID.MiniNukeGrenadeI;
            }
            if (ammoItem.type == ItemID.MiniNukeII) {
                AmmoTypes = ProjectileID.MiniNukeGrenadeII;
            }
            int proj1 = Projectile.NewProjectile(Source, GunShootPos, (ShootVelocityInProjRot + new Vector2(0, -4)) * 0.8f, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            Main.projectile[proj1].timeLeft += 120;
            int proj2 = Projectile.NewProjectile(Source, GunShootPos, (ShootVelocityInProjRot + new Vector2(0, -8)) * 0.6f, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            Main.projectile[proj2].timeLeft += 120;
            int ammonum = Main.rand.Next(4);
            if (ammonum <= 1) {
                int proj3 = Projectile.NewProjectile(Source, GunShootPos, (ShootVelocityInProjRot + new Vector2(0, -12)) * 0.5f, AmmoTypes, WeaponDamage / 2, WeaponKnockback, Owner.whoAmI, 0);
                Main.projectile[proj3].timeLeft += 120;
            }
            if (ammonum == 0) {
                int proj4 = Projectile.NewProjectile(Source, GunShootPos, (ShootVelocityInProjRot + new Vector2(0, -16)) * 0.4f, AmmoTypes, WeaponDamage / 2, WeaponKnockback, Owner.whoAmI, 0);
                Main.projectile[proj4].timeLeft += 120;
            }
        }
    }
}
