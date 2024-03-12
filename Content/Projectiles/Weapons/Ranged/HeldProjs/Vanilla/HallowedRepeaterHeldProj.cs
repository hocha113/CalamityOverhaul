using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class HallowedRepeaterHeldProj : BaseGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.HallowedRepeater].Value;
        public override int targetCayItem => ItemID.HallowedRepeater;
        public override int targetCWRItem => ItemID.HallowedRepeater;
        int thisNeedsTime;
        int chargeAmmoNum;
        public override void SetRangedProperty() {
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandDistance = 15;
            HandDistanceY = 0;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0;
            Item.useTime = 16;
        }

        public override void FiringShoot() {
            int proj = Projectile.NewProjectile(Source, GunShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            Main.projectile[proj].extraUpdates += 2;
            if (Main.projectile[proj].penetrate == 1) {
                Main.projectile[proj].maxPenetrate += 2;
                Main.projectile[proj].penetrate += 2;
            }
            for (int i = 0; i < 2; i++) {
                int proj1 = Projectile.NewProjectile(Source, GunShootPos, ShootVelocity.RotatedBy(Main.rand.NextFloat(-0.04f, 0.04f)) * Main.rand.NextFloat(0.8f, 1f), ProjectileID.HolyArrow, WeaponDamage * 2, WeaponKnockback, Owner.whoAmI, 0);
                Main.projectile[proj1].extraUpdates += 1;
                _ = UpdateConsumeAmmo();
            }
        }
    }
}
