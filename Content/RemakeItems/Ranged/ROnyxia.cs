using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class ROnyxia : CWRItemOverride
    {
        public override int TargetID => ModContent.ItemType<Onyxia>();
        public override void SetDefaults(Item item) => item.SetCartridgeGun<OnyxiaHeldProj>(280);
        public override bool? On_CanConsumeAmmo(Item weapon, Item ammo, Player player) => Main.rand.NextFloat() > 0.15f;
    }
}
