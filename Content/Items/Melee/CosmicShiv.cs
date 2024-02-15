using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Rarities;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 宇宙短剑
    /// </summary>
    internal class CosmicShiv : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "CosmicShiv";

        public override void SetDefaults() {
            Item.useStyle = ItemUseStyleID.Rapier;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = 15;
            Item.useTime = 15;
            Item.width = 44;
            Item.height = 44;
            Item.damage = 218;
            Item.knockBack = 9f;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.noUseGraphic = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<RCosmicShivProjectile>();
            Item.shootSpeed = 2.4f;
            Item.value = CalamityGlobalItem.Rarity14BuyPrice;
            Item.rare = ModContent.RarityType<DarkBlue>();
            Item.Calamity().donorItem = true;
            
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            return true;
        }

        public override bool CanUseItem(Player player) {
            return true;
        }

        public override bool? UseItem(Player player) {
            return true;
        }
    }
}
