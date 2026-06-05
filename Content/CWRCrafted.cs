using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Materials;
using CalamityOverhaul.Content.QuestLogs;
using CalamityOverhaul.Content.Tiles;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using static Terraria.ModLoader.ModContent;

namespace CalamityOverhaul.Content
{
    internal class CWRCrafted : ModSystem, ILocalizedModType
    {
        public string LocalizationCategory => "Recipes";

        public static LocalizedText ApostolicRelicsGroup { get; private set; }
        public static LocalizedText GodEaterWeaponGroup { get; private set; }
        public static LocalizedText FishGroupName { get; private set; }

        public static string Any => Language.GetTextValue("LegacyMisc.37");
        public static RecipeGroup ARGroup;
        public static RecipeGroup GodDWGroup;
        public static RecipeGroup FishGroup;
        public static RecipeGroup IronPickaxeGroup;
        public static RecipeGroup TinBarGroup;
        public static RecipeGroup TungstenBarGroup;
        public static RecipeGroup GoldBarGroup;
        public static RecipeGroup AdamantiteBarGroup;
        public static RecipeGroup MythrilBarGroup;

        public static void SpawnAction(Recipe recipe, Item item, List<Item> consumedItems, Item destinationStack) {
            item.TurnToAir();
            Main.LocalPlayer.CWR().InspectOmigaTime = 120;
            CombatText.NewText(Main.LocalPlayer.Hitbox, Main.DiscoColor
                , Language.GetTextValue($"Mods.CalamityOverhaul.Tools.RecipesLoseText"));
        }

        public override void SetStaticDefaults() {
            ApostolicRelicsGroup = this.GetLocalization(nameof(ApostolicRelicsGroup), () => "使徒遗物");
            GodEaterWeaponGroup = this.GetLocalization(nameof(GodEaterWeaponGroup), () => "噬神者武器");
            FishGroupName = this.GetLocalization(nameof(FishGroupName), () => "鱼");
        }

        public override void Unload() {
            ARGroup = null;
            GodDWGroup = null;
            FishGroup = null;
            AdamantiteBarGroup = null;
        }

        private static void ModifyResultContent(Recipe recipe) {
            //添加无尽催化剂的额外联动合成
            {
                if (CWRLoad.EternitySoul > ItemID.None) {
                    if (recipe.HasResult(ItemType<InfinityCatalyst>())) {
                        recipe.AddIngredient(CWRLoad.DeviatingEnergy, InfinityCatalyst.QFD(15));
                        recipe.AddIngredient(CWRLoad.AbomEnergy, InfinityCatalyst.QFD(15));
                        recipe.AddIngredient(CWRLoad.EternalEnergy, InfinityCatalyst.QFD(15));
                    }
                }
                if (CWRLoad.MetanovaBar > ItemID.None) {
                    if (recipe.HasResult(ItemType<InfinityCatalyst>())) {
                        recipe.AddIngredient(CWRLoad.MetanovaBar, InfinityCatalyst.QFD(15));
                    }
                }
            }
        }

        private static void AddResultContent() {
            //添加染缸的合成
            {
                Recipe.Create(ItemID.DyeVat)
                .AddIngredient(ItemID.Wood, 50)
                    .AddTile(TileID.Sawmill)
                    .Register();
            }
            //添加地狱熔炉的合成
            {
                Recipe.Create(ItemID.Hellforge)
                .AddIngredient(ItemID.Furnace)
                    .AddIngredient(ItemID.Hellstone, 10)
                    .AddTile(TileID.Anvils)
                    .Register();
            }
            //添加风暴长矛的合成
            {
                Recipe.Create(ItemID.ThunderSpear)
                .AddIngredient(ItemID.Spear)
                .AddIngredient(CWRID.Item_StormlionMandible, 5)
                .AddTile(TileID.Anvils)
                .Register();
                Recipe.Create(ItemID.ThunderSpear)
                    .AddIngredient(ItemID.Trident)
                    .AddIngredient(CWRID.Item_StormlionMandible, 5)
                    .AddTile(TileID.Anvils)
                    .Register();
            }
            //添加闪光皇后鱼的配方
            {
                Recipe.Create(CWRID.Item_SparklingEmpress)
                    .AddRecipeGroup(FishGroup)
                    .AddIngredient(CWRID.Item_SeaPrism, 15)
                    .AddIngredient(CWRID.Item_PearlShard, 5)
                    .AddTile(TileID.Anvils)
                    .Register();
            }
            //添加硫火鱼的配方
            {
                Recipe.Create(CWRID.Item_DragoonDrizzlefish)
                    .AddRecipeGroup(FishGroup)
                    .AddIngredient(ItemID.Hellstone, 15)
                    .AddIngredient(CWRID.Item_PearlShard, 5)
                    .AddTile(TileID.Anvils)
                    .Register();
            }
            //添加卢克索礼物的合成
            {
                Recipe.Create(CWRID.Item_LuxorsGift)
                    .AddIngredient(ItemID.FossilOre, 5)
                    .AddIngredient(CWRID.Item_PearlShard, 12)
                    .AddTile(TileID.Anvils)
                    .Register();
            }
            //添加雪球炮的合成
            {
                Recipe.Create(ItemID.SnowballCannon)
                    .AddIngredient(ItemID.IllegalGunParts, 1)
                    .AddIngredient(ItemID.SnowBlock, 30)
                    .AddIngredient(ItemID.IceBlock, 50)
                    .AddTile(TileID.Anvils)
                    .Register();
            }
            //添加魔影系列的合成
            {
                //诘责
                Recipe.Create(CWRID.Item_Condemnation)
                    .AddIngredient(ItemID.HallowedRepeater)
                    .AddIngredient(CWRID.Item_AshesofAnnihilation, 12)
                    .AddTile(CWRID.Tile_DraedonsForge)
                    .Register();
                //狞桀
                Recipe.Create(CWRID.Item_Vehemence)
                    .AddIngredient(CWRID.Item_ValkyrieRay)
                    .AddIngredient(CWRID.Item_AshesofAnnihilation, 12)
                    .AddTile(CWRID.Tile_DraedonsForge)
                    .Register();
                //恣睢
                Recipe.Create(CWRID.Item_Violence)
                    .AddIngredient(ItemID.Gungnir)
                    .AddIngredient(CWRID.Item_AshesofAnnihilation, 12)
                    .AddTile(CWRID.Tile_DraedonsForge)
                    .Register();
                //恂戒
                Recipe.Create(CWRID.Item_Vigilance)
                    .AddIngredient(CWRID.Item_DeathstareRod)
                    .AddIngredient(CWRID.Item_AshesofAnnihilation, 12)
                    .AddTile(CWRID.Tile_DraedonsForge)
                    .Register();
                //异端
                Recipe.Create(CWRID.Item_Heresy)
                    .AddIngredient(ItemID.WaterBolt)
                    .AddIngredient(CWRID.Item_AshesofAnnihilation, 12)
                    .AddTile(CWRID.Tile_DraedonsForge)
                    .Register();
            }
            //添加水矢的合成
            {
                Recipe.Create(ItemID.WaterBolt)
                    .AddIngredient(ItemID.Book)
                    .AddIngredient(ItemID.BottledWater, 2)
                    .AddIngredient(ItemID.ManaCrystal, 2)
                    .AddTile(TileID.Bookcases)
                    .Register();
            }
        }

        private static void SetOmigaSnyRecipes() {
            //key代表合成结果，value代表需要的材料列表
            Dictionary<int, string[]> omigaSnyRecipeDic = [];
            foreach (var pair in CWRLoad.ItemIDToOmigaSnyContent) {
                if (pair.Value == null) {
                    continue;
                }
                if (!CWRLoad.ItemAutoloadingOmigaSnyRecipe[pair.Key]) {
                    continue;//如果该物品不需要自动装填终焉合成内容，就跳过它
                }
                omigaSnyRecipeDic.Add(pair.Key, pair.Value);
            }

            //key代表材料，value代表这个材料需要的数量
            Dictionary<int, int> ingredientDic;

            foreach (KeyValuePair<int, string[]> snyContent in omigaSnyRecipeDic) {
                ingredientDic = [];
                foreach (var fullName in snyContent.Value) {
                    int itemID = VaultUtils.GetItemTypeFromFullName(fullName);
                    //不要在材料里面添加空气物品或者添加自己
                    if (itemID == snyContent.Key || itemID == ItemID.None) {
                        continue;
                    }
                    if (!ingredientDic.TryAdd(itemID, 1)) {
                        ingredientDic[itemID]++;
                    }
                }

                if (ingredientDic.Count == 0) {
                    continue;
                }

                Recipe recipe = Recipe.Create(snyContent.Key);
                //进行一下排序，让是终焉物品的材料排在前面
                foreach (var ingredientPair in ingredientDic.OrderByDescending(pair => CWRLoad.ItemIDToOmigaSnyContent[pair.Key] != null)) {
                    if (ingredientPair.Key == ItemID.None || ingredientPair.Value <= 0) {
                        continue;
                    }
                    recipe.AddIngredient(ingredientPair.Key, ingredientPair.Value);
                }
                recipe.AddBlockingSynthesisEvent()
                    .AddTile(TileType<TransmutationOfMatter>())
                    .DisableDecraft()
                    .Register();
            }
        }

        public override void AddRecipes() {
            {//添加终焉合成内容
                if (CWRRef.Has) {
                    SetOmigaSnyRecipes();
                }
            }
            {//添加配方的操作
                if (CWRRef.Has) {
                    AddResultContent();
                }
            }
        }

        public override void PostAddRecipes() {
            //遍历所有配方，执行对应的配方修改，这个应该执行在最前，防止覆盖后续的修改操作
            for (int i = 0; i < Recipe.numRecipes; i++) {
                if (CWRServerConfig.Instance.QuestLog) {
                    Main.recipe[i].AddOnCraftCallback(QLPlayer.CraftedItem);
                }
                if (CWRRef.Has) {
                    ModifyResultContent(Main.recipe[i]);
                }
            }
        }

        public override void AddRecipeGroups() {
            IronPickaxeGroup = new RecipeGroup(() => $"{Any} {Lang.GetItemNameValue(ItemID.IronPickaxe)}",
            [
                ItemID.IronPickaxe,
                ItemID.LeadPickaxe,
            ]);
            RecipeGroup.RegisterGroup("CWRMod:IronPickaxeGroup", IronPickaxeGroup);

            TinBarGroup = new RecipeGroup(() => $"{Any} {Lang.GetItemNameValue(ItemID.TinBar)}",
            [
                ItemID.TinBar,
                ItemID.CopperBar,
            ]);
            RecipeGroup.RegisterGroup("CWRMod:TinBarGroup", TinBarGroup);

            TungstenBarGroup = new RecipeGroup(() => $"{Any} {Lang.GetItemNameValue(ItemID.TungstenBar)}",
            [
                ItemID.TungstenBar,
                ItemID.SilverBar,
            ]);
            RecipeGroup.RegisterGroup("CWRMod:TungstenBarGroup", TungstenBarGroup);

            GoldBarGroup = new RecipeGroup(() => $"{Any} {Lang.GetItemNameValue(ItemID.GoldBar)}",
            [
                ItemID.GoldBar,
                ItemID.PlatinumBar,
            ]);
            RecipeGroup.RegisterGroup("CWRMod:GoldBarGroup", GoldBarGroup);

            AdamantiteBarGroup = new RecipeGroup(() => $"{Any} {Lang.GetItemNameValue(ItemID.AdamantiteBar)}",
            [
                ItemID.AdamantiteBar,
                ItemID.TitaniumBar,
            ]);
            RecipeGroup.RegisterGroup("CWRMod:AdamantiteBarGroup", AdamantiteBarGroup);

            MythrilBarGroup = new RecipeGroup(() => $"{Any} {Lang.GetItemNameValue(ItemID.MythrilBar)}",
            [
                ItemID.MythrilBar,
                ItemID.OrichalcumBar,
            ]);
            RecipeGroup.RegisterGroup("CWRMod:MythrilBarGroup", MythrilBarGroup);

            ARGroup = new RecipeGroup(() => $"{Any} {ApostolicRelicsGroup.Value}",
            [
                CWRID.Item_ArmoredShell,
                CWRID.Item_DarkPlasma,
                CWRID.Item_TwistingNether,
            ]);
            RecipeGroup.RegisterGroup("CWRMod:ARGroup", ARGroup);

            GodDWGroup = new RecipeGroup(() => $"{Any} {GodEaterWeaponGroup.Value}",
            [
                CWRID.Item_Excelsus,
                CWRID.Item_TheObliterator,
                CWRID.Item_Deathwind,
                CWRID.Item_DeathhailStaff,
                CWRID.Item_StaffoftheMechworm,
                CWRID.Item_Eradicator,
                CWRID.Item_CosmicDischarge,
                CWRID.Item_Norfleet,
            ]);
            RecipeGroup.RegisterGroup("CWRMod:GodDWGroup", GodDWGroup);

            FishGroup = new RecipeGroup(() => $"{Any} {FishGroupName.Value}",
            [
                ItemID.Goldfish,
                ItemID.Bass,
                ItemID.Trout,
                ItemID.Salmon,
                ItemID.AtlanticCod,
                ItemID.Tuna,
                ItemID.RedSnapper,
                ItemID.NeonTetra,
                ItemID.ArmoredCavefish,
                ItemID.Damselfish,
                ItemID.CrimsonTigerfish,
                ItemID.FrostMinnow,
                ItemID.PrincessFish,
                ItemID.GoldenCarp,
                ItemID.SpecularFish,
                ItemID.Prismite,
                ItemID.VariegatedLardfish,
                ItemID.FlarefinKoi,
                ItemID.DoubleCod,
                ItemID.Honeyfin,
                ItemID.Obsidifish,
                ItemID.ChaosFish,
                ItemID.Stinkfish,
            ]);
            RecipeGroup.RegisterGroup("CWRMod:FishGroup", FishGroup);
        }
    }
}
