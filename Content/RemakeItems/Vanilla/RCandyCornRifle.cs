using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    internal class RCandyCornRifle : ItemOverride
    {
        public override int TargetID => ItemID.CandyCornRifle;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => item.SetCartridgeGun<CandyCornRifleHeldProj>(60);
    }
}
