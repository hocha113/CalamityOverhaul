using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class SnowmanCannonHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.SnowmanCannon].Value;
        public override int targetCayItem => ItemID.SnowmanCannon;
        public override int targetCWRItem => ItemID.SnowmanCannon;
        public override void SetRangedProperty() {
            FireTime = 45;
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandDistance = 15;
            HandDistanceY = 0;
            GunPressure = 0.3f;
            ControlForce = 0.05f;
            RepeatedCartridgeChange = true;
            Recoil = 4.8f;
            RangeOfStress = 48;
            kreloadMaxTime = 60;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 22;
            RecoilOffsetRecoverValue = 0.8f;
        }

        public override void PreInOwnerUpdate() {
            LoadingAnimation(30, 0, 13);
        }

        public override bool KreLoadFulfill() {
            return base.KreLoadFulfill();
        }

        public override void FiringShoot() {
            AmmoTypes = CWRUtils.SnowmanCannonAmmo(GetSelectedBullets());
            SpawnGunFireDust();
            _ = SoundEngine.PlaySound(ScorchedEarthEcType.ShootSound with { Pitch = 0.3f }, Projectile.Center);
            DragonsBreathRifleHeldProj.SpawnGunDust(Projectile, Projectile.Center, ShootVelocity);
            SpawnGunFireDust(GunShootPos, ShootVelocity, dustID1: 76, dustID2: 149, dustID3: 76);
            int ammonum = Main.rand.Next(7);
            if (ammonum != 0) {
                int proj1 = Projectile.NewProjectile(Owner.parent(), GunShootPos, ShootVelocity * 1.6f, AmmoTypes, WeaponDamage * 2, WeaponKnockback, Owner.whoAmI, 0);
                Main.projectile[proj1].scale *= 2f;
                for (int i = 0; i < 2; i++) {
                    int proj2 = Projectile.NewProjectile(Owner.parent(), GunShootPos, ShootVelocity.RotatedBy(MathHelper.Lerp(-0.1f, 0.1f, i)) * 1f, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                    Main.projectile[proj2].scale *= 1.5f;
                    _ = UpdateConsumeAmmo();
                }
                for (int i = 0; i < 2; i++) {
                    int proj3 = Projectile.NewProjectile(Owner.parent(), GunShootPos, ShootVelocity.RotatedBy(MathHelper.Lerp(-0.1f, 0.1f, i)) * 1.2f, AmmoTypes, WeaponDamage / 2, WeaponKnockback, Owner.whoAmI, 0);
                    Main.projectile[proj3].extraUpdates += 1;
                    Main.projectile[proj3].scale *= 1f;
                    _ = UpdateConsumeAmmo();
                }
                for (int i = 0; i < 2; i++) {
                    int proj4 = Projectile.NewProjectile(Owner.parent(), GunShootPos, ShootVelocity.RotatedBy(MathHelper.Lerp(-0.2f, 0.2f, i)) * 0.2f, AmmoTypes, WeaponDamage / 2, WeaponKnockback, Owner.whoAmI, 0);
                    Main.projectile[proj4].scale *= 1f;
                    Main.projectile[proj4].timeLeft += 3600;
                    _ = UpdateConsumeAmmo();
                }
            }
            if (ammonum == 0) {
                int proj5 = Projectile.NewProjectile(Owner.parent(), GunShootPos, ShootVelocity * 0.00001f, AmmoTypes, WeaponDamage * 10, WeaponKnockback, Owner.whoAmI, 0);
                Main.projectile[proj5].scale *= 3f;
            }
        }

        public override void PostFiringShoot() {
            base.PostFiringShoot();
            EjectCasing();
        }

        public override void PostDraw(Color lightColor) {
            //if (BulletNum > 0 && BulletNum <= 4 && IsKreload) {
            //    string path = CWRConstant.Item_Ranged + "ScorchedEarth_PrimedForAction_" + BulletNum;
            //    Texture2D value = CWRUtils.GetT2DValue(path);
            //    Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, null, onFire ? Color.White : lightColor
            //        , Projectile.rotation, TextureValue.Size() / 2, Projectile.scale, DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
            //}
        }
    }
}
