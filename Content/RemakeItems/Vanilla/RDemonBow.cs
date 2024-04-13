using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.ID;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    internal class RDemonBow : BaseRItem
    {
        public override int TargetID => ItemID.DemonBow;
        public override bool IsVanilla => true;
        public override string TargetToolTipItemName => "Wap_DemonBow_Text";
        public override void SetDefaults(Item item) => item.SetHeldProj<DemonBowHeldProj>();
    }
}
