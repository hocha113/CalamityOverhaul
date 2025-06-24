using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RAuroraBlazer : CWRItemOverride
    {
        public override int TargetID => ModContent.ItemType<AuroraBlazer>();
        public override void SetDefaults(Item item) {
            item.SetCartridgeGun<AuroraBlazerHeldProj>(660);
            item.CWR().CartridgeType = CartridgeUIEnum.JAR;
        }
    }
}
