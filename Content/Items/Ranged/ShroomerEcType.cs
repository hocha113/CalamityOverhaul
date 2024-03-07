using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class ShroomerEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Shroomer";
        public override void SetDefaults() {
            Item.SetCalamityGunSD<Shroomer>();
            Item.SetCartridgeGun<ShroomerHeldProj>(12);
        }
    }
}
