using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class REternalBlizzard : CWRItemOverride
    {
        public override void SetDefaults(Item item) {
            item.damage = 48;
            item.UseSound = CWRSound.Gun_Crossbow_Shoot with { Volume = 0.7f };
            item.SetHeldProj<EternalBlizzardHeld>();
        }
    }
}
