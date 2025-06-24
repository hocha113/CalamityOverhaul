﻿using CalamityOverhaul.Content.Projectiles.Weapons.Rogue.HeldProjs.Vanilla;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    internal class RSpear : CWRItemOverride
    {
        public override int TargetID => ItemID.Spear;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) {
            item.UseSound = null;
            item.DamageType = CWRLoad.RogueDamageClass;
            item.shoot = ModContent.ProjectileType<SpearHeld>();
        }
        public override bool? On_CanUseItem(Item item, Player player) => player.ownedProjectileCounts[item.shoot] <= 6;
        public override bool? On_Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }
    }
}
