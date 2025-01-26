using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Melee;
using CalamityMod.Projectiles.Ranged;
using CalamityMod.Projectiles.Typeless;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class StarmadaHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Starmada";
        public override int targetCayItem => ModContent.ItemType<Starmada>();
        public override int targetCWRItem => ModContent.ItemType<StarmadaEcType>();

        public override void SetRangedProperty() {
            kreloadMaxTime = 90;
            FireTime = 15;
            HandIdleDistanceX = 25;
            HandIdleDistanceY = 5;
            HandFireDistanceX = 25;
            HandFireDistanceY = -10;
            ShootPosNorlLengValue = -12;
            ShootPosToMouLengValue = 30;
            RepeatedCartridgeChange = true;
            CanCreateCaseEjection = false;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 1.2f;
            RangeOfStress = 25;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 12;
        }

        public override void FiringShoot() {
            for (int i = 0; i < 5; i++) {
                AmmoTypes = Utils.SelectRandom(Main.rand, new int[]{
                            ModContent.ProjectileType<PlasmaBlast>(),
                            ModContent.ProjectileType<AstralStar>(),
                            ModContent.ProjectileType<GalacticaComet>(),
                            ProjectileID.StarCannonStar,
                            ProjectileID.Starfury
                        });
                Projectile.NewProjectile(Source, ShootPos, ShootVelocity, AmmoTypes
                    , WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            }
        }
    }
}
