using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.ID;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    /// <summary>
    /// 冰雪弓
    /// </summary>
    internal class RIceBow : BaseRItem
    {
        public override int TargetID => ItemID.IceBow;
        public override bool IsVanilla => true;
        public override string TargetToolTipItemName => "Wap_IceBow_Text";
        public override void SetDefaults(Item item) => item.SetHeldProj<IceBowHeldProj>();
    }
}
