using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    /// <summary>
    /// 铂金弓
    /// </summary>
    internal class RPlatinumBow : BaseRItem
    {
        public override int TargetID => ItemID.PlatinumBow;
        public override bool IsVanilla => true;
        public override string TargetToolTipItemName => "Wap_PlatinumBow_Text";
        public override void SetDefaults(Item item) => item.SetHeldProj<PlatinumBowHeldProj>();
    }
}
