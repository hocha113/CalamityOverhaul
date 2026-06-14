using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged.Starships
{
    internal class Starship : ModItem
    {
        public override string Texture => CWRConstant.Item_Ranged + "Starship";
        public override void SetDefaults() {
            Item.DamageType = DamageClass.Ranged;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.width = 94;
            Item.height = 34;
            Item.damage = 186;
            Item.useTime = 4;
            Item.useAnimation = 4;
            Item.useAmmo = AmmoID.Bullet;
            Item.shoot = ModContent.ProjectileType<StarshipHeld>();
            Item.shootSpeed = 18f;
            Item.knockBack = 2.5f;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.channel = true;
            Item.autoReuse = true;
            Item.UseSound = null;
            Item.rare = ItemRarityID.Red;
            Item.value = Item.buyPrice(1, 80, 0, 0);
        }

        //右键：装填彗星特殊弹
        public override bool AltFunctionUse(Player player) => true;

        //物品使用本身不消耗子弹，由手持弹幕在实际开火与装填时自行拾取
        public override bool CanConsumeAmmo(Item ammo, Player player) => BaseHeldGun.AmmoConsumeContext;

        public override bool CanUseItem(Player player)
            => player.ownedProjectileCounts[ModContent.ProjectileType<StarshipHeld>()] == 0;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback)
            => BaseHeldGun.SpawnHeldProj<StarshipHeld>(player, source);

        public override void AddRecipes() {
            if (CWRID.Item_Starmada > 0 && CWRID.Item_ShadowspecBar > 0
                && CWRID.Item_Rock > 0 && CWRID.Tile_DraedonsForge > 0) {
                CreateRecipe().AddIngredient(CWRID.Item_Starmada).AddIngredient(CWRID.Item_ShadowspecBar, 5).AddIngredient(CWRID.Item_Rock).AddTile(CWRID.Tile_DraedonsForge).Register();
            }
        }
    }
}
