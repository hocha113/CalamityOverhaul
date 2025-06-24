using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    /// <summary>
    /// 棕榈木弓
    /// </summary>
    internal class RPalmWoodBow : CWRItemOverride
    {
        public override int TargetID => ItemID.PalmWoodBow;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => item.SetHeldProj<PalmWoodBowHeldProj>();
    }
}
