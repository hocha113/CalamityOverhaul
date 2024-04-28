using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria;
using CalamityMod;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class EternalBlizzardHeldProj : BaseGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "EternalBlizzard";
        public override int targetCayItem => ModContent.ItemType<EternalBlizzard>();
        public override int targetCWRItem => ModContent.ItemType<EternalBlizzardEcType>();
        int fireIndex;
        public override void SetRangedProperty() {
            HandDistance = 30;
            HandDistanceY = 6;
            HandFireDistance = 30;
            ShootPosToMouLengValue = 10;
            ShootPosNorlLengValue = -5;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0;
            IsCrossbow = true;
            CanRightClick = true;
            DrawCrossArrowNorlMode = 3;
            ForcedConversionTargetAmmoFunc = () => true;
            ToTargetAmmo = ModContent.ProjectileType<IcicleArrowProj>();
        }

        public override void FiringIncident() {
            base.FiringIncident();
        }

        public override void FiringShoot() {
            Item.useTime = 10;
            for (int i = 0; i < fireIndex + 1; i++) {
                int proj = Projectile.NewProjectile(Source, GunShootPos, ShootVelocity.RotatedByRandom(0.12f)
                    , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                Main.projectile[proj].SetArrowRot();
                if (Main.rand.NextBool(2)) {
                    Main.projectile[proj].damage /= 2;
                }
            }
            
            if (++fireIndex > 3) {
                Item.useTime = 36;
                fireIndex = 0;
            }

            _ = UpdateConsumeAmmo();
            _ = CreateRecoil();
        }

        public override void FiringShootR() {
            Item.useTime = 10;
            int proj = Projectile.NewProjectile(Source, GunShootPos, ShootVelocity
                , AmmoTypes, (int)(WeaponDamage * 0.6f), WeaponKnockback, Owner.whoAmI, 0);
            Main.projectile[proj].SetArrowRot();
            Main.projectile[proj].Calamity().allProjectilesHome = true;
            _ = UpdateConsumeAmmo();
            _ = CreateRecoil();
        }
    }
}
