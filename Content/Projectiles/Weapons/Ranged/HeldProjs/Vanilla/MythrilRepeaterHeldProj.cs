using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class MythrilRepeaterHeldProj : BaseGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.MythrilRepeater].Value;
        public override int targetCayItem => ItemID.MythrilRepeater;
        public override int targetCWRItem => ItemID.MythrilRepeater;
        public override void SetRangedProperty() {
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandDistance = 15;
            HandDistanceY = 0;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0;
            DrawCrossArrowToMode = -6;
            IsCrossbow = true;
        }

        public override void FiringShoot() {
            Projectile proj = Projectile.NewProjectileDirect(Source, GunShootPos, ShootVelocity
                , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            proj.usesLocalNPCImmunity = true;
            proj.localNPCHitCooldown = -1;
            proj.extraUpdates += 1;
            proj.SetArrowRot();
            if (proj.penetrate == 1) {
                proj.maxPenetrate += 1;
                proj.penetrate += 1;
            }
            _ = UpdateConsumeAmmo();
        }
    }
}
