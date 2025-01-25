using CalamityMod;
using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Particles;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class PristineFuryHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "PristineFury_Animated";
        public override int targetCayItem => ModContent.ItemType<PristineFury>();
        public override int targetCWRItem => ModContent.ItemType<PristineFuryEcType>();
        private int maxFrame = 1;
        public override void SetRangedProperty() {
            FireTime = 3;
            HandIdleDistanceX = 25;
            HandIdleDistanceY = 4;
            HandFireDistanceX = 25;
            HandFireDistanceY = -5;
            ShootPosNorlLengValue = -3;
            ShootPosToMouLengValue = 0;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0.5f;
            RangeOfStress = 28;
            kreloadMaxTime = 90;
            RepeatedCartridgeChange = true;
            FiringDefaultSound = false;
            CanRightClick = true;
            loadTheRounds = CWRSound.Liquids_Fill_0 with { Pitch = -0.8f };
            ForcedConversionTargetAmmoFunc = () => true;
            ToTargetAmmo = ModContent.ProjectileType<PristineFire>();
            LoadingAA_None.Roting = 30;
            LoadingAA_None.gunBodyX = 0;
            LoadingAA_None.gunBodyY = 13;
            CanCreateCaseEjection = CanCreateSpawnGunDust = false;
        }

        public override void SetShootAttribute() {
            if (onFireR) {
                ShootPosToMouLengValue = 0;
                if (++fireIndex > 3) {
                    SoundEngine.PlaySound(Item.UseSound, GunShootPos);
                    fireIndex = 0;
                }
                return;
            }
            ShootPosToMouLengValue = -16;
            if (++fireIndex > 4) {
                SoundEngine.PlaySound(Item.UseSound, GunShootPos);
                fireIndex = 0;
            }
        }

        public override void FiringShoot() {
            CritSpark spark = new CritSpark(GunShootPos + ShootVelocity * 3f + new Vector2(0, -3), ShootVelocity.RotatedBy(0.25f * Owner.direction).RotatedByRandom(0.25f) * Main.rand.NextFloat(0.2f, 1.8f), Main.rand.NextBool() ? Color.DarkOrange : Color.OrangeRed, Color.OrangeRed, 0.9f, 18, 2f, 1.9f);
            GeneralParticleHandler.SpawnParticle(spark);
            int proj = Projectile.NewProjectile(Source, GunShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            Main.projectile[proj].Calamity().allProjectilesHome = true;
        }

        public override void FiringShootR() {
            for (int i = 0; i < 2; i++) {
                Vector2 newVel = ShootVelocity.RotatedByRandom(MathHelper.ToRadians(5f));
                Projectile.NewProjectile(Source, GunShootPos, newVel, ModContent.ProjectileType<PristineSecondary>(), WeaponDamage, WeaponKnockback, Owner.whoAmI);
            }
        }

        public override void GunDraw(Vector2 drawPos, ref Color lightColor) {
            Texture2D value = TextureValue;
            if (CanFire) {
                CWRUtils.ClockFrame(ref Projectile.frame, 5, 3);
                maxFrame = 4;
            }
            else {
                Projectile.frame = 0;
                maxFrame = 1;
                value = CWRUtils.GetT2DValue(CWRConstant.Cay_Wap_Ranged + "PristineFury");
            }
            Main.EntitySpriteDraw(value, drawPos, CWRUtils.GetRec(value, Projectile.frame, maxFrame), lightColor
                , Projectile.rotation, CWRUtils.GetOrig(value, maxFrame), Projectile.scale
                , DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
        }
    }
}
