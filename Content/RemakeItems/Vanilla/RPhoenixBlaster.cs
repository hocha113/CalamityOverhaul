using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    internal class RPhoenixBlaster : BaseRItem
    {
        public override int TargetID => ItemID.PhoenixBlaster;
        public override bool IsVanilla => true;
        public override string TargetToolTipItemName => "Wap_PhoenixBlaster_Text";
        public override void SetDefaults(Item item) => item.SetCartridgeGun<PhoenixBlasterHeldProj>(22);
    }
}
