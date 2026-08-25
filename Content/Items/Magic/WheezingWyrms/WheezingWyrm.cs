using CalamityOverhaul.Content.Projectiles;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.WheezingWyrms
{
    /// <summary>
    /// 哮龙杖，世纪之花后法师喷火杖。冷启动先咳嗽喷烟，点燃后持续喷吐升温，
    /// 焰色从暗红一路烧到炽蓝，越亮越烫；实际节奏与伤害缩放在 <see cref="WheezingWyrmHeld"/>
    /// </summary>
    internal class WheezingWyrm : ModItem
    {
        public override string Texture => CWRConstant.Item_Magic + "WheezingWyrm";

        public override void SetDefaults() {
            Item.width = 47;
            Item.height = 53;
            Item.damage = 54;
            Item.DamageType = DamageClass.Magic;
            Item.mana = 3;
            Item.useTime = Item.useAnimation = 5;//单口龙焰的间隔
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 1.6f;
            Item.UseSound = null;//音效全在持握弹幕，按咳嗽/点燃/喷焰状态走
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<WyrmFlame>();
            Item.shootSpeed = 11.5f;
            Item.rare = ItemRarityID.Lime;
            Item.value = Item.sellPrice(0, 10);
        }

        public override bool CanUseItem(Player player)
            => player.ownedProjectileCounts[ModContent.ProjectileType<WheezingWyrmHeld>()] == 0;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback)
            => BaseHeldGun.SpawnHeldProj<WheezingWyrmHeld>(player, source);

        public override void AddRecipes() {
            CreateRecipe().
                AddIngredient(ItemID.ChlorophyteBar, 12).
                AddIngredient(ItemID.Ectoplasm, 8).
                AddIngredient(ItemID.SoulofFright, 10).
                AddTile(TileID.MythrilAnvil).
                Register();
        }
    }
}
