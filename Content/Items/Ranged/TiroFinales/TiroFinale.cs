using CalamityOverhaul.Content.DamageModify;
using CalamityOverhaul.Content.Projectiles;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged.TiroFinales
{
    /// <summary>
    /// 终焉圆舞曲，世纪之花后远程/魔法双系燧发长枪。实弹与魔力双消耗，
    /// 每次开火以金丝带在身周织出一支幻影燧发枪，枪阵环绕轮鸣；
    /// 右键收束全阵奏响终曲。节奏与枪阵逻辑在 <see cref="TiroFinaleHeld"/>
    /// </summary>
    internal class TiroFinale : ModItem
    {
        public override string Texture => CWRConstant.Item_Ranged + "TiroFinale";

        public override void SetDefaults() {
            Item.width = 96;
            Item.height = 28;
            Item.damage = 58;
            Item.DamageType = RangedMagicDamageClass.Instance;
            Item.mana = 6;//每发实弹的魔力，同时是织出一支幻影枪的代价
            Item.useTime = Item.useAnimation = 26;//老式燧发枪的沉稳单发节奏
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 5f;
            Item.UseSound = null;//音效全在持握弹幕，按开火/枪阵/终曲分层
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.autoReuse = true;
            Item.shoot = ProjectileID.Bullet;
            Item.shootSpeed = 13f;
            Item.useAmmo = AmmoID.Bullet;
            Item.rare = ItemRarityID.Lime;
            Item.value = Item.sellPrice(0, 12);
        }

        //物品使用本身不消耗子弹，由持握弹幕在实际开火时自行拾取
        public override bool CanConsumeAmmo(Item ammo, Player player) => BaseHeldGun.AmmoConsumeContext;

        public override bool AltFunctionUse(Player player) => true;

        public override bool CanUseItem(Player player)
            => player.ownedProjectileCounts[ModContent.ProjectileType<TiroFinaleHeld>()] == 0;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback)
            => BaseHeldGun.SpawnHeldProj<TiroFinaleHeld>(player, source);

        public override void AddRecipes() {
            CreateRecipe().
                AddIngredient(ItemID.SpectreBar, 12).
                AddIngredient(ItemID.IllegalGunParts, 1).
                AddIngredient(ItemID.GoldBar, 10).
                AddIngredient(ItemID.Ectoplasm, 8).
                AddTile(TileID.MythrilAnvil).
                Register();
        }
    }
}
