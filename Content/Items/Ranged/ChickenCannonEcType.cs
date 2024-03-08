using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria.Audio;
using Terraria.ID;
using Terraria;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class ChickenCannonEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "ChickenCannon";
        public override void SetDefaults() {
            Item.SetCalamityGunSD<ChickenCannon>();
            Item.UseSound = SoundID.Item61;
            Item.SetCartridgeGun<ChickenCannonHeldProj>(25);
        }
    }
}
