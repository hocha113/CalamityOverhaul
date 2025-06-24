﻿using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RKingsbane : CWRItemOverride
    {
        public override int TargetID => ModContent.ItemType<Kingsbane>();
        public override void SetDefaults(Item item) {
            item.damage = 115;
            item.shoot = ModContent.ProjectileType<KingsbaneHeldProj>();
            item.SetCartridgeGun<KingsbaneHeldProj>(980);
        }

        public override bool? On_CanConsumeAmmo(Item weapon, Item ammo, Player player) => Main.rand.NextFloat() > 0.35f && player.ownedProjectileCounts[weapon.shoot] > 0;
    }
}
