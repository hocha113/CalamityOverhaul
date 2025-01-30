using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class REternalBlizzard : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<EternalBlizzard>();

        public override void SetDefaults(Item item) {
            item.damage = 48;
            item.UseSound = CWRSound.Gun_Crossbow_Shoot with { Volume = 0.7f };
            item.SetHeldProj<EternalBlizzardHeldProj>();
        }
    }
}
