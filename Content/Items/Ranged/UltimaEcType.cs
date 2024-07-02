using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class UltimaEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Ultima";
        public override void SetDefaults() {
            Item.SetCalamitySD<Ultima>();
            Item.SetHeldProj<UltimaHeldProj>();
        }
    }
}
