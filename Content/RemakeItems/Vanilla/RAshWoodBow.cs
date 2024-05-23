using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    /// <summary>
    /// 白蜡木弓
    /// </summary>
    internal class RAshWoodBow : BaseRItem
    {
        public override int TargetID => ItemID.AshWoodBow;
        public override bool IsVanilla => true;
        public override string TargetToolTipItemName => "Wap_WoodenBow_Text";
        public override void SetDefaults(Item item) => item.SetHeldProj<AshWoodBowHeldProj>();
    }
}
