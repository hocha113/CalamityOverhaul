using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    internal class RDaedalusStormbow : BaseRItem
    {
        public override int TargetID => ItemID.DaedalusStormbow;
        public override bool IsVanilla => true;
        public override string TargetToolTipItemName => "Wap_DaedalusStormbow_Text";
        public override void SetDefaults(Item item) => item.SetHeldProj<DaedalusStormbowHeldProj>();
    }
}
