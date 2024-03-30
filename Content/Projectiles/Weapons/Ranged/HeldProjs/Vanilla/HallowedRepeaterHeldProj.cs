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
            Projectile proj = Projectile.NewProjectileDirect(Source, GunShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            proj.usesLocalNPCImmunity = true;
            proj.localNPCHitCooldown = 2;
            proj.extraUpdates += 1;
            if (proj.penetrate == 1) {
                proj.maxPenetrate += 2;
                proj.penetrate += 2;
            }
            for (int i = 0; i < 3; i++) {
                if (i == 0) {
                    AmmoTypes = ProjectileID.IchorArrow;
                }
                else if (i == 1) {
                    AmmoTypes = ProjectileID.HolyArrow;
                }
                else if (i == 2) {
                    AmmoTypes = ProjectileID.CursedArrow;
                }
                int proj1 = Projectile.NewProjectile(Source, GunShootPos, ShootVelocity.RotatedBy(Main.rand.NextFloat(-0.08f, 0.08f)) * Main.rand.NextFloat(0.8f, 1f), AmmoTypes, 2 * WeaponDamage/3, WeaponKnockback, Owner.whoAmI, 0);
                Main.projectile[proj1].extraUpdates += 2;
            }
        }
    }
}
