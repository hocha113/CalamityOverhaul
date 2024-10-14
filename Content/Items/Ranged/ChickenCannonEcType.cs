using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class ChickenCannonEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "ChickenCannon";
        public override void SetDefaults() {
            Item.SetItemCopySD<ChickenCannon>();
            Item.damage = 220;
            Item.UseSound = SoundID.Item61;
            Item.SetCartridgeGun<ChickenCannonHeldProj>(25);
        }
    }
}
