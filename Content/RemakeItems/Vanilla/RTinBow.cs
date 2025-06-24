using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    /// <summary>
    /// 锡弓
    /// </summary>
    internal class RTinBow : CWRItemOverride
    {
        public override int TargetID => ItemID.TinBow;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => item.SetHeldProj<TinBowHeldProj>();
    }
}
