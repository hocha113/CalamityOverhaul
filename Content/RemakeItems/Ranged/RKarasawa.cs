using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RKarasawa : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<Karasawa>();
        public override int ProtogenesisID => ModContent.ItemType<KarasawaEcType>();
        public override string TargetToolTipItemName => "KarasawaEcType";
        public override void SetDefaults(Item item) => item.SetCartridgeGun<KarasawaHeldProj>(6);
    }
}
