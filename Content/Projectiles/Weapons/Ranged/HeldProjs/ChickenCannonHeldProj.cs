using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class ChickenCannonHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Proj_Ranged + "ChickenCannonHeld";
        public override int targetCayItem => ModContent.ItemType<ChickenCannon>();
        public override int targetCWRItem => ModContent.ItemType<ChickenCannonEcType>();

        private bool spanSound = false;
        public override void SetRangedProperty() {
            kreloadMaxTime = 120;
            FireTime = 25;
            HandDistance = 25;
            HandDistanceY = 5;
            HandFireDistance = 25;
            HandFireDistanceY = -4;
            ShootPosNorlLengValue = -8;
            ShootPosToMouLengValue = 30;
            GunPressure = 0f;
            ControlForce = 0f;
            Recoil = 1.2f;
            RangeOfStress = 25;
            RecoilRetroForceMagnitude = 6;
            EjectCasingProjSize = 2f;
            CanRightClick = true;
            RepeatedCartridgeChange = true;
            EnableRecoilRetroEffect = true;
        }

        public override void PostInOwnerUpdate() {
            CanUpdateMagazineContentsInShootBool = CanCreateRecoilBool = onFire;
            if (onFire) {
                CWRUtils.ClockFrame(ref Projectile.frame, 5, 3);
            }
        }

        public override void SetShootAttribute() {
            if (onFire) {
                RecoilRetroForceMagnitude = 13;
                CanCreateCaseEjection = CanCreateSpawnGunDust = true;

            }
            else if (onFireR) {
                RecoilRetroForceMagnitude = 0;
                CanCreateCaseEjection = CanCreateSpawnGunDust = false;
            }
        }

        public override void HanderPlaySound() {
            if (onFire) {
                SoundEngine.PlaySound(SoundID.Item61, Owner.Center);
            }
        }

        public override void FiringShoot() {
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity, Item.shoot
                , WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }

        public override void FiringShootR() {
            for (int i = 0; i < Main.maxProjectiles; ++i) {
                Projectile p = Main.projectile[i];
                if (!p.active || p.owner != Owner.whoAmI || p.type != Item.shoot) {
                    continue;
                }
                p.timeLeft = 1;
                p.netUpdate = true;
                p.netSpam = 0;
                spanSound = true;
            }
        }

        public override void PostFiringShoot() {
            if (spanSound) {
                RecoilRetroForceMagnitude = 22;
                SoundEngine.PlaySound(SoundID.Item110, Owner.Center);
            }
        }

        public override void GunDraw(ref Color lightColor) {
            Main.EntitySpriteDraw(TextureValue, Projectile.Center - Main.screenPosition
                , CWRUtils.GetRec(TextureValue, Projectile.frame, 4), lightColor
                , Projectile.rotation, CWRUtils.GetOrig(TextureValue, 4), Projectile.scale
                , DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
        }
    }
}
