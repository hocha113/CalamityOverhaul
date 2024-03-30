using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    internal class RChlorophyteShotbow : BaseRItem
    {
        public override int TargetID => ItemID.ChlorophyteShotbow;
        public override bool IsVanilla => true;
        public override string TargetToolTipItemName => "Wap_ChlorophyteShotbow_Text";
        public override void SetDefaults(Item item) {
            item.SetHeldProj<ChlorophyteShotbowHeldProj>();
            item.useTime = 20;
            item.damage = 35;
        }
    }
}
