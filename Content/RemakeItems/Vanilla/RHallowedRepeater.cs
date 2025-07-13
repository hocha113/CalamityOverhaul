﻿using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    internal class RHallowedRepeater : CWRItemOverride
    {
        public override int TargetID => ItemID.HallowedRepeater;
        
        public override void SetDefaults(Item item) {
            item.damage = 60;
            item.useTime = 30;
            item.SetHeldProj<HallowedRepeaterHeldProj>();
        }
        public override bool? On_Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) => false;
    }
}
