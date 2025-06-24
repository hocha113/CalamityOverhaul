using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    internal class RDaedalusStormbow : CWRItemOverride
    {
        public override int TargetID => ItemID.DaedalusStormbow;
        public override bool IsVanilla => true;

        public override void SetDefaults(Item item) => item.SetHeldProj<DaedalusStormbowHeldProj>();
    }
}
