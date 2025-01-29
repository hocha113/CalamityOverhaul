using CalamityMod.Items.Weapons.Magic;
using CalamityOverhaul.Content.Items.Magic;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Magic
{
    internal class RAethersWhisper : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<AethersWhisper>();
        public override int ProtogenesisID => ModContent.ItemType<AethersWhisperEcType>();
        public override string TargetToolTipItemName => "AethersWhisperEcType";
        public override void SetDefaults(Item item) {
            item.useTime = 30;
            item.SetHeldProj<AethersWhisperHeldProj>();
        }
    }
}
