using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class ToxibowHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Toxibow";
        public override int targetCayItem => ModContent.ItemType<Toxibow>();
        public override int targetCWRItem => ModContent.ItemType<ToxibowEcType>();
        public override void SetRangedProperty() => BowstringData.DeductRectangle = new Rectangle(2, 12, 2, 30);
        public override void BowShoot() => OrigItemShoot();
    }
}
