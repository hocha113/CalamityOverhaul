using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RAnimosity : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<Animosity>();
        public override int ProtogenesisID => ModContent.ItemType<AnimosityEcType>();
        public override string TargetToolTipItemName => "AnimosityEcType";
        public override void SetDefaults(Item item) => item.SetCartridgeGun<AnimosityHeldProj>(55);
    }
}
