using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class OnyxChainBlasterEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "OnyxChainBlaster";
        public override void SetDefaults() {
            Item.damage = 58;
            Item.SetCalamitySD<OnyxChainBlaster>();
            Item.SetCartridgeGun<OnyxChainBlasterHeldProj>(100);
        }
        public override bool CanConsumeAmmo(Item ammo, Player player) => Main.rand.NextFloat() > 0.1f;
    }
}
