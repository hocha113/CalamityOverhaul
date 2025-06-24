using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    /// <summary>
    /// 铅弓
    /// </summary>
    internal class RLeadBow : CWRItemOverride
    {
        public override int TargetID => ItemID.LeadBow;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => item.SetHeldProj<LeadBowHeldProj>();
    }
}
