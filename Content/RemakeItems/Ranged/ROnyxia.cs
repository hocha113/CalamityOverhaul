using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class ROnyxia : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<Onyxia>();
        public override void SetDefaults(Item item) => item.SetCartridgeGun<OnyxiaHeldProj>(280);
        public override bool? On_CanConsumeAmmo(Item weapon, Item ammo, Player player) => Main.rand.NextFloat() > 0.15f;
    }
}
