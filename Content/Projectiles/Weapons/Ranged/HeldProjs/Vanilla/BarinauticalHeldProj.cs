using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class BarinauticalHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Barinautical";
        public override int targetCayItem => ModContent.ItemType<Barinautical>();
        public override int targetCWRItem => ModContent.ItemType<BarinauticalEcType>();
        int fireIndex;
        public override void BowShoot() {
            Item.useTime = 4;
            if (AmmoTypes == ProjectileID.WoodenArrowFriendly) {
                AmmoTypes = ModContent.ProjectileType<BoltArrow>();
            }
            base.BowShoot();
            fireIndex++;
            if (fireIndex > 4) {
                Item.useTime = 15;
                fireIndex = 0;
            }
        }
    }
}
