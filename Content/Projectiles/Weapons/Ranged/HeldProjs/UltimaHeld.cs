using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class UltimaHeld : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Ultima";
        public override void SetRangedProperty() {
            BowArrowDrawBool = false;
            CanFireMotion = false;
            InOwner_HandState_AlwaysSetInFireRoding = true;
        }
        public override void PostInOwner() {
            if (!CanFire) {
                fireIndex = 0;
            }
        }
        public override void BowShoot() {
            AmmoTypes = CWRID.Proj_UltimaBolt;
            if (fireIndex > 33) {
                AmmoTypes = CWRID.Proj_UltimaBolt;
                if (Main.rand.NextBool(3)) {
                    AmmoTypes = CWRID.Proj_UltimaSpark;
                    base.BowShoot();
                    AmmoTypes = CWRID.Proj_UltimaRay;
                }
            }
            FireOffsetPos = ShootVelocity.GetNormalVector() * Main.rand.Next(-13, 13);
            base.BowShoot();
            fireIndex++;
        }
    }
}
