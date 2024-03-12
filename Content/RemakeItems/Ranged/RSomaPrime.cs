using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.ModLoader;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RSomaPrime : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<SomaPrime>();
        public override int ProtogenesisID => ModContent.ItemType<SomaPrimeEcType>();
        public override string TargetToolTipItemName => "SomaPrimeEcType";
        public override void SetDefaults(Item item) => item.SetCartridgeGun<SomaPrimeHeldProj>(700);
    }
}
