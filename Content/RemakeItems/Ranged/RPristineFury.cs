using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RPristineFury : CWRItemOverride
    {
        public override void SetDefaults(Item item) {
            item.SetCartridgeGun<PristineFuryHeldProj>(160);
            item.CWR().CartridgeType = CartridgeUIEnum.JAR;
        }
    }
}
