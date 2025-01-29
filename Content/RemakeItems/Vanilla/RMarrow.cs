using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    /// <summary>
    /// 骸骨弓
    /// </summary>
    internal class RMarrow : ItemOverride
    {
        public override int TargetID => ItemID.Marrow;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => item.SetHeldProj<MarrowHeldProj>();
    }
}
