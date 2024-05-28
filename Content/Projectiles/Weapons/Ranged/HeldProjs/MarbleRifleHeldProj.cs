using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged.Extras;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class MarbleRifleHeldProj : GraniteRifleHeldProj
    {
        public override string Texture => CWRConstant.Item_Ranged + "MarbleRifle";
        public override int targetCayItem => ModContent.ItemType<MarbleRifle>();
        public override int targetCWRItem => ModContent.ItemType<MarbleRifle>();
        public override void SetRangedProperty() {
            base.SetRangedProperty();
            ToTargetAmmo = ModContent.ProjectileType<MarbleBullet>();//你要转化什么?
        }
    }
}
