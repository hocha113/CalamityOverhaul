using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.ModLoader;
using Terraria;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RBrimstoneFury : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<BrimstoneFury>();
        public override int ProtogenesisID => ModContent.ItemType<BrimstoneFuryEcType>();
        public override string TargetToolTipItemName => "BrimstoneFuryEcType";
        public override void SetDefaults(Item item) => item.SetHeldProj<BrimstoneFuryHeldProj>();
    }
}
