using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RHalleysInferno : CWRItemOverride
    {
        public override int TargetID => ModContent.ItemType<HalleysInferno>();

        public override void SetDefaults(Item item) {
            item.SetCartridgeGun<HalleysInfernoHeldProj>(86);
            item.CWR().CartridgeType = CartridgeUIEnum.JAR;
        }
    }
}
