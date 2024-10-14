using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria.ID;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class SpeedBlasterEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "SpeedBlaster";
        public override void SetDefaults() {
            Item.SetItemCopySD<SpeedBlaster>();
            Item.useAmmo = AmmoID.Bullet;
            Item.SetCartridgeGun<SpeedBlasterHeldProj>(80);
        }
    }
}
