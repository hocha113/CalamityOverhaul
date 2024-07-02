using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria.ID;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class StellarCannonEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "StellarCannon";
        public override void SetDefaults() {
            Item.SetCalamitySD<StellarCannon>();
            Item.damage = 115;
            Item.useAmmo = AmmoID.FallenStar;
            Item.SetCartridgeGun<StellarCannonHeldProj>(30);
        }
    }
}
