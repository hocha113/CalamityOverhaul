using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class OnyxBlasterHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.OnyxBlaster].Value;
        public override int TargetID => ItemID.OnyxBlaster;
        public override void SetRangedProperty() {
            FireTime = 18;
            kreloadMaxTime = 18;
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandIdleDistanceX = 18;
            HandIdleDistanceY = 0;
            GunPressure = 0.3f;
            ControlForce = 0.1f;
            Recoil = 3.2f;
            RangeOfStress = 48;
            LoadingAmmoAnimation = LoadingAmmoAnimationEnum.Shotgun;
            if (!MagazineSystem) {
                FireTime += kreloadMaxTime;
            }
        }

        public override void HanderPlaySound() {
            SoundEngine.PlaySound(CWRSound.Gun_Shotgun_Shoot2 with { Volume = 0.4f, Pitch = -0.1f }, Projectile.Center);
        }

        public override void FiringShoot() {
            for (int i = 0; i < 4; i++) {
                int proj = Projectile.NewProjectile(Source, ShootPos, ShootVelocity.RotatedByRandom(0.13f) * Main.rand.NextFloat(0.9f, 1.5f)
                    , ProjectileID.BlackBolt, (int)(WeaponDamage * 0.9f), WeaponKnockback, Owner.whoAmI, 0);
                int proj2 = Projectile.NewProjectile(Source2, ShootPos, ShootVelocity.RotatedByRandom(0.11f) * Main.rand.NextFloat(0.9f, 1.2f)
                    , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                Main.projectile[proj].timeLeft += Main.rand.Next(30);
                Main.projectile[proj2].extraUpdates += 1;
                Main.projectile[proj2].scale += Main.rand.NextFloat(0.5f);
            }
        }
    }
}
