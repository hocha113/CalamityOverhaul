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
    internal class CWRCrafted : ModSystem
    {
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
        public static int[] Gemstones => [
                ItemID.Sapphire,//蓝玉
                ItemID.Ruby,//红宝石
                ItemID.Emerald,//翡翠
                ItemID.Topaz,//黄宝石
                ItemID.Amethyst,//紫水晶
                ItemID.Diamond,//钻石
        ];
        public static void LoadenGemstoneRecipe(int gemstonesID, int dyeID) {
            foreach (var gemstone in Gemstones) {
                if (gemstone == gemstonesID) {
                    continue;
                }
                Recipe.Create(gemstonesID)
                    .AddIngredient(gemstone)
                    .AddIngredient(dyeID)
                    .AddTile(TileID.DyeVat)
                    .DisableDecraft()//不要被微光转化
                    .Register();
            }
        }

        public static void SpawnAction(Recipe recipe, Item item, List<Item> consumedItems, Item destinationStack) {
            item.TurnToAir();
            Main.LocalPlayer.CWR().InspectOmigaTime = 120;
            CombatText.NewText(Main.LocalPlayer.Hitbox, Main.DiscoColor
                , Language.GetTextValue($"Mods.CalamityOverhaul.Tools.RecipesLoseText"));
        }

        public static void MouldRecipeEvent(Recipe recipe, Item item, List<Item> consumedItems, Item destinationStack) {
            MurasamaMould murasamaMould = null;
            foreach (Item i in consumedItems) {
                if (i.type == ItemType<MurasamaMould>()) {
                    murasamaMould = (MurasamaMould)i.ModItem;
                }
            }
            if (murasamaMould != null) {
                murasamaMould.Durability--;
                if (murasamaMould.Durability <= 0) {
                    murasamaMould.Item.TurnToAir();
                }
                else {
                    Main.LocalPlayer.QuickSpawnItem(item.FromObjectGetParent(), murasamaMould.Item);
                }
            }
        }

        public override void Unload() {
            ARGroup = null;
            GodDWGroup = null;
            FishGroup = null;
            AdamantiteBarGroup = null;
        }

        private static void ModifyResultContent(Recipe recipe) {
            //修改雪境暴徒的合成
            {
                if (recipe.HasResult(CWRID.Item_SnowRuffianMask)) {//面具
                    recipe.RemoveIngredient(ItemID.FlinxFur);//移除雪怪皮毛的配方
                    recipe.AddIngredient(ItemID.Leather, 2);//添加皮革
                }
                if (recipe.HasResult(CWRID.Item_SnowRuffianChestplate)) {//胸甲
                    recipe.RemoveIngredient(ItemID.FlinxFur);//移除雪怪皮毛的配方
                    recipe.AddIngredient(ItemID.Leather, 4);//添加皮革
                }
                if (recipe.HasResult(CWRID.Item_SnowRuffianGreaves)) {//护腿
                    recipe.RemoveIngredient(ItemID.FlinxFur);//移除雪怪皮毛的配方
                    recipe.AddIngredient(ItemID.Leather, 2);//添加皮革
                }
            }
            //修改凤凰爆破枪的合成
            {
                if (recipe.HasResult(ItemID.PhoenixBlaster)) {
                    recipe.AddIngredient(CWRID.Item_PurifiedGel, 10);//添加纯净凝胶
                }
            }
            //修改火山系列
            {
                //火山长矛
                if (recipe.HasResult(CWRID.Item_VulcaniteLance)) {
                    recipe.AddIngredient(CWRID.Item_Brimlance);//添加硫磺火矛
                }
                //欧陆巨弓
                if (recipe.HasResult(CWRID.Item_ContinentalGreatbow)) {
                    recipe.AddIngredient(CWRID.Item_BrimstoneFury);//添加硫火之怒
                }
                //地狱风暴
                if (recipe.HasResult(CWRID.Item_Helstorm)) {
                    recipe.AddIngredient(CWRID.Item_Hellborn);//添加地狱降临
                }
            }
            //修改肉后矿弩的合成：添加前置弓以及同级转化
            {
                //秘银弓合成需要钴蓝弓
                if (recipe.HasResult(ItemID.MythrilRepeater)) {
                    recipe.AddIngredient(ItemID.CobaltRepeater, 1);
                }
                //山铜弓合成需要钯金弓
                if (recipe.HasResult(ItemID.OrichalcumRepeater)) {
                    recipe.AddIngredient(ItemID.PalladiumRepeater, 1);
                }
                //精金弓合成需要秘银弓
                if (recipe.HasResult(ItemID.AdamantiteRepeater)) {
                    recipe.AddIngredient(ItemID.MythrilRepeater, 1);
                }
                //钛金弓合成需要山铜弓
                if (recipe.HasResult(ItemID.TitaniumRepeater)) {
                    recipe.AddIngredient(ItemID.OrichalcumRepeater, 1);
                }
                //神圣连弩合成需要精金弩和钛金弩
                if (recipe.HasResult(ItemID.HallowedRepeater)) {
                    recipe.AddIngredient(ItemID.AdamantiteRepeater, 1);
                    recipe.AddIngredient(ItemID.TitaniumRepeater, 1);
                    recipe.AddIngredient(ItemID.Ichor, 5);//添加灵液
                    recipe.AddIngredient(ItemID.CursedFlame, 5);//添加诅咒焰
                    recipe.AddIngredient(ItemID.UnicornHorn, 1);//添加独角兽角
                }
            }
            /// /修改天底的合成
            //{
            //  if (recipe.HasResult(ItemType<Nadir>())) {
            //      recipe.RemoveIngredient(CWRID.Item_AuricBar);//移除圣金源锭的配方
            //      recipe.AddIngredient(ItemType<CosmiliteBar>(), 5);//添加宇宙锭
            //  }
            //}
            //修改天顶剑的合成
            {
                if (recipe.HasResult(ItemID.Zenith)) {
                    recipe.RemoveIngredient(CWRID.Item_AuricBar);//移除圣金源锭的配方
                    recipe.RemoveTile(134);
                    recipe.AddTile(TileID.LunarCraftingStation);
                }
            }
            //修改大守卫者的合成
            {
                if (recipe.HasResult(CWRID.Item_GrandGuardian)) {
                    recipe.RemoveIngredient(ItemID.FragmentNebula);//移除星云碎片
                    recipe.AddIngredient(ItemID.LunarBar, 5);//添加夜明锭
                }
            }
            //修改月神P的合成
            {
                if (recipe.HasResult(CWRID.Item_SomaPrime)) {
                    recipe.AddIngredient(CWRID.Item_Infinity);//添加无穷
                }
            }
            //添加无尽催化剂的额外联动合成
            {
                if (CWRLoad.EternitySoul > ItemID.None) {
                    if (recipe.HasResult(ItemType<InfinityCatalyst>())) {
                        recipe.AddIngredient(CWRLoad.DeviatingEnergy, InfinityCatalyst.QFD(15));
                        recipe.AddIngredient(CWRLoad.AbomEnergy, InfinityCatalyst.QFD(15));
                        recipe.AddIngredient(CWRLoad.EternalEnergy, InfinityCatalyst.QFD(15));
                    }
                    //if (recipe.HasResult(CWRLoad.EternitySoul)) {//永恒魂额外需要5个无尽锭来合成
                    //  recipe.AddIngredient(ItemType<InfiniteIngot>(), 5);
                    //}
                }
                if (CWRLoad.MetanovaBar > ItemID.None) {
                    if (recipe.HasResult(ItemType<InfinityCatalyst>())) {
                        recipe.AddIngredient(CWRLoad.MetanovaBar, InfinityCatalyst.QFD(15));
                    }
                }
            }
            //瘟疫系列修改
            {
                //瘟疫大剑
                if (recipe.HasResult(CWRID.Item_PlagueKeeper)) {
                    recipe.RemoveIngredient(ItemID.LunarBar);//移除夜明锭的配方
                    recipe.AddIngredient(CWRID.Item_Hellkite);//添加地狱龙锋
                    recipe.AddIngredient(ItemType<PestilenceIngot>(), 5);//添加瘟疫锭
                }
                //瘟疫
                if (recipe.HasResult(CWRID.Item_Contagion)) {
                    recipe.RemoveIngredient(CWRID.Item_PlagueCellCanister);//移除瘟疫细胞罐的配方
                    recipe.AddIngredient(ItemType<PestilenceIngot>(), 15);//添加瘟疫锭
                }
                //瘟疫胸甲
                if (recipe.HasResult(CWRID.Item_PlaguebringerCarapace)) {
                    recipe.RemoveIngredient(CWRID.Item_InfectedArmorPlating);//移除瘟疫装甲镀层的配方
                    recipe.RemoveIngredient(CWRID.Item_PlagueCellCanister);//移除瘟疫细胞罐的配方
                    recipe.AddIngredient(ItemType<PestilenceIngot>(), 8);//添加瘟疫锭
                }
                //瘟疫头盔
                if (recipe.HasResult(CWRID.Item_PlaguebringerVisor)) {
                    recipe.RemoveIngredient(CWRID.Item_InfectedArmorPlating);//移除瘟疫装甲镀层的配方
                    recipe.RemoveIngredient(CWRID.Item_PlagueCellCanister);//移除瘟疫细胞罐的配方
                    recipe.AddIngredient(ItemType<PestilenceIngot>(), 5);//添加瘟疫锭
                }
                //瘟疫护腿
                if (recipe.HasResult(CWRID.Item_PlaguebringerPistons)) {
                    recipe.RemoveIngredient(CWRID.Item_InfectedArmorPlating);//移除瘟疫装甲镀层的配方
                    recipe.RemoveIngredient(CWRID.Item_PlagueCellCanister);//移除瘟疫细胞罐的配方
                    recipe.AddIngredient(ItemType<PestilenceIngot>(), 5);//添加瘟疫锭
                }
            }
            //修改拉扎尔射线
            {
                if (recipe.HasResult(CWRID.Item_Lazhar)) {
                    recipe.RemoveIngredient(ItemID.SpaceGun);//移除太空枪的配方
                }
            }
        }

        private static void AddResultContent() {
            //添加煤炭的合成
            //{//添加这个不是个好主意，因为这个"煤"应该不是木炭
            //  Recipe.Create(ItemID.Coal)
            //      .AddRecipeGroup(RecipeGroupID.Wood, 2)
            //      .AddTile(TileID.Furnaces)
            //      .Register();
            //}
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
            //添加宝石的合成
            {
                LoadenGemstoneRecipe(ItemID.Sapphire, ItemID.BlueDye);
                LoadenGemstoneRecipe(ItemID.Ruby, ItemID.RedDye);
                LoadenGemstoneRecipe(ItemID.Emerald, ItemID.GreenDye);
                LoadenGemstoneRecipe(ItemID.Topaz, ItemID.YellowDye);
                LoadenGemstoneRecipe(ItemID.Amethyst, ItemID.PurpleDye);
                LoadenGemstoneRecipe(ItemID.Diamond, ItemID.SkyBlueDye);
            }
            //添加热线枪的合成
            {
                Recipe.Create(ItemID.HeatRay)
                .AddIngredient(ItemID.SpaceGun)
                    .AddIngredient(CWRID.Item_ScoriaBar, 5)
                    .AddTile(TileID.MythrilAnvil)
                    .Register();
            }

            //添加血泪的额外合成
            {
                Recipe.Create(ItemID.BloodMoonStarter)
                    .AddIngredient(CWRID.Item_BloodSample, 50)
                    .AddIngredient(CWRID.Item_BlightedGel, 75)
                    .AddTile(TileID.DemonAltar)
                    .DisableDecraft()
                    .Register();
                Recipe.Create(ItemID.BloodMoonStarter)
                    .AddIngredient(CWRID.Item_RottenMatter, 50)
                    .AddIngredient(CWRID.Item_BlightedGel, 75)
                    .AddTile(TileID.DemonAltar)
                    .DisableDecraft()
                    .Register();
            }

            //添加迈达斯统帅的合成
            {
                Recipe.Create(CWRID.Item_MidasPrime)
                .AddIngredient(CWRID.Item_CrackshotColt)
                .AddIngredient(ItemID.GoldRing)
                .AddTile(TileID.Anvils)
                .Register();
            }
            //添加钱币枪的合成
            {
                Recipe.Create(ItemID.CoinGun)
                .AddIngredient(CWRID.Item_MidasPrime)
                .AddRecipeGroup(AdamantiteBarGroup, 5)
                .AddIngredient(ItemID.PlatinumCoin, 5)
                .AddTile(TileID.MythrilAnvil)
                .Register();
            }
            //添加圣火之刃的合成
            {
                Recipe.Create(CWRID.Item_HolyCollider)
                .AddIngredient(CWRID.Item_CelestialClaymore)
                .AddIngredient(CWRID.Item_DivineGeode, 16)
                .AddTile(TileID.LunarCraftingStation)
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
            //添加瘟疫系列的合成
            {
                //添加瘟疫长矛的合成
                Recipe.Create(CWRID.Item_DiseasedPike)
                    .AddIngredient(CWRID.Item_HellionFlowerSpear)//添加刺花长矛
                    .AddIngredient(ItemType<PestilenceIngot>(), 5)//添加瘟疫锭
                    .AddTile(CWRID.Tile_PlagueInfuser)
                    .Register();

                //添加瘟疫悠悠球的合成
                Recipe.Create(CWRID.Item_Pandemic)
                    .AddIngredient(CWRID.Item_SulphurousGrabber)//添加硫磺掠夺者
                    .AddIngredient(ItemType<PestilenceIngot>(), 5)//添加瘟疫锭
                    .AddTile(CWRID.Tile_PlagueInfuser)
                    .Register();

                //添加毒针的合成
                Recipe.Create(CWRID.Item_TheSyringe)
                    .AddIngredient(ItemType<PestilenceIngot>(), 5)//添加瘟疫锭
                    .AddTile(CWRID.Tile_PlagueInfuser)
                    .Register();

                //添加疫病污染者的合成
                Recipe.Create(CWRID.Item_PestilentDefiler)
                    .AddIngredient(ItemType<PestilenceIngot>(), 8)//添加瘟疫锭
                    .AddTile(CWRID.Tile_PlagueInfuser)
                    .Register();

                //添加蜂巢发射器的合成
                Recipe.Create(CWRID.Item_TheHive)
                    .AddIngredient(ItemType<PestilenceIngot>(), 8)//添加瘟疫锭
                    .AddTile(CWRID.Tile_PlagueInfuser)
                    .Register();

                //添加枯萎散播者的合成
                Recipe.Create(CWRID.Item_BlightSpewer)
                    .AddIngredient(ItemType<PestilenceIngot>(), 8)//添加瘟疫锭
                    .AddTile(CWRID.Tile_PlagueInfuser)
                    .Register();

                //添加蜂毒弓的合成
                Recipe.Create(CWRID.Item_Malevolence)
                    .AddIngredient(ItemType<PestilenceIngot>(), 8)//添加瘟疫锭
                    .AddTile(CWRID.Tile_PlagueInfuser)
                    .Register();

                //添加瘟疫法杖的合成
                Recipe.Create(CWRID.Item_PlagueStaff)
                    .AddIngredient(ItemType<PestilenceIngot>(), 8)//添加瘟疫锭
                    .AddTile(CWRID.Tile_PlagueInfuser)
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
            //添加鬼妖村正的合成
            {
                int _Mould = ItemType<MurasamaMould>();
                int murasamaItem = CWRID.Item_Murasama;

                Recipe.Create(_Mould)
                    .AddIngredient(murasamaItem, 1)
                    .AddIngredient(CWRID.Item_PlasmaDriveCore, 1)
                    .AddIngredient(CWRID.Item_MysteriousCircuitry, 5)
                    .AddIngredient(ItemID.CrimtaneBar, 15)
                    .AddConsumeIngredientCallback((Recipe recipe, int type, ref int amount, bool isDecrafting) => {
                        if (type == murasamaItem)
                            amount = 0;
                    })
                    .AddTile(TileID.Anvils)
                    .DisableDecraft()
                    .Register();

                Recipe.Create(_Mould)
                    .AddIngredient(CWRID.Item_EncryptedSchematicHell, 1)
                    .AddIngredient(ItemID.CrimtaneBar, 15)
                    .AddConsumeIngredientCallback((Recipe recipe, int type, ref int amount, bool isDecrafting) => {
                        if (type == murasamaItem)
                            amount = 0;
                    })
                    .AddTile(TileID.Anvils)
                    .DisableDecraft()
                    .Register();

                Recipe.Create(CWRID.Item_Murasama)
                    .AddIngredient(ItemID.Muramasa, 1)
                    .AddIngredient(ItemID.MeteoriteBar, 1)
                    .AddIngredient(_Mould, 1)
                    .AddOnCraftCallback(MouldRecipeEvent)
                    .AddTile(TileID.Anvils)
                    .DisableDecraft()
                    .Register();

                Recipe.Create(CWRID.Item_Murasama)
                    .AddIngredient(ItemID.Katana, 1)
                    .AddIngredient(ItemID.MeteoriteBar, 1)
                    .AddIngredient(ItemID.CobaltBar, 1)
                    .AddIngredient(_Mould, 1)
                    .AddOnCraftCallback(MouldRecipeEvent)
                    .AddTile(TileID.Anvils)
                    .DisableDecraft()
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
            //添加恒吹雪的合成
            {
                Recipe.Create(CWRID.Item_EternalBlizzard)
                    .AddIngredient(CWRID.Item_Arbalest)
                    .AddIngredient(CWRID.Item_CoreofEleum, 6)
                    .AddIngredient(ItemID.IceBlock, 500)
                    .AddTile(TileID.IceMachine)
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

            ARGroup = new RecipeGroup(() => $"{Any} {CWRLocText.GetTextValue("CWRRecipes_ApostolicRelics")}",
            [
                CWRID.Item_ArmoredShell,
                CWRID.Item_DarkPlasma,
                CWRID.Item_TwistingNether,
            ]);
            RecipeGroup.RegisterGroup("CWRMod:ARGroup", ARGroup);

            GodDWGroup = new RecipeGroup(() => $"{Any} {CWRLocText.GetTextValue("CWRRecipes_GodEaterWeapon")}",
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

            FishGroup = new RecipeGroup(() => $"{Any} {CWRLocText.GetTextValue("CWRRecipes_FishGroup")}",
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
