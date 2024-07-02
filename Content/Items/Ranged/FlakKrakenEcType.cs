using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class FlakKrakenEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "FlakKraken";
        public override void SetDefaults() {
            Item.SetCalamitySD<FlakKraken>();
            Item.damage = 84;
            Item.SetCartridgeGun<FlakKrakenHeldProj>(80);
            Item.CWR().Scope = true;
        }
    }
}
