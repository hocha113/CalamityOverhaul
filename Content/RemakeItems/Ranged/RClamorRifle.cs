using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.ModLoader;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RClamorRifle : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<ClamorRifle>();
        public override int ProtogenesisID => ModContent.ItemType<ClamorRifleEcType>();
        public override string TargetToolTipItemName => "ClamorRifleEcType";
        public override void SetDefaults(Item item) => item.SetCartridgeGun<ClamorRifleHeldProj>(45);
    }
}
