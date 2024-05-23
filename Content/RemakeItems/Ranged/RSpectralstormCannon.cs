using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RSpectralstormCannon : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<SpectralstormCannon>();
        public override int ProtogenesisID => ModContent.ItemType<SpectralstormCannonEcType>();
        public override string TargetToolTipItemName => "SpectralstormCannonEcType";
        public override void SetDefaults(Item item) => item.SetCartridgeGun<SpectralstormCannonHeldProj>(180);
    }
}
