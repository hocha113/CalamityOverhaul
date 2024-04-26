using CalamityMod.Items.Weapons.Magic;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Magic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs
{
    internal class EffervescenceHeldProj : BaseMagicGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Magic + "Effervescence";
        public override int targetCayItem => ModContent.ItemType<Effervescence>();
        public override int targetCWRItem => ModContent.ItemType<EffervescenceEcType>();
        public override void SetMagicProperty() {
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandDistance = 20;
            HandDistanceY = 3;
            HandFireDistance = 20;
            HandFireDistanceY = -5;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0;
        }

        public override void FiringIncident() {
            base.FiringIncident();
        }

        public override int Shoot() {
            OffsetPos += ShootVelocity.UnitVector() * -6;
            int type = 0;
            for (int i = 0; i < 3; i++) {
                type = base.Shoot();
                Main.projectile[type].velocity = Main.projectile[type].velocity.RotatedByRandom(0.3f);
            }
            return type;
        }
    }
}
