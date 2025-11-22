using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RHalleysInferno : CWRItemOverride
    {
        public override void SetDefaults(Item item) {
            item.SetCartridgeGun<HalleysInfernoHeld>(86);
            item.CWR().CartridgeType = CartridgeUIEnum.JAR;
        }
    }
}
