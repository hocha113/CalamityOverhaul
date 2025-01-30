using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RCrackshotColt : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<CrackshotColt>();
        public override void SetDefaults(Item item) => SetDefaultsFunc(item);
        public static void SetDefaultsFunc(Item Item) {
            Item.SetCartridgeGun<CrackshotColtHeld>(6);
            Item.CWR().CartridgeType = CartridgeUIEnum.Magazines;
        }
    }

    internal class CrackshotColtHeld : MidasPrimeHeldProj
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "CrackshotColt";
        public override int TargetID => ModContent.ItemType<CrackshotColt>();
    }
}
