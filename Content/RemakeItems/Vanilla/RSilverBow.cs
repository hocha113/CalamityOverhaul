using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    /// <summary>
    /// 银弓
    /// </summary>
    internal class RSilverBow : BaseRItem
    {
        public override int TargetID => ItemID.SilverBow;
        public override bool IsVanilla => true;
        public override string TargetToolTipItemName => "Wap_SilverBow_Text";
        public override void SetDefaults(Item item) => item.SetHeldProj<SilverBowHeldProj>();
    }
}
