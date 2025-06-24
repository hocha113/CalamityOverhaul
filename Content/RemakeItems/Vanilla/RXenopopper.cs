using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    internal class RXenopopper : CWRItemOverride
    {
        public override int TargetID => ItemID.Xenopopper;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => item.SetCartridgeGun<XenopopperHeldProj>(65);
    }
}
