using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RMonsoon : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<Monsoon>();
        public override int ProtogenesisID => ModContent.ItemType<MonsoonEcType>();
        public override string TargetToolTipItemName => "MonsoonEcType";
        public override void SetDefaults(Item item) => item.SetHeldProj<MonsoonHeldProj>();
    }
}
