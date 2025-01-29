using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class BarinauticalHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Barinautical";
        public override int TargetID => ModContent.ItemType<Barinautical>();
        public override void SetRangedProperty() {
            ForcedConversionTargetAmmoFunc = () => AmmoTypes == ProjectileID.WoodenArrowFriendly;
            ToTargetAmmo = AmmoTypes = ModContent.ProjectileType<BoltArrow>();
            ISForcedConversionDrawAmmoInversion = true;
        }
        public override void SetShootAttribute() {
            Item.useTime = 4;
            fireIndex++;
            if (fireIndex >= 4) {
                Item.useTime = 20;
                fireIndex = 0;
            }
        }
    }
}
