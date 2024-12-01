using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
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
        public override void SetRangedProperty() {
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandDistance = 15;
            HandDistanceY = 0;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0;
            IsCrossbow = true;
        }

        public override void FiringShoot() {
            Projectile proj = Projectile.NewProjectileDirect(Source, GunShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            proj.SetArrowRot();
            proj.usesLocalNPCImmunity = true;
            proj.localNPCHitCooldown = 2;
            proj.extraUpdates += 1;
            if (proj.penetrate == 1) {
                proj.maxPenetrate += 2;
                proj.penetrate += 2;
            }
            for (int i = 0; i < 3; i++) {
                int[] arrows = new int[] { ProjectileID.IchorArrow, ProjectileID.HolyArrow, ProjectileID.CursedArrow };
                int proj1 = Projectile.NewProjectile(Source, GunShootPos
                        , ShootVelocity.RotatedByRandom(0.2f) * Main.rand.NextFloat(0.8f, 1f)
                        , arrows[i], WeaponDamage / 3, WeaponKnockback, Owner.whoAmI, 0);
                Main.projectile[proj1].SetArrowRot();
            }

            _ = UpdateConsumeAmmo();
        }
    }
}
