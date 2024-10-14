using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class FlakToxicannonEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "FlakToxicannon";
        public override void SetDefaults() {
            Item.SetItemCopySD<FlakToxicannon>();
            Item.SetCartridgeGun<FlakToxicannonHeldProj>(65);
            Item.damage = 62;
            Item.useAmmo = AmmoID.Bullet;
            Item.CWR().Scope = true;
        }
    }
}
