using CalamityOverhaul.Content.Projectiles;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged.SneezingWyrms
{
    /// <summary>
        /// 嚏龙铳，世纪之花后速射龙炮，与哮龙杖同窑出炉。快速射出龙击弹，
        /// 连射令枪膛升温、弹色沿黑体色带变亮；憋压的龙鼻间歇打嚏向侧面喷烟。
        /// 节奏与温度逻辑在 <see cref="SneezingWyrmHeld"/>
    /// </summary>
    internal class SneezingWyrm : ModItem
    {
        public override string Texture => CWRConstant.Item_Ranged + "SneezingWyrm";

        public override void SetDefaults() {
            Item.width = 52;
            Item.height = 33;
            Item.damage = 36;
            Item.DamageType = DamageClass.Ranged;
            Item.useTime = Item.useAnimation = 5;//龙击弹射速
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 2f;
            Item.UseSound = null;//开火音效在持握弹幕，按枪膛热度变调
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.autoReuse = true;
            Item.shoot = ProjectileID.Bullet;
            Item.shootSpeed = 13f;
            Item.useAmmo = AmmoID.Bullet;
            Item.rare = ItemRarityID.Lime;
            Item.value = Item.sellPrice(0, 10);
        }

        //物品使用本身不消耗子弹，由手持弹幕在实际开火时自行拾取
        public override bool CanConsumeAmmo(Item ammo, Player player) => BaseHeldGun.AmmoConsumeContext;

        public override bool CanUseItem(Player player)
            => player.ownedProjectileCounts[ModContent.ProjectileType<SneezingWyrmHeld>()] == 0;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback)
            => BaseHeldGun.SpawnHeldProj<SneezingWyrmHeld>(player, source);

        public override void AddRecipes() {
            CreateRecipe().
                AddIngredient(ItemID.ChlorophyteBar, 12).
                AddIngredient(ItemID.Ectoplasm, 8).
                AddIngredient(ItemID.SoulofMight, 10).
                AddTile(TileID.MythrilAnvil).
                Register();
        }
    }
}
