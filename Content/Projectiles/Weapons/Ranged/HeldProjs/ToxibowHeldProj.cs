using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.RangedModify.Core;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class ToxibowHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Toxibow";
        public override int TargetID => ModContent.ItemType<Toxibow>();
        public override void SetRangedProperty() => BowstringData.DeductRectangle = new Rectangle(2, 12, 2, 30);
        public override void BowShoot() => OrigItemShoot();
    }
}
