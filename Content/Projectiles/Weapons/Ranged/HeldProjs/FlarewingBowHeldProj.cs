using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class FlarewingBowHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "FlarewingBow";
        public override int targetCayItem => ModContent.ItemType<FlarewingBow>();
        public override int targetCWRItem => ModContent.ItemType<FlarewingBowEcType>();
        public override void SetRangedProperty() {
            BowArrowDrawNum = 3;
            HandFireDistanceX = 20;
            DrawArrowMode = -24;
            BowstringData.DeductRectangle = new Rectangle(2, 14, 2, 44);
        }
        public override void BowShoot() {
            OrigItemShoot();
        }
    }
}
