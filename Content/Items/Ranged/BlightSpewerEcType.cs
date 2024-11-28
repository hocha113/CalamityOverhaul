using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class BlightSpewerEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "BlightSpewer";
        public override void SetDefaults() {
            Item.SetItemCopySD<BlightSpewer>();
            Item.SetCartridgeGun<BlightSpewerHeldProj>(160);
            Item.CWR().CartridgeType = CartridgeUIEnum.JAR;
        }
    }
}
