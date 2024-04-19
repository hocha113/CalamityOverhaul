using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.ModLoader;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RElementalEruption : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<ElementalEruption>();
        public override int ProtogenesisID => ModContent.ItemType<ElementalEruptionEcType>();
        public override string TargetToolTipItemName => "ElementalEruptionEcType";
        public override void SetDefaults(Item item) {
            item.SetCartridgeGun<ElementalEruptionHeldProj>(160);
            item.CWR().CartridgeEnum = CartridgeUIEnum.JAR;
        }
    }
}
