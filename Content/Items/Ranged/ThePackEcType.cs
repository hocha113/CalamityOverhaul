using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class ThePackEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "ThePack";
        public override void SetDefaults() {
            Item.SetCalamitySD<ThePack>();
            Item.SetCartridgeGun<ThePackHeldProj>(12);
        }
    }
}
