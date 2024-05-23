using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RShadethrower : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<Shadethrower>();
        public override int ProtogenesisID => ModContent.ItemType<ShadethrowerEcType>();
        public override string TargetToolTipItemName => "ShadethrowerEcType";
        public override void SetDefaults(Item item) {
            item.SetCartridgeGun<ShadethrowerHeldProj>(160);
            item.CWR().CartridgeEnum = CartridgeUIEnum.JAR;
        }
    }
}
