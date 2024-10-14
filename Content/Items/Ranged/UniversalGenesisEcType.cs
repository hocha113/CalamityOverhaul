using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class UniversalGenesisEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "UniversalGenesis";
        public override void SetDefaults() {
            Item.SetItemCopySD<UniversalGenesis>();
            Item.SetCartridgeGun<UniversalGenesisHeldProj>(50);
        }
    }
}
