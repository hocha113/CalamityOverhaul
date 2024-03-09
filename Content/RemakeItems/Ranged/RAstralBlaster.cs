using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.ModLoader;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RAstralBlaster : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<AstralBlaster>();
        public override int ProtogenesisID => ModContent.ItemType<AstralBlasterEcType>();
        public override string TargetToolTipItemName => "AstralBlasterEcType";
        public override void SetDefaults(Item item) => item.SetCartridgeGun<AstralBlasterHeldProj>(30);
    }
}
