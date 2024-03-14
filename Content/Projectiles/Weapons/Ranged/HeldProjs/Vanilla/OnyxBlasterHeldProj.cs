using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.PhosphorescentGauntletProj;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class OnyxBlasterHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.OnyxBlaster].Value;
        public override int targetCayItem => ItemID.OnyxBlaster;
        public override int targetCWRItem => ItemID.OnyxBlaster;
        public override void SetRangedProperty() {
            FireTime = 48;
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandDistance = 15;
            HandDistanceY = 0;
            GunPressure = 0.1f;
            ControlForce = 0.05f;
            Recoil = 1f;
            RangeOfStress = 48;
            RepeatedCartridgeChange = true;
            kreloadMaxTime = 45;
        }

        public override void PreInOwnerUpdate() {
            LoadingAnimation(30, 0, 13);
        }

        public override bool KreLoadFulfill() {
            return true;
        }

        public override void PostFiringShoot() {
        }

        public override void FiringShoot() {
            SpawnGunFireDust(GunShootPos, ShootVelocity);
            for (int i = 0; i < 4; i++) {
                _ = Projectile.NewProjectile(Owner.parent(), GunShootPos, ShootVelocity.RotatedBy(Main.rand.NextFloat(-0.12f, 0.12f)) * Main.rand.NextFloat(0.8f, 1.2f), AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                int proj = Projectile.NewProjectile(Owner.parent(), GunShootPos, ShootVelocity.RotatedBy(Main.rand.NextFloat(-0.22f, 0.22f)) * Main.rand.NextFloat(0.9f, 1.5f), ProjectileID.BlackBolt, (int)(WeaponDamage * 0.9f), WeaponKnockback, Owner.whoAmI, 0);
                Main.projectile[proj].timeLeft += 25;
                _ = CreateRecoil();
                UpdateMagazineContents();
            }
            _ = CreateRecoil();
        }
    }
}
