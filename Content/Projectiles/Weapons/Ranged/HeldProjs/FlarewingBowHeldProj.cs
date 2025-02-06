using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.RangedModify.Core;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class FlarewingBowHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "FlarewingBow";
        public override int TargetID => ModContent.ItemType<FlarewingBow>();
        public override void SetRangedProperty() {
            BowArrowDrawNum = 3;
            HandFireDistanceX = 20;
            DrawArrowMode = -24;
            BowstringData.DeductRectangle = new Rectangle(2, 14, 2, 44);
        }
        public override void BowShoot() => OrigItemShoot();
    }
}
