using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.ModLoader;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RKingsbane : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<Kingsbane>();
        public override int ProtogenesisID => ModContent.ItemType<KingsbaneEctype>();
        public override string TargetToolTipItemName => "KingsbaneEctype";
        public override void SetDefaults(Item item) => item.SetCartridgeGun<KingsbaneHeldProj>(1200);
    }
}
