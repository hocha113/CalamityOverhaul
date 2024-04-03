using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Sounds;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    //这个项目可以视作是一次以BaseGun为基础制作换弹效果的尝试，它仍旧可以与物品的弹夹系统发生关联
    internal class DeepcoreGK2HeldProj : BaseGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "DeepcoreGK2";
        public override int targetCayItem => ModContent.ItemType<DeepcoreGK2>();
        public override int targetCWRItem => ModContent.ItemType<DeepcoreGK2EcType>();
        int kreLoadTime;
        public override void SetRangedProperty() {
            HandFireDistance = HandDistance = 36;
            HandFireDistanceY = HandDistanceY = -8;
            ShootPosNorlLengValue = -20;
            ShootPosToMouLengValue = 28;
            AngleFirearmRest = -11;
            CanRightClick = true;
            FiringDefaultSound = false;
        }

        public override void FiringIncident() {
            base.FiringIncident();
            if (!Item.CWR().IsKreload) {
                kreLoadTime++;
                if (kreLoadTime == 10) {
                    SoundEngine.PlaySound(CWRSound.CaseEjection with { Volume = 0.6f }, Projectile.Center);
                }
                if (kreLoadTime > 60) {
                    Item.CWR().NumberBullets = Item.CWR().AmmoCapacity;
                    Item.CWR().IsKreload = true;
                    Item.CWR().SpecialAmmoState = SpecialAmmoStateEnum.ordinary;
                }
            }
            else {
                kreLoadTime = 0;
            }
            if (onFireR) {
                Recoil = 0.3f;
                Item.useTime = 7;
                Item.UseSound = CommonCalamitySounds.LargeWeaponFireSound with { Volume = 0.3f, Pitch = 0.2f };
            }
            else {
                Recoil = 1.2f;
                Item.useTime = 14;
                Item.UseSound = SoundID.Item38;
            }
        }

        public override void FiringShoot() {
            if (Item.CWR().NumberBullets <= 0) {
                return;
            }
            int proj = Projectile.NewProjectile(Source, GunShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            Main.projectile[proj].scale *= 3;
            Main.projectile[proj].extraUpdates += 1;
            SpawnGunFireDust(GunShootPos, dustID1: DustID.YellowStarDust, dustID2: DustID.FireworksRGB, dustID3: DustID.FireworksRGB);
            CaseEjection(2);
            SoundEngine.PlaySound(Item.UseSound, Projectile.Center);
            _ = UpdateConsumeAmmo();
            _ = CreateRecoil();
            Item.CWR().NumberBullets--;
            if (Item.CWR().NumberBullets <= 0) {
                Item.CWR().IsKreload = false;
                Item.CWR().NumberBullets = 0;
            }
        }

        public override void FiringShootR() {
            if (Item.CWR().NumberBullets <= 0) {
                return;
            }
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            SpawnGunFireDust(GunShootPos + ShootVelocity, dustID1: DustID.YellowStarDust, dustID2: DustID.FireworkFountain_Red, dustID3: DustID.FireworkFountain_Red);
            CaseEjection();
            SoundEngine.PlaySound(Item.UseSound, Projectile.Center);
            _ = UpdateConsumeAmmo();
            _ = CreateRecoil();
            Item.CWR().NumberBullets--;
            if (Item.CWR().NumberBullets <= 0) {
                Item.CWR().IsKreload = false;
                Item.CWR().NumberBullets = 0;
            }
        }
    }
}
