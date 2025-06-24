using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RBlissfulBombardier : CWRItemOverride
    {
        public override int TargetID => ModContent.ItemType<BlissfulBombardier>();
        public override void SetDefaults(Item item) => item.SetCartridgeGun<BlissfulBombardierHeldProj>(40);
    }
}
