using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RThePack : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<ThePack>();
        public override int ProtogenesisID => ModContent.ItemType<ThePackEcType>();
        public override string TargetToolTipItemName => "ThePackEcType";
        public override void SetDefaults(Item item) => item.SetCartridgeGun<ThePackHeldProj>(12);
    }
}
