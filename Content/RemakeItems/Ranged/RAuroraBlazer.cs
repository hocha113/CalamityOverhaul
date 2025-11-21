using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RAuroraBlazer : CWRItemOverride
    {
        public override void SetDefaults(Item item) {
            item.SetCartridgeGun<AuroraBlazerHeldProj>(660);
            item.CWR().CartridgeType = CartridgeUIEnum.JAR;
        }
    }
}
