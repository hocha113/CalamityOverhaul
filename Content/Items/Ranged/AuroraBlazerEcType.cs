using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class AuroraBlazerEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "AuroraBlazer";
        public override void SetDefaults() {
            Item.SetItemCopySD<AuroraBlazer>();
            Item.SetCartridgeGun<AuroraBlazerHeldProj>(660);
            Item.CWR().CartridgeEnum = CartridgeUIEnum.JAR;
        }
    }
}
