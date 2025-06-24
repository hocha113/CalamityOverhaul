using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    internal class RChainGun : CWRItemOverride
    {
        public override bool IsVanilla => true;
        public override int TargetID => ItemID.ChainGun;
        public override void SetDefaults(Item item) => item.SetCartridgeGun<ChainGunHeldProj>(200);
    }
}
