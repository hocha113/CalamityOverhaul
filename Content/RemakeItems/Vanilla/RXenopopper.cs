using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    internal class RXenopopper : ItemOverride
    {
        public override int TargetID => ItemID.Xenopopper;
        public override bool IsVanilla => true;
        public override string TargetToolTipItemName => "Wap_Xenopopper_Text";
        public override void SetDefaults(Item item) => item.SetCartridgeGun<XenopopperHeldProj>(65);
    }
}
