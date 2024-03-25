using CalamityMod;
using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Utilities;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class SniperRifleHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.SniperRifle].Value;
        public override int targetCayItem => ItemID.SniperRifle;
        public override int targetCWRItem => ItemID.SniperRifle;
        public bool criticalStrike;
        public override void SetRangedProperty() {
            FiringDefaultSound = false;
            CanUpdateMagazineContentsInShootBool = false;
            RepeatedCartridgeChange = true;
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandDistance = 15;
            HandDistanceY = 0;
            GunPressure = 0f;
            ControlForce = 0f;
            Recoil = 0f;
            RangeOfStress = 48;
            kreloadMaxTime = 120;
        }

        public override void PreInOwnerUpdate() {
            LoadingAnimation(30, 0, 13);
            criticalStrike = BulletNum == 1;
        }

        public override void FiringShoot() {
            float soundtype = 0f;
            GunPressure = 0f;
            ControlForce = 0f;
            Recoil = 0f;
            if (BulletNum == 1) {
                FireTime = 90;
                soundtype = 0.2f;
            }
            if (BulletNum == 4) {
                FireTime = 60;
            }

            if (Owner.ownedProjectileCounts[ModContent.ProjectileType<SniperRifleOnSpan>()] == 0) {
                Projectile.NewProjectile(Source, GunShootPos, Vector2.Zero
                    , ModContent.ProjectileType<SniperRifleOnSpan>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0, Projectile.whoAmI, soundtype);
            }
            return;
        }
    }
}
