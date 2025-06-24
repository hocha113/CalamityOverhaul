using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RTheBallista : CWRItemOverride
    {
        public override int TargetID => ModContent.ItemType<TheBallista>();
        public override void SetDefaults(Item item) => item.SetHeldProj<TheBallistaHeldProj>();
    }
}
