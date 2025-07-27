﻿using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    internal class RGrenadeLauncher : CWRItemOverride
    {
        public override int TargetID => ItemID.GrenadeLauncher;

        public override void SetDefaults(Item item) {
            item.SetHeldProj<GrenadeLauncherHeldProj>();
            item.CWR().HasCartridgeHolder = true;
            item.CWR().AmmoCapacity = 60;
            item.useTime = 20;
            item.damage = 20;
        }
        public override bool? On_Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) => false;
    }
}
