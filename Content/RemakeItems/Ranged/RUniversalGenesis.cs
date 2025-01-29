using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RUniversalGenesis : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<UniversalGenesis>();
        public override int ProtogenesisID => ModContent.ItemType<UniversalGenesisEcType>();
        public override string TargetToolTipItemName => "UniversalGenesisEcType";
        public override void SetDefaults(Item item) => item.SetCartridgeGun<UniversalGenesisHeldProj>(50);
    }
}
