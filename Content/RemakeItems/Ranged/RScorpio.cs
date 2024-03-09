using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria.ModLoader;
using Terraria;
using CalamityOverhaul.Content.RemakeItems.Core;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RScorpio : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<Scorpio>();
        public override int ProtogenesisID => ModContent.ItemType<ScorpioEcType>();
        public override string TargetToolTipItemName => "ScorpioEcType";
        public override void SetDefaults(Item item) => item.SetCartridgeGun<ScorpioHeldProj>(5);
    }
}
