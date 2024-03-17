using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Rarities;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RDeathwind : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.Deathwind>();
        public override int ProtogenesisID => ModContent.ItemType<DeathwindEcType>();
        public override string TargetToolTipItemName => "DeathwindEcType";

        public override void Load() {
            SetReadonlyTargetID = TargetID;
        }
        public override void SetDefaults(Item item) {
            item.damage = 248;
            item.DamageType = DamageClass.Ranged;
            item.width = 40;
            item.height = 82;
            item.useTime = 14;
            item.useAnimation = 14;
            item.useStyle = ItemUseStyleID.Shoot;
            item.noMelee = true;
            item.noUseGraphic = true;
            item.knockBack = 5f;
            item.value = CalamityGlobalItem.Rarity14BuyPrice;
            item.rare = ModContent.RarityType<DarkBlue>();
            item.autoReuse = true;
            item.shoot = ModContent.ProjectileType<DeathwindHeldProj>();
            item.shootSpeed = 20f;
            item.useAmmo = AmmoID.Arrow;
            item.Calamity().canFirePointBlankShots = true;
            item.SetHeldProj<DeathwindHeldProj>();
        }

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            item.initialize();
            item.CWR().ai[1] = type;
            if (player.ownedProjectileCounts[ModContent.ProjectileType<DeathwindHeldProj>()] <= 0) {
                item.CWR().ai[0] = Projectile.NewProjectile(source, position, Vector2.Zero
                , ModContent.ProjectileType<DeathwindHeldProj>()
                , damage, knockback, player.whoAmI);
            }
            return false;
        }
    }
}
