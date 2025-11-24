using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.AriaofTheCosmoses
{
    /// <summary>
    /// 寰宇咏叹调
    /// </summary>
    internal class AriaofTheCosmos : ModItem
    {
        public override string Texture => CWRConstant.Item_Magic + "AriaofTheCosmos";

        public override void SetDefaults() {
            Item.damage = 85;
            Item.DamageType = DamageClass.Magic;
            Item.mana = 20;
            Item.width = 52;
            Item.height = 52;
            Item.useTime = 30;
            Item.useAnimation = 30;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.knockBack = 5f;
            Item.value = Item.buyPrice(gold: 50);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = null; // 音效由手持弹幕处理
            Item.autoReuse = false; // 需要手动释放
            Item.shoot = ModContent.ProjectileType<AccretionDisk>();
            Item.shootSpeed = 0f;
            Item.channel = true; // 启用引导，允许持续按住
            
            // 设置手持弹幕
            Item.SetHeldProj<AriaofTheCosmosHeld>();
        }

        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips) {
            tooltips.Add(new TooltipLine(Mod, "ChargeInfo", "按住左键蓄力，释放后发射强大的吸积盘") {
                OverrideColor = new Color(255, 200, 100)
            });
            tooltips.Add(new TooltipLine(Mod, "Stage1", "• 阶段一 (1秒): 基础吸积盘") {
                OverrideColor = Color.Yellow
            });
            tooltips.Add(new TooltipLine(Mod, "Stage2", "• 阶段二 (2秒): 增强吸积盘") {
                OverrideColor = Color.OrangeRed
            });
            tooltips.Add(new TooltipLine(Mod, "Stage3", "• 阶段三 (3秒): 毁灭性吸积盘") {
                OverrideColor = Color.Purple
            });
            tooltips.Add(new TooltipLine(Mod, "DamageInfo", "伤害随蓄力时间增加，最高可达350%") {
                OverrideColor = Color.Cyan
            });
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, 
            Vector2 velocity, int type, int damage, float knockback) {
            // 射击由手持弹幕处理
            return false;
        }

        public override void AddRecipes() {
            // TODO: 添加合成配方
            // 示例配方（可以根据实际需求修改）
            /*
            CreateRecipe()
                .AddIngredient(ItemID.SpellTome)
                .AddIngredient(ItemID.FragmentVortex, 18)
                .AddIngredient(ItemID.LunarBar, 12)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
            */
        }
    }
}
