using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    /// <summary>
    /// 白蜡木弓
    /// </summary>
    internal class RAshWoodBow : CWRItemOverride
    {
        public override int TargetID => ItemID.AshWoodBow;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => item.SetHeldProj<AshWoodBowHeldProj>();
    }
}
