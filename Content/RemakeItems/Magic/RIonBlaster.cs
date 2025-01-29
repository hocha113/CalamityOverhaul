using CalamityMod.Items.Weapons.Magic;
using CalamityOverhaul.Content.Items.Magic;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Magic
{
    internal class RIonBlaster : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<IonBlaster>();
        public override int ProtogenesisID => ModContent.ItemType<IonBlasterEcType>();
        public override string TargetToolTipItemName => "IonBlasterEcType";
        public override void SetDefaults(Item item) => item.SetHeldProj<IonBlasterHeldProj>();
    }
}
