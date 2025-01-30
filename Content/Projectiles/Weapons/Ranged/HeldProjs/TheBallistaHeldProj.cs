using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class TheBallistaHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "TheBallista";
        public override int TargetID => ModContent.ItemType<TheBallista>();
        public override void SetRangedProperty() => BowstringData.DeductRectangle = new Rectangle(8, 20, 2, 32);
        public override void BowShoot() => OrigItemShoot();
    }
}
