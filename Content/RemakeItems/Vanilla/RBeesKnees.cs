using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    /// <summary>
    /// 蜂漆弓
    /// </summary>
    internal class RBeesKnees : BaseRItem
    {
        public override int TargetID => ItemID.BeesKnees;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => item.SetHeldProj<BeesKneesHeldProj>();
    }
}
