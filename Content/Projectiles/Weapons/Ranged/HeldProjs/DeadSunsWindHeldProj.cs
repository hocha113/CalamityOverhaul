using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class DeadSunsWindHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "DeadSunsWind";
        public override int targetCayItem => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.DeadSunsWind>();
        public override int targetCWRItem => ModContent.ItemType<DeadSunsWindEcType>();

        public override void SetRangedProperty() {
            HandDistance = 30;
            HandFireDistance = 30;
            HandFireDistanceY = -10;
            Recoil = 0;
        }

        public override void FiringShoot() {
            Projectile.NewProjectile(Source, Projectile.Center, ShootVelocity
                    , Item.shoot, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }
    }
}
