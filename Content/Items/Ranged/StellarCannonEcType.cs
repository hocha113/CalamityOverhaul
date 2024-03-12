using CalamityMod.Items.Materials;
using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class StellarCannonEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "StellarCannon";
        public override void SetDefaults() {
            Item.SetCalamitySD<StellarCannon>();
            Item.useAmmo = AmmoID.Bullet;
            Item.SetCartridgeGun<StellarCannonHeldProj>(30);
        }
    }
}
