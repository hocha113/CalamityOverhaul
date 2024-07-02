using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class ElementalEruptionEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "ElementalEruption";
        public override void SetDefaults() {
            Item.SetCalamitySD<ElementalEruption>();
            Item.SetCartridgeGun<ElementalEruptionHeldProj>(160);
            Item.CWR().CartridgeEnum = CartridgeUIEnum.JAR;
        }
    }
}
