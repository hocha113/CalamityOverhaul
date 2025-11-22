using CalamityOverhaul.Content.RangedModify.Core;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class BarinauticalHeld : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Barinautical";
        public override void SetRangedProperty() {
            ForcedConversionTargetAmmoFunc = () => Owner.IsWoodenAmmo(AmmoTypes);
            ToTargetAmmo = AmmoTypes = CWRID.Proj_BoltArrow;
            ISForcedConversionDrawAmmoInversion = true;
        }
        public override void SetShootAttribute() {
            Item.useTime = 4;
            fireIndex++;
            if (fireIndex >= 4) {
                Item.useTime = 20;
                fireIndex = 0;
            }
        }
    }
}
