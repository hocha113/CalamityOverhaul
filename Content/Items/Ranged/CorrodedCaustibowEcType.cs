using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class CorrodedCaustibowEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "CorrodedCaustibow";
        public override void SetDefaults() {
            Item.SetCalamitySD<CorrodedCaustibow>();
            Item.SetHeldProj<CorrodedCaustibowHeldProj>();
        }
    }
}
