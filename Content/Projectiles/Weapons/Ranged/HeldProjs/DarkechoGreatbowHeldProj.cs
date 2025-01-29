using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class DarkechoGreatbowHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "DarkechoGreatbow";
        public override int TargetID => ModContent.ItemType<DarkechoGreatbow>();
        public override void SetRangedProperty() {
            BowArrowDrawNum = 2;
            HandIdleDistanceX = 16;
            HandFireDistanceX = 16;
            DrawArrowMode = -24;
            BowstringData.DeductRectangle = new Rectangle(2, 6, 2, 54);
        }
        public override void BowShoot() => OrigItemShoot();
    }
}
