using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    /// <summary>
    /// 棕榈木弓
    /// </summary>
    internal class RPalmWoodBow : ItemOverride
    {
        public override int TargetID => ItemID.PalmWoodBow;
        public override bool IsVanilla => true;
        public override string TargetToolTipItemName => "Wap_WoodenBow_Text";
        public override void SetDefaults(Item item) => item.SetHeldProj<PalmWoodBowHeldProj>();
    }
}
