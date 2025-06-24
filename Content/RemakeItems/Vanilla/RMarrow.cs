using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    /// <summary>
    /// 骸骨弓
    /// </summary>
    internal class RMarrow : CWRItemOverride
    {
        public override int TargetID => ItemID.Marrow;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => item.SetHeldProj<MarrowHeldProj>();
    }
}
