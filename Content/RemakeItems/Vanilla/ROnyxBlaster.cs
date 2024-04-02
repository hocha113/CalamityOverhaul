using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    internal class ROnyxBlaster : BaseRItem
    {
        public override int TargetID => ItemID.OnyxBlaster;
        public override bool IsVanilla => true;
        public override string TargetToolTipItemName => "Wap_OnyxBlaster_Text";
        public override void SetDefaults(Item item) => item.SetCartridgeGun<OnyxBlasterHeldProj>(8);
    }
}
