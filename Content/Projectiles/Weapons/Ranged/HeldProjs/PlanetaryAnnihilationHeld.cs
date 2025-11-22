using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class PlanetaryAnnihilationHeld : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "PlanetaryAnnihilation";
        public override void SetRangedProperty() => BowstringData.DeductRectangle = new Rectangle(8, 8, 2, 84);
        public override void PostInOwner() {
            if (onFire) {
                BowArrowDrawBool = false;
                LimitingAngle();
            }
        }

        public override void HanderPlaySound() {
            if (Owner.IsWoodenAmmo(AmmoTypes)) {
                SoundEngine.PlaySound(Item.UseSound, Projectile.Center);
            }
            else {
                SoundEngine.PlaySound(SoundID.Item5, Projectile.Center);
            }
        }

        public override void BowShoot() => OrigItemShoot();
    }
}
