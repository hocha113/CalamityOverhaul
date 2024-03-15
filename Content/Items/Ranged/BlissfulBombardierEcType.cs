using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class BlissfulBombardierEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "BlissfulBombardier";
        public override void SetDefaults() {
            Item.SetCalamitySD<BlissfulBombardier>();
            Item.SetCartridgeGun<BlissfulBombardierHeldProj>(20);
        }
    }
}
