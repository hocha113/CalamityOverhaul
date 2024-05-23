using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class FlakKrakenEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "FlakKraken";
        public override void SetDefaults() {
            Item.SetCalamitySD<FlakKraken>();
            Item.SetCartridgeGun<FlakKrakenHeldProj>(80);
            Item.CWR().Scope = true;
        }
    }
}
