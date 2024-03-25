using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class SomaPrimeEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "SomaPrime";
        public override void SetDefaults() {
            Item.SetCalamitySD<SomaPrime>();
            Item.SetCartridgeGun<SomaPrimeHeldProj>(600);
        }
        public override bool CanConsumeAmmo(Item ammo, Player player) => Main.rand.NextFloat() > 0.3f;
    }
}
