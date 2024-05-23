using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RHavocsBreath : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<HavocsBreath>();
        public override int ProtogenesisID => ModContent.ItemType<HavocsBreathEcType>();
        public override string TargetToolTipItemName => "HavocsBreathEcType";
        public override void SetDefaults(Item item) {
            item.SetCartridgeGun<HavocsBreathHeldProj>(160);
            item.CWR().CartridgeEnum = CartridgeUIEnum.JAR;
        }
    }
}
