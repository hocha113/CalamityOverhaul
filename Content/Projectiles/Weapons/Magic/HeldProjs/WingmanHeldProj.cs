using CalamityMod.Items.Weapons.Magic;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Magic;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs
{
    internal class WingmanHeldProj : BaseMagicGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Magic + "Wingman";
        public override int targetCayItem => ModContent.ItemType<Wingman>();
        public override int targetCWRItem => ModContent.ItemType<WingmanEcType>();
        public override void SetMagicProperty() {
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandDistance = 15;
            HandDistanceY = 9;
            HandFireDistance = 15;
            HandFireDistanceY = -5;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0;
            ArmRotSengsBackNoFireOffset = 113;
        }

        public override void FiringIncident() {
            base.FiringIncident();
        }

        public override int Shoot() {
            return base.Shoot();
        }
    }
}
