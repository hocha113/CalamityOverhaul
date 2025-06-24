using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    internal class RChlorophyteShotbow : CWRItemOverride
    {
        public override int TargetID => ItemID.ChlorophyteShotbow;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) {
            item.SetHeldProj<ChlorophyteShotbowHeldProj>();
            item.useTime = 24;
            item.damage = 30;
        }
    }
}
