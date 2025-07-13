﻿using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    internal class RSnowballCannon : CWRItemOverride
    {
        public override int TargetID => ItemID.SnowballCannon;
        
        public override void SetDefaults(Item item) => item.SetCartridgeGun<SnowballCannonHeldProj>(8);
        public override bool? On_Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) => false;
    }
}
