using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class AngelicShotgunHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "AngelicShotgun";
        public override int targetCayItem => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.AngelicShotgun>();
        public override int targetCWRItem => ModContent.ItemType<AngelicShotgunEcType>();
        public override void SetRangedProperty() {
            FireTime = 20;
            EnableRecoilRetroEffect = true;
            ControlForce = 0.1f;
            GunPressure = 0.3f;
            Recoil = 2;
            HandDistance = 28;
            HandDistanceY = 3;
            ForcedConversionTargetAmmoFunc = () => AmmoTypes == ProjectileID.Bullet;
            ToTargetAmmo = ModContent.ProjectileType<HallowPointRoundProj>();
        }

        public override void FiringShoot() {
            for (int i = 0; i < 4; i++) {
                int proj = Projectile.NewProjectile(Source, Projectile.Center
                    , ShootVelocity.RotatedByRandom(0.08f)
                    , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                Main.projectile[proj].CWR().SpanTypes = (byte)SpanTypesEnum.AngelicShotgun;
            }
        }
    }
}
