using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Summon.Deepclaws
{
    /// <summary>
    /// 钳渊，深渊龙虾召唤杖。龙虾以钳击突进近战，每第三次钳击命中后
    /// 收势引爆空化钳鸣，范围伤害并把敌人向爆心拖拽。行为在 <see cref="DeepclawLobster"/>
    /// </summary>
    internal class Deepclaw : ModItem
    {
        public override string Texture => CWRConstant.Item_Summon + "Deepclaw";

        public override void SetDefaults() {
            Item.width = 66;
            Item.height = 98;
            Item.damage = 58;
            Item.mana = 10;
            Item.knockBack = 3.5f;
            Item.useTime = Item.useAnimation = 32;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.UseSound = SoundID.Item44;
            Item.noMelee = true;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<DeepclawLobster>();
            Item.shootSpeed = 8f;
            Item.buffType = ModContent.BuffType<DeepclawBuff>();
            Item.DamageType = DamageClass.Summon;
            Item.rare = ItemRarityID.Lime;
            Item.value = Item.sellPrice(0, 10);
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            player.AddBuff(Item.buffType, 2);
            Vector2 spawn = player.Center + new Vector2(player.direction * 16f, -12f);
            Projectile.NewProjectile(source, spawn, velocity * 0.3f + new Vector2(0f, -2.5f)
                , type, damage, knockback, player.whoAmI);
            return false;
        }

        public override void AddRecipes() {
            if (CWRID.Item_Lumenyl > 0 && CWRID.Item_Voidstone > 0 && CWRID.Item_DepthCells > 0) {
                CreateRecipe().
                    AddIngredient(CWRID.Item_Lumenyl, 10).
                    AddIngredient(CWRID.Item_DepthCells, 10).
                    AddIngredient(CWRID.Item_Voidstone, 14).
                    AddIngredient(ItemID.ChlorophyteBar, 6).
                    AddTile(TileID.MythrilAnvil).
                    Register();
                return;
            }
            CreateRecipe().
                AddIngredient(ItemID.ChlorophyteBar, 10).
                AddIngredient(ItemID.SharkFin, 6).
                AddIngredient(ItemID.SoulofNight, 10).
                AddTile(TileID.MythrilAnvil).
                Register();
        }
    }

    /// <summary>钳渊龙虾在场的召唤增益</summary>
    internal class DeepclawBuff : ModBuff
    {
        public override string Texture => CWRConstant.Buff + "DeepclawBuff";

        public override void SetStaticDefaults() {
            Main.buffNoTimeDisplay[Type] = true;
            Main.buffNoSave[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex) {
            if (player.ownedProjectileCounts[ModContent.ProjectileType<DeepclawLobster>()] > 0) {
                player.buffTime[buffIndex] = 18000;
            }
            else {
                player.DelBuff(buffIndex);
                buffIndex--;
            }
        }
    }
}
