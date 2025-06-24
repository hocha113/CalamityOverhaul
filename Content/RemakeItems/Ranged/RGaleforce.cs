using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RGaleforce : CWRItemOverride
    {
        public override int TargetID => ModContent.ItemType<Galeforce>();
        public override void SetDefaults(Item item) => item.SetHeldProj<GaleforceHeldProj>();
    }
}
