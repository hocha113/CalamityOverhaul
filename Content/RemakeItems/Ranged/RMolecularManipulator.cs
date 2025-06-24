using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RMolecularManipulator : CWRItemOverride
    {
        public override int TargetID => ModContent.ItemType<MolecularManipulator>();
        public override void SetDefaults(Item item) => item.SetCartridgeGun<MolecularManipulatorHeldProj>(480);
    }
}
