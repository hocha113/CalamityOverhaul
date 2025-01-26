using CalamityMod;
using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class PlanetaryAnnihilationHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "PlanetaryAnnihilation";
        public override int targetCayItem => ModContent.ItemType<PlanetaryAnnihilation>();
        public override int targetCWRItem => ModContent.ItemType<PlanetaryAnnihilationEcType>();
        public override void SetRangedProperty() {
            BowstringData.DeductRectangle = new Rectangle(8, 8, 2, 84);
        }
        public override void PostInOwner() {
            if (onFire) {
                BowArrowDrawBool = false;
                LimitingAngle();
            }
        }

        public override void HanderPlaySound() {
            if (CalamityUtils.CheckWoodenAmmo(AmmoTypes, Owner)) {
                SoundEngine.PlaySound(Item.UseSound, Projectile.Center);
            }
            else {
                SoundEngine.PlaySound(SoundID.Item5, Projectile.Center);
            }
        }

        public override void BowShoot() => OrigItemShoot();
    }
}
