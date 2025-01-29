using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    /// <summary>
    /// 珍珠木弓
    /// </summary>
    internal class RPearlwoodBow : ItemOverride
    {
        public override int TargetID => ItemID.PearlwoodBow;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => item.SetHeldProj<PearlwoodBowHeldProj>();
    }
}
