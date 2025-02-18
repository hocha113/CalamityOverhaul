using CalamityOverhaul.Content.RangedModify.Core;
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
        public override int TargetID => ItemID.LaserRifle;
        public override void SetRangedProperty() {
            ShootPosToMouLengValue = -50;
            ShootPosNorlLengValue = 0;
            HandIdleDistanceX = 16;
            HandIdleDistanceY = 2;
            HandFireDistanceY = -2;
            GunPressure = 0.1f;
            ControlForce = 0.02f;
            Recoil = 0f;
            RangeOfStress = 48;
            CanCreateCaseEjection = false;
            CanCreateSpawnGunDust = false;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 6;
            RecoilOffsetRecoverValue = 0.8f;
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
