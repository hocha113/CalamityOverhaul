using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    internal class RChlorophyteShotbow : ItemOverride
    {
        public override int TargetID => ItemID.ChlorophyteShotbow;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) {
            item.SetHeldProj<ChlorophyteShotbowHeldProj>();
            item.useTime = 22;
            item.damage = 35;
        }
    }
}
