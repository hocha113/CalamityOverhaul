using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria.ID;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class SepticSkewerEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "SepticSkewer";
        public override void SetDefaults() {
            Item.SetCalamitySD<SepticSkewer>();
            Item.useAmmo = AmmoID.Bullet;
            Item.SetCartridgeGun<SepticSkewerHeldProj>(8);
        }
    }
}
