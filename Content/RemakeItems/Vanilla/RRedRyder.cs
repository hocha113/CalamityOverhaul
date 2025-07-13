﻿using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    internal class RRedRyder : CWRItemOverride
    {
        public override int TargetID => ItemID.RedRyder;
        
        public override void SetDefaults(Item item) {
            item.useTime = 60;
            item.SetHeldProj<RedRyderHeldProj>();
        }
        public override bool? On_Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) => false;
    }
}
