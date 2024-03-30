using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class AdamantiteRepeaterHeldProj : BaseGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.AdamantiteRepeater].Value;
        public override int targetCayItem => ItemID.AdamantiteRepeater;
        public override int targetCWRItem => ItemID.AdamantiteRepeater;
        public override void SetRangedProperty() {
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandDistance = 15;
            HandDistanceY = 0;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0;
        }

        public override void FiringShoot() {
            Projectile proj = Projectile.NewProjectileDirect(Source, GunShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            proj.extraUpdates += 1;
            proj.usesLocalNPCImmunity = true;
            proj.localNPCHitCooldown = 5;
            if (proj.penetrate == 1) {
                proj.maxPenetrate += 2;
                proj.penetrate += 2;
            }
            _ = UpdateConsumeAmmo();
        }
    }
}
