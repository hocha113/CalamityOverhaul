using CalamityMod.Items;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.DawnshatterAzureProj;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.Extras
{
    internal class DawnshatterAzure : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "DawnshatterAzure";
        public override void SetDefaults()
        {
            Item.height = Item.width = 54;
            Item.damage = 2709;
            Item.DamageType = DamageClass.Melee;
            Item.noMelee = true;
            Item.useTurn = true;
            Item.noUseGraphic = true;
            Item.useTime = Item.useAnimation = 15;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 5.75f;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.value = CalamityGlobalItem.Rarity3BuyPrice;
            Item.rare = ItemRarityID.Orange;
            Item.shoot = ModContent.ProjectileType<DawnshatterAzureProj>();
            Item.shootSpeed = 8f;
            Item.CWR().OmigaSnyContent = SupertableRecipeDate.FullItems10;
        }

        public override void ModifyWeaponCrit(Player player, ref float crit) => crit += 11;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            int proj = Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }

        public override bool CanUseItem(Player player)
        {
            return player.ownedProjectileCounts[Item.shoot] <= 0;
        }
    }
}
