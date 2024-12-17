using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class TacticalShotgunHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.TacticalShotgun].Value;
        public override int targetCayItem => ItemID.TacticalShotgun;
        public override int targetCWRItem => ItemID.TacticalShotgun;
        public override void SetRangedProperty() {
            FireTime = 20;
            kreloadMaxTime = 25;
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = -4;
            HandIdleDistanceX = 17;
            HandIdleDistanceY = 4;
            GunPressure = 0.3f;
            ControlForce = 0.06f;
            Recoil = 1.4f;
            RangeOfStress = 25;
            RepeatedCartridgeChange = true;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 7;
            LoadingAmmoAnimation = LoadingAmmoAnimationEnum.Shotgun;
            if (!MagazineSystem) {
                FireTime += kreloadMaxTime;
            }
        }

        public override void HanderPlaySound() {
            SoundEngine.PlaySound(CWRSound.Gun_Shotgun_Shoot with { Volume = 0.4f, Pitch = -0.1f }, Projectile.Center);
        }

        public override void FiringShoot() {
            for (int i = 0; i < 10; i++) {
                int proj = Projectile.NewProjectile(Source, GunShootPos
                    , ShootVelocity.RotatedBy(Main.rand.NextFloat(-0.07f, 0.07f)) * Main.rand.NextFloat(1f, 1.5f) * 0.8f
                    , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                Main.projectile[proj].scale += 0.3f;
                Main.projectile[proj].usesLocalNPCImmunity = true;
                Main.projectile[proj].localNPCHitCooldown = -1;
                Main.projectile[proj].extraUpdates += 1;
            }
        }
    }
}
