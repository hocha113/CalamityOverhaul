using CalamityOverhaul.Content.Items.Magic.Extras;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Magic.EtherRoarProj
{
    internal class EtherRoarHeldProj : BaseMagicGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Magic + "AethersWhisper";
        public override int targetCayItem => ModContent.ItemType<EtherRoar>();
        public override int targetCWRItem => ModContent.ItemType<EtherRoar>();
        public override void SetMagicProperty() {
            ShootPosToMouLengValue = 60;
            ShootPosNorlLengValue = -2;
            HandIdleDistanceX = 45;
            HandIdleDistanceY = 9;
            HandFireDistanceX = 45;
            HandFireDistanceY = -5;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0;
        }

        public override void FiringIncident() {
            base.FiringIncident();
        }

        public override void FiringShoot() {
            Projectile.NewProjectile(Source, ShootPos, Vector2.Zero, ModContent.ProjectileType<EtherRoarOnSpan>()
                , WeaponDamage, WeaponKnockback, Owner.whoAmI, 0, Projectile.whoAmI);
        }
    }
}
