using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class LaserRifleHeldProj : BaseGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.LaserRifle].Value;
        public override int targetCayItem => ItemID.LaserRifle;
        public override int targetCWRItem => ItemID.LaserRifle;
        public override void SetRangedProperty() {
            ShootPosToMouLengValue = -50;
            ShootPosNorlLengValue = 0;
            HandIdleDistanceX = 15;
            HandIdleDistanceY = 0;
            GunPressure = 0.2f;
            ControlForce = 0.05f;
            Recoil = 0f;
            RangeOfStress = 48;
            CanCreateCaseEjection = false;
            CanCreateSpawnGunDust = false;
        }

        public override void FiringShoot() {
            if (Owner.CheckMana(Item)) {
                CanCreateRecoilBool = FiringDefaultSound = true;
                Owner.manaRegenDelay = 30;
                Owner.statMana -= Item.mana;
                if (Owner.statMana < 0) {
                    Owner.statMana = 0;
                }
            }
            else {
                CanCreateRecoilBool = FiringDefaultSound = false;
                return;
            }
            Projectile.NewProjectile(Source, ShootPos, ShootVelocity
                , ProjectileID.PurpleLaser, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }
    }
}
