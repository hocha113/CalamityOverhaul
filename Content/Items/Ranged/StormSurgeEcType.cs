using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class StormSurgeEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "StormSurge";
        public override void SetDefaults() {
            Item.SetItemCopySD<StormSurge>();
            Item.SetHeldProj<StormSurgeHeldProj>();
        }
    }
}
