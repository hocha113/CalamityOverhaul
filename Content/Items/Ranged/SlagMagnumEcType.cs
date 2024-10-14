using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class SlagMagnumEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "SlagMagnum";
        public override void SetDefaults() {
            Item.SetItemCopySD<SlagMagnum>();
            Item.damage = 58;
            Item.UseSound = CWRSound.Gun_Magnum_Shoot with { Volume = 0.35f };
            Item.SetCartridgeGun<SlagMagnumHeldProj>(8);
        }
    }
}
