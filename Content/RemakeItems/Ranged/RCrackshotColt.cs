using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RCrackshotColt : CWRItemOverride
    {
        public override void SetDefaults(Item item) => SetDefaultsFunc(item);
        public static void SetDefaultsFunc(Item Item) {
            Item.SetCartridgeGun<CrackshotColtHeld>(6);
            Item.CWR().CartridgeType = CartridgeUIEnum.Magazines;
        }
    }

    internal class CrackshotColtHeld : MidasPrimeHeldProj
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "CrackshotColt";
    }
}
