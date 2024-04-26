using CalamityMod.Items.Weapons.Magic;
using CalamityOverhaul.Content.Items.Magic;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.ModLoader;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Magic
{
    internal class RNanoPurge : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<NanoPurge>();
        public override int ProtogenesisID => ModContent.ItemType<NanoPurgeEcType>();
        public override string TargetToolTipItemName => "NanoPurgeEcType";
        public override void SetDefaults(Item item) => item.SetHeldProj<NanoPurgeHeldProj>();
    }
}
