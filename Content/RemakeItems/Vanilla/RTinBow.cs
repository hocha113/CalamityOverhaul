using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    /// <summary>
    /// 锡弓
    /// </summary>
    internal class RTinBow : ItemOverride
    {
        public override int TargetID => ItemID.TinBow;
        public override bool IsVanilla => true;
        public override string TargetToolTipItemName => "Wap_TinBow_Text";
        public override void SetDefaults(Item item) => item.SetHeldProj<TinBowHeldProj>();
    }
}
