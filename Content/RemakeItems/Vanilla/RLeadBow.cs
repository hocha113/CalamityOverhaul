using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.ID;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    /// <summary>
    /// 铅弓
    /// </summary>
    internal class RLeadBow : BaseRItem
    {
        public override int TargetID => ItemID.LeadBow;
        public override bool IsVanilla => true;
        public override string TargetToolTipItemName => "Wap_LeadBow_Text";
        public override void SetDefaults(Item item) => item.SetHeldProj<LeadBowHeldProj>();
    }
}
