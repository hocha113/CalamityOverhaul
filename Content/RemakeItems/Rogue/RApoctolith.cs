﻿using CalamityMod.Items.Weapons.Rogue;
using CalamityOverhaul.Content.Items.Rogue;
using CalamityOverhaul.Content.Projectiles.Weapons.Rogue.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.DataStructures;
using Terraria.ModLoader;
using Terraria;
using Microsoft.Xna.Framework;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Rogue
{
    internal class RApoctolith : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<Apoctolith>();
        public override int ProtogenesisID => ModContent.ItemType<ApoctolithEcType>();
        public override string TargetToolTipItemName => "ApoctolithEcType";
        public override void SetDefaults(Item item) {
            item.useStyle = ItemUseStyleID.Shoot;
            item.shoot = ModContent.ProjectileType<ApoctolithHeld>();
        }
        public override bool? On_CanUseItem(Item item, Player player) => player.ownedProjectileCounts[item.shoot] <= 6;
        public override bool? On_Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }
    }
}
