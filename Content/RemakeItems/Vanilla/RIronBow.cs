using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    /// <summary>
    /// 铁弓
    /// </summary>
    internal class RIronBow : CWRItemOverride
    {
        public override int TargetID => ItemID.IronBow;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => item.SetHeldProj<IronBowHeldProj>();
    }
}
