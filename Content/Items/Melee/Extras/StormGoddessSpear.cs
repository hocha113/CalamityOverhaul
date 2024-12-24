using CalamityMod.Items.Materials;
using CalamityMod.Rarities;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.StormGoddessSpearProj;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.Extras
{
    internal class StormGoddessSpear : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "StormGoddessSpear";
        public override void SetStaticDefaults() {
            //this.GetLocalization("Legend");
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
            Item.channel = true;
            Item.useAnimation = 19;
            Item.useTime = 19;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 9.75f;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.value = Terraria.Item.buyPrice(0, 18, 25, 0);
            Item.shoot = ModContent.ProjectileType<StormGoddessSpearProj>();
            Item.shootSpeed = 15f;
            Item.rare = ModContent.RarityType<DarkBlue>();
        }

        public override void ModifyWeaponCrit(Player player, ref float crit) => crit += 7;

        public override bool CanUseItem(Player player) => player.ownedProjectileCounts[ModContent.ProjectileType<StormGoddessSpearProj>()] <= 0;

        public override void ModifyTooltips(List<TooltipLine> tooltips) => CWRUtils.SetItemLegendContentTops(ref tooltips, Name);

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.ThunderSpear)
                .AddIngredient<CalamityMod.Items.Weapons.Melee.StormRuler>()
                .AddIngredient<CalamityMod.Items.Weapons.Rogue.StormfrontRazor>()
                .AddIngredient<StormlionMandible>(5)
                .AddIngredient(ItemID.LunarBar, 15)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }
    }
}
