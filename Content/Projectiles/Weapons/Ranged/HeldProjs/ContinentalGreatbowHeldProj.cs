using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class ContinentalGreatbowHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "ContinentalGreatbow";
        public override int TargetID => ModContent.ItemType<ContinentalGreatbow>();
        public override void SetRangedProperty() {
            BowArrowDrawNum = 3;
            BowstringData.DeductRectangle = new Rectangle(8, 16, 4, 26);
        }
        public override void BowShoot() {
            OrigItemShoot();
        }
    }
}
