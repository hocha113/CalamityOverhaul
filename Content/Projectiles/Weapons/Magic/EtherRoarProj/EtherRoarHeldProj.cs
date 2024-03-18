using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Magic.Extras;
using Microsoft.Xna.Framework;
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
            HandDistance = 45;
            HandDistanceY = 9;
            HandFireDistance = 45;
            HandFireDistanceY = -5;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0;
        }

        public override void FiringIncident() {
            base.FiringIncident();
        }

        public override int Shoot() {
            int proj = 0;
            Projectile.NewProjectile(Source, GunShootPos, Vector2.Zero, ModContent.ProjectileType<EtherRoarOnSpan>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0, Projectile.whoAmI);
            return proj;
        }
    }
}
