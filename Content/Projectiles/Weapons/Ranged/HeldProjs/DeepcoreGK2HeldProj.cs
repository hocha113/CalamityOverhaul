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
    internal class DeepcoreGK2HeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "DeepcoreGK2";
        public override int targetCayItem => ModContent.ItemType<DeepcoreGK2>();
        public override int targetCWRItem => ModContent.ItemType<DeepcoreGK2EcType>();
        public override void SetRangedProperty() {
            HandFireDistance = HandDistance = 36;
            HandFireDistanceY = HandDistanceY = -8;
            ShootPosNorlLengValue = -20;
            ShootPosToMouLengValue = 28;
            AngleFirearmRest = -11;
            CanRightClick = true;
            FiringDefaultSound = false;
        }

        public override void FiringShoot() {
            Recoil = 1.2f;
            Item.useTime = 14;
            Item.UseSound = SoundID.Item38;
            int proj = Projectile.NewProjectile(Source, GunShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            Main.projectile[proj].scale *= 3;
            Main.projectile[proj].extraUpdates += 1;
            SpawnGunFireDust(GunShootPos, dustID1: DustID.YellowStarDust, dustID2: DustID.FireworksRGB, dustID3: DustID.FireworksRGB);
            CaseEjection(2);
            SoundEngine.PlaySound(Item.UseSound, Projectile.Center);
        }

        public override void FiringShootR() {
            Recoil = 0.3f;
            Item.useTime = 7;
            Item.UseSound = CommonCalamitySounds.LargeWeaponFireSound with { Volume = 0.3f, Pitch = 0.2f };
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            SpawnGunFireDust(GunShootPos + ShootVelocity, dustID1: DustID.YellowStarDust, dustID2: DustID.FireworkFountain_Red, dustID3: DustID.FireworkFountain_Red);
            CaseEjection();
            SoundEngine.PlaySound(Item.UseSound, Projectile.Center);
        }
    }
}
