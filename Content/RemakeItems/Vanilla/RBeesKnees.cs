using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    /// <summary>
    /// 蜂漆弓
    /// </summary>
    internal class RBeesKnees : CWRItemOverride
    {
        public override int TargetID => ItemID.BeesKnees;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => item.SetHeldProj<BeesKneesHeldProj>();
    }
}
