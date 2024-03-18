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
        private SlotId accumulator;
        public override void SetRangedProperty() {
            FiringDefaultSound = false;
            CanUpdateMagazineContentsInShootBool = false;
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
        }

        public override void FiringShoot() {
            GunPressure = 0f;
            ControlForce = 0f;
            Recoil = 0f;
            if (BulletNum == 1) {
                FireTime = 90;
                Item.crit = 100;
            }
            if (BulletNum == 4) {
                Item.crit = -1000;
                FireTime = 60;
            }

            if (Owner.ownedProjectileCounts[ModContent.ProjectileType<SniperRifleOnSpan>()] == 0) {
                //accumulator = SoundEngine.PlaySound(CWRSound.Accumulator with { Pitch = 0.3f }, Projectile.Center);
                Projectile.NewProjectile(Source, GunShootPos, Vector2.Zero
                    , ModContent.ProjectileType<SniperRifleOnSpan>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0, Projectile.whoAmI);
            }
            return;
        }
    }
}
