using CalamityMod.Items.Materials;
using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Items.Weapons.Rogue;
using CalamityMod.Rarities;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.StormGoddessSpearProj;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    ///<summary>
    ///风暴女神之矛
    ///</summary>
    internal class StormGoddessSpear : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "StormGoddessSpear";

        ///<summary>连击索引计数器</summary>
        private static int comboIndex = 0;

        public override void SetStaticDefaults() {
            ItemID.Sets.AnimatesAsSoul[Type] = true;
            Main.RegisterItemAnimation(Type, new DrawAnimationVertical(5, 5));
        }

        public override void SetDefaults() {
            Item.width = 100;
            Item.height = 100;
            Item.damage = 440;
            Item.DamageType = DamageClass.Melee;
            Item.noMelee = true;
            Item.useTurn = true;
            Item.noUseGraphic = true;
            Item.channel = false; //改为非蓄力
            Item.useAnimation = 18; //加快攻速
            Item.useTime = 18;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 9.75f;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.value = Item.buyPrice(0, 18, 25, 0);
            Item.shoot = ModContent.ProjectileType<StormGoddessSpearHeld>();
            Item.shootSpeed = 18f; //提高射速
            Item.rare = ModContent.RarityType<DarkBlue>();
        }

        public override void ModifyWeaponCrit(Player player, ref float crit) => crit += 10; //提高暴击率

        public override bool CanUseItem(Player player) {
            //允许同时存在多个弹幕（连击系统）
            return player.ownedProjectileCounts[Item.shoot] <= 2;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            //使用连击索引
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI, ai0: comboIndex);

            //循环连击计数
            comboIndex = (comboIndex + 1) % 3;

            return false;
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            //添加武器说明
            tooltips.Add(new TooltipLine(Mod, "StormGoddessSpearCombo",
                "[c/C8E6FF:三段连击系统]"));
            tooltips.Add(new TooltipLine(Mod, "StormGoddessSpearCombo1",
                "[c/C8E6FF:  第一击·天矛突刺：追踪闪电精准打击]"));
            tooltips.Add(new TooltipLine(Mod, "StormGoddessSpearCombo2",
                "[c/96C8FF:  第二击·雷霆横扫：三道闪电扇形攻击]"));
            tooltips.Add(new TooltipLine(Mod, "StormGoddessSpearCombo3",
                "[c/64B4FF:  第三击·风暴上挑：环形闪电直线爆发]"));
            tooltips.Add(new TooltipLine(Mod, "StormGoddessSpearLightning",
                "[c/FFFFFF:释放真实的风暴闪电，分叉、连锁、劈击]"));
            tooltips.Add(new TooltipLine(Mod, "StormGoddessSpearAdrenaline",
                "[c/FFFF64:肾上腺素：第三击释放7道闪电风暴]"));
            tooltips.Add(new TooltipLine(Mod, "StormGoddessSpearCrit",
                "[c/FFD700:暴击生成连锁电弧追击敌人]"));

            CWRUtils.SetItemLegendContentTops(ref tooltips, Name);
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.ThunderSpear)
                .AddIngredient<StormRuler>()
                .AddIngredient<StormfrontRazor>()
                .AddIngredient<StormlionMandible>(5)
                .AddIngredient(ItemID.LunarBar, 15)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }

        public override void HoldItem(Player player) {
            //在手持时添加微弱的电光效果（白蓝色）
            if (Main.rand.NextBool(20)) {
                Vector2 dustPos = player.Center + Main.rand.NextVector2Circular(30, 30);
                Dust dust = Dust.NewDustPerfect(dustPos, DustID.Electric, Vector2.Zero, Scale: 1.2f);
                dust.noGravity = true;
                dust.fadeIn = 1.3f;
                dust.color = new Color(180, 220, 255); //白蓝色
            }
        }
    }
}
