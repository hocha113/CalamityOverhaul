using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class CrackshotColtEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "MidasPrime";
        public static void SetDefaultsFunc(Item Item) {
            Item.SetCartridgeGun<CrackshotColtHeld>(6);
            Item.CWR().CartridgeType = CartridgeUIEnum.Magazines;
        }
        public override void SetDefaults() {
            Item.SetItemCopySD<CrackshotColt>();
            SetDefaultsFunc(Item);
        }
    }

    internal class RCrackshotColt : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CrackshotColt>();
        public override int ProtogenesisID => ModContent.ItemType<CrackshotColtEcType>();
        public override string TargetToolTipItemName => "CrackshotColtEcType";
        public override void SetDefaults(Item item) => CrackshotColtEcType.SetDefaultsFunc(item);
    }

    internal class CrackshotColtHeld : MidasPrimeHeldProj
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "CrackshotColt";
        public override int targetCayItem => ModContent.ItemType<CrackshotColt>();
        public override int targetCWRItem => ModContent.ItemType<CrackshotColtEcType>();
    }
}
