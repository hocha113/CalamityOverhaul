using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class SurgeDriverHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "SurgeDriver";
        public override int targetCayItem => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.SurgeDriver>();
        public override int targetCWRItem => ModContent.ItemType<SurgeDriverEcType>();

        public override void SetRangedProperty() {
            loadTheRounds = CWRSound.CaseEjection2 with { Pitch = -0.2f };
            kreloadMaxTime = 120;
            FireTime = 20;
            HandIdleDistanceX = 52;
            HandFireDistanceX = 52;
            HandFireDistanceY = -13;
            ShootPosNorlLengValue = -4;
            ShootPosToMouLengValue = 35;
            RepeatedCartridgeChange = true;
            FiringDefaultSound = false;
            CanCreateSpawnGunDust = false;
            Recoil = 0;
            LoadingAA_None.Roting = 30;
            LoadingAA_None.gunBodyX = 0;
            LoadingAA_None.gunBodyY = 23;
        }

        public override void KreloadSoundloadTheRounds() {
            SoundEngine.PlaySound(loadTheRounds with { Pitch = -0.3f }, Projectile.Center);
        }

        public override bool PreFireReloadKreLoad() {
            if (BulletNum <= 0) {
                LoadingReminder = false;//在发射后设置一下装弹提醒开关，防止进行一次有效射击后仍旧弹出提示
                IsKreload = false;
                if (Item.type != ItemID.None) {
                    Item.CWR().IsKreload = false;
                }
                BulletNum = 0;
            }
            FireTime--;
            if (FireTime < 6) {
                FireTime = 6;
            }
            return false;
        }

        public override bool KreLoadFulfill() {
            FireTime = 20;
            return true;
        }

        public override void FiringShoot() {
            Recoil = 0;
            GunPressure = 0;
            ControlForce = 0.01f;
            RangeOfStress = 5;
            if (BulletNum > 40) {
                float sengs = (98 - BulletNum) * 0.015f;
                SoundEngine.PlaySound(Item.UseSound.Value with { Pitch = sengs > 0.95f ? 0.95f : sengs }, Projectile.Center);
                Projectile.NewProjectile(Owner.FromObjectGetParent(), GunShootPos, ShootVelocity * (1 + sengs)
                    , ModContent.ProjectileType<PrismEnergyBullet>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            }
            else {
                if (BulletNum == 1) {
                    Recoil = 7;
                    GunPressure = 0.8f;
                    ControlForce = 0.03f;
                    RangeOfStress = 55;
                    Vector2 vr = ShootVelocity.UnitVector();
                    float lengValue = 16;
                    float lengInXValue = 80;
                    for (int i = 0; i < 26; i++) {
                        Vector2 vr2 = vr * lengValue + vr.GetNormalVector() * Main.rand.NextFloat(-lengInXValue, lengInXValue);
                        lengValue += Main.rand.Next(60, 90);
                        lengInXValue += 7;
                        Projectile.NewProjectile(Owner.GetSource_FromThis(), vr2 + Projectile.Center, Vector2.Zero, ModContent.ProjectileType<PrismExplosionLarge>(), Projectile.damage, 0f, Projectile.owner);
                    }
                    SoundEngine.PlaySound(Item.UseSound.Value with { Pitch = -0.3f, Volume = 1.5f, MaxInstances = 3 }, Projectile.Center);
                    return;
                }
                SoundEngine.PlaySound(Item.UseSound.Value with { Pitch = 1f }, Projectile.Center);
                int proj = Projectile.NewProjectile(Owner.FromObjectGetParent(), GunShootPos, ShootVelocity
                    , ModContent.ProjectileType<PrismaticEnergyBlast>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                Main.projectile[proj].usesIDStaticNPCImmunity = true;
                Main.projectile[proj].localNPCHitCooldown = -1;
            }
        }
    }
}
