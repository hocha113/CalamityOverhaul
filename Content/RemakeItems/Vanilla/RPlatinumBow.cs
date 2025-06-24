using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    /// <summary>
    /// 铂金弓
    /// </summary>
    internal class RPlatinumBow : CWRItemOverride
    {
        public override int TargetID => ItemID.PlatinumBow;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => item.SetHeldProj<PlatinumBowHeldProj>();
    }
}
