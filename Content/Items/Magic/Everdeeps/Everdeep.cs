using CalamityOverhaul.Content.Projectiles;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Everdeeps
{
    /// <summary>
    /// 永渊,深渊魔典。发射环形深渊水流,穿透敌人后折回再击;
    /// 连续命中攒动共鸣,满溢时在敌人身上掀起巨大的水龙卷。
    /// 节奏与共鸣结算在 <see cref="EverdeepHeld"/>
    /// </summary>
    internal class Everdeep : ModItem
    {
        public override string Texture => CWRConstant.Item_Magic + "Everdeep";

        public override void SetDefaults() {
            Item.width = 52;
            Item.height = 86;
            Item.damage = 72;
            Item.DamageType = DamageClass.Magic;
            Item.mana = 11;
            Item.useTime = Item.useAnimation = 26;//一环水流的间隔
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 3.2f;
            Item.UseSound = null;//音效在持握弹幕,按施放/共鸣状态走
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<EverdeepRing>();
            Item.shootSpeed = 12.5f;
            Item.rare = ItemRarityID.Lime;
            Item.value = Item.sellPrice(0, 12);
        }

        public override bool CanUseItem(Player player)
            => player.ownedProjectileCounts[ModContent.ProjectileType<EverdeepHeld>()] == 0;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback)
            => BaseHeldGun.SpawnHeldProj<EverdeepHeld>(player, source);

        public override void AddRecipes() {
            if (CWRID.Item_Lumenyl > 0 && CWRID.Item_Voidstone > 0) {
                CreateRecipe().
                    AddIngredient(ItemID.WaterBolt).
                    AddIngredient(CWRID.Item_Lumenyl, 12).
                    AddIngredient(CWRID.Item_Voidstone, 20).
                    AddTile(TileID.MythrilAnvil).
                    Register();
            }
            else {
                CreateRecipe().
                    AddIngredient(ItemID.WaterBolt).
                    AddIngredient(ItemID.Ectoplasm, 8).
                    AddIngredient(ItemID.SoulofNight, 12).
                    AddTile(TileID.MythrilAnvil).
                    Register();
            }
        }
    }
}
