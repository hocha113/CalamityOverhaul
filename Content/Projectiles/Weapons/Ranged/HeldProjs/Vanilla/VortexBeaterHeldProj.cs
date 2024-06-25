using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class VortexBeaterHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.VortexBeater].Value;
        public override int targetCayItem => ItemID.VortexBeater;
        public override int targetCWRItem => ItemID.VortexBeater;
        float randomShootRotset;
        int fireIndex;
        int fireIndex2;
        public override void SetRangedProperty() {
            FireTime = 4;
            HandDistance = 20;
            HandDistanceY = 2;
            ShootPosToMouLengValue = 20;
            ShootPosNorlLengValue = -4;
            Recoil = 0.3f;
            CanRightClick = true;
            SpwanGunDustMngsData.splNum = 0.3f;
            SpwanGunDustMngsData.dustID1 = DustID.Vortex;
            SpwanGunDustMngsData.dustID2 = DustID.Vortex;
            SpwanGunDustMngsData.dustID3 = DustID.Vortex;
        }

        public override float GetGunInFireRot() {
            float rot = base.GetGunInFireRot();
            if (kreloadTimeValue == 0) {
                rot += randomShootRotset;
            }
            return rot;
        }

        public override void SetShootAttribute() {
            if (onFireR) {
                EjectCasingProjSize = 2;
                FireTime = 2;
                if (++fireIndex2 >= 4) {
                    randomShootRotset = Main.rand.NextFloat(-0.3f, 0.3f);
                    FireTime = 20;
                    fireIndex2 = 0;
                }
                return;
            }
            EjectCasingProjSize = 1;
            FireTime = 4;
            randomShootRotset = Main.rand.NextFloat(-0.16f, 0.16f);
            if (++fireIndex > 6) {
                SoundEngine.PlaySound(SoundID.Item45 with { Pitch = 0.45f }, Projectile.Center);
                if (Projectile.IsOwnedByLocalPlayer()) {
                    Projectile.NewProjectile(Source, GunShootPos, ShootVelocity
                    , ProjectileID.VortexBeaterRocket, WeaponDamage * 2, WeaponKnockback, Owner.whoAmI, 0);
                }
                fireIndex = 0;
            }
        }

        public override void FiringShoot() =>
        Projectile.NewProjectile(Source, GunShootPos, ShootVelocityInProjRot
                , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);

        public override void FiringShootR() =>
        Projectile.NewProjectile(Source, GunShootPos, ShootVelocityInProjRot
                , ProjectileID.VortexBeaterRocket, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
    }
}
