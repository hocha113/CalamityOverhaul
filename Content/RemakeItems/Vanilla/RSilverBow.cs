using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    /// <summary>
    /// 银弓
    /// </summary>
    internal class RSilverBow : CWRItemOverride
    {
        public override int TargetID => ItemID.SilverBow;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => item.SetHeldProj<SilverBowHeldProj>();
    }
}
