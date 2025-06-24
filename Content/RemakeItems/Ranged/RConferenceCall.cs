using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RConferenceCall : CWRItemOverride
    {
        public override int TargetID => ModContent.ItemType<ConferenceCall>();
        public override void SetDefaults(Item item) => item.SetCartridgeGun<ConferenceCallHeldProj>(85);
    }
}
