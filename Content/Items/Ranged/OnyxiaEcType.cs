using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class OnyxiaEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Onyxia";
        public override void SetDefaults() {
            Item.SetCalamitySD<Onyxia>();
            Item.SetCartridgeGun<OnyxiaHeldProj>(280);
        }

        public override bool CanConsumeAmmo(Item ammo, Player player) => Main.rand.NextFloat() > 0.15f;
    }
}
