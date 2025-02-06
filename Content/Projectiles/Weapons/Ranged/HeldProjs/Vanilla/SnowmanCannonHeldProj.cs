using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.RangedModify.Core;
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
        public override int TargetID => ItemID.SnowmanCannon;
        public override void SetRangedProperty() {
            FireTime = 60;
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandIdleDistanceX = 15;
            HandIdleDistanceY = 0;
            GunPressure = 0.3f;
            ControlForce = 0.02f;
            RepeatedCartridgeChange = true;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 22;
            RecoilOffsetRecoverValue = 0.9f;
            Recoil = 4.8f;
            RangeOfStress = 48;
            kreloadMaxTime = 60;
            EjectCasingProjSize = 2;
            SpwanGunDustMngsData.dustID1 = 76;
            SpwanGunDustMngsData.dustID2 = 149;
            SpwanGunDustMngsData.dustID3 = 76;
            LoadingAA_None.Roting = -30;
            LoadingAA_None.gunBodyX = 3;
            LoadingAA_None.gunBodyY = -3;
        }

        public override void HanderPlaySound() {
            SoundEngine.PlaySound(ScorchedEarth.ShootSound with { Pitch = 0.3f }, Projectile.Center);
        }

        public override void SetShootAttribute() {
            AmmoTypes = ProjectileID.RocketSnowmanI;
            int ammoItemType = ModItem.GetSelectedBullets().type;
            if (VaultUtils.SnowmanCannonAmmoMap.ContainsKey(ammoItemType)) {
                AmmoTypes = VaultUtils.SnowmanCannonAmmoMap[ammoItemType];
            }
        }

        public override void FiringShoot() {
            int ammonum = Main.rand.Next(7);
            if (ammonum != 0) {
                int proj1 = Projectile.NewProjectile(Source, ShootPos, ShootVelocity * 1.4f, AmmoTypes, WeaponDamage * 2, WeaponKnockback, Owner.whoAmI, 0);
                Main.projectile[proj1].scale *= 2f;
                Main.projectile[proj1].usesLocalNPCImmunity = true;
                Main.projectile[proj1].localNPCHitCooldown = 5;
                for (int i = 0; i < 2; i++) {
                    int proj2 = Projectile.NewProjectile(Source2, ShootPos, ShootVelocity.RotatedBy(MathHelper.Lerp(-0.1f, 0.1f, i)) * 1f, AmmoTypes, WeaponDamage / 2, WeaponKnockback, Owner.whoAmI, 0);
                    Main.projectile[proj2].scale *= 1.5f;
                    Main.projectile[proj2].usesLocalNPCImmunity = true;
                    Main.projectile[proj2].localNPCHitCooldown = 5;
                    _ = UpdateConsumeAmmo();
                }
                for (int i = 0; i < 2; i++) {
                    int proj3 = Projectile.NewProjectile(Source2, ShootPos, ShootVelocity.RotatedBy(MathHelper.Lerp(-0.1f, 0.1f, i)) * 1.2f, AmmoTypes, WeaponDamage / 4, WeaponKnockback, Owner.whoAmI, 0);
                    Main.projectile[proj3].extraUpdates += 1;
                    Main.projectile[proj3].scale *= 1f;
                    Main.projectile[proj3].usesLocalNPCImmunity = true;
                    Main.projectile[proj3].localNPCHitCooldown = 5;
                    _ = UpdateConsumeAmmo();
                }
                for (int i = 0; i < 2; i++) {
                    int proj4 = Projectile.NewProjectile(Source2, ShootPos, ShootVelocity.RotatedBy(MathHelper.Lerp(-0.2f, 0.2f, i)) * 0.2f, AmmoTypes, WeaponDamage / 4, WeaponKnockback, Owner.whoAmI, 0);
                    Main.projectile[proj4].scale *= 1f;
                    Main.projectile[proj4].timeLeft += 3600;
                    Main.projectile[proj4].usesLocalNPCImmunity = true;
                    Main.projectile[proj4].localNPCHitCooldown = 5;
                    _ = UpdateConsumeAmmo();
                }
                ModOwner.SetScreenShake(3);
            }
            if (ammonum == 0) {
                int proj5 = Projectile.NewProjectile(Source, ShootPos, ShootVelocity * 0.00001f, AmmoTypes, WeaponDamage * 10, WeaponKnockback, Owner.whoAmI, 0);
                Main.projectile[proj5].scale *= 3f;
                Main.projectile[proj5].usesLocalNPCImmunity = true;
                Main.projectile[proj5].localNPCHitCooldown = 5;
                Main.projectile[proj5].CWR().GetHitAttribute.NeverCrit = true;
                ModOwner.SetScreenShake(3.5f);
            }
        }
    }
}
