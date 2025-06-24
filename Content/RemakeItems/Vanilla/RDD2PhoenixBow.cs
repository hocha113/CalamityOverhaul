using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    /// <summary>
    /// 幽灵凤凰
    /// </summary>
    internal class RDD2PhoenixBow : CWRItemOverride
    {
        public override int TargetID => ItemID.DD2PhoenixBow;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => item.SetHeldProj<DD2PhoenixBowHeldProj>();
    }
}
