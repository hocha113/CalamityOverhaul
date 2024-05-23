using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    /// <summary>
    /// 铁弓
    /// </summary>
    internal class RIronBow : BaseRItem
    {
        public override int TargetID => ItemID.IronBow;
        public override bool IsVanilla => true;
        public override string TargetToolTipItemName => "Wap_IronBow_Text";
        public override void SetDefaults(Item item) => item.SetHeldProj<IronBowHeldProj>();
    }
}
