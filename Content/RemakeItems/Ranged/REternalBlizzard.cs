using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.ModLoader;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class REternalBlizzard : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<EternalBlizzard>();
        public override int ProtogenesisID => ModContent.ItemType<EternalBlizzardEcType>();
        public override string TargetToolTipItemName => "EternalBlizzardEcType";
        public override void SetDefaults(Item item) => item.SetHeldProj<EternalBlizzardHeldProj>();
    }
}
