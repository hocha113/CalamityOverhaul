using CalamityMod.Items.DraedonMisc;
using CalamityMod.Items.Materials;
using CalamityMod.Items.Placeables;
using CalamityMod.Tiles.Furniture.CraftingStations;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Materials;
using CalamityOverhaul.Content.Items.Melee.Extras;
using CalamityOverhaul.Content.RemakeItems.Core;
using CalamityOverhaul.Content.RemakeItems.Vanilla;
using CalamityOverhaul.Content.Tiles;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using static Terraria.ModLoader.ModContent;

namespace CalamityOverhaul.Content
{
    //在这个类中无论如何都不应该引入灾厄武器部分的命名空间，这会导致引用混乱，
    //因为 CalamityOverhaul 使用的武器类名是与灾厄一样的，两者需要靠手动的命名引用来区分
    internal class CWRRecipes : ModSystem
    {
        public static void SpawnAction(Recipe recipe, Item item, List<Item> consumedItems, Item destinationStack) {
            item.TurnToAir();
            CombatText.NewText(Main.LocalPlayer.Hitbox, Main.DiscoColor, Language.GetTextValue($"Mods.CalamityOverhaul.Tools.RecipesLoseText"));
        }

        public static void MouldRecipeEvent(Recipe recipe, Item item, List<Item> consumedItems, Item destinationStack) {
            MurasamaMould murasamaMould = null;
            foreach(Item i in consumedItems) {
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
                    Main.LocalPlayer.QuickSpawnItem(item.parent(), murasamaMould.Item);
                }
            }
        }

        public override void PostAddRecipes() {
            if (!CWRConstant.ForceReplaceResetContent) {
                foreach (BaseRItem baseRItem in CWRMod.RItemInstances) {
                    if (baseRItem.FormulaSubstitution)
                        baseRItem.LoadItemRecipe();
                }
            }

            //添加血泪的额外合成
            {
                Recipe.Create(ItemID.BloodMoonStarter)
                    .AddIngredient(ItemType<BloodSample>(), 50)
                    .AddIngredient(ItemType<BlightedGel>(), 75)
                    .AddTile(TileID.DemonAltar)
                    .Register();
                Recipe.Create(ItemID.BloodMoonStarter)
                    .AddIngredient(ItemType<RottenMatter>(), 50)
                    .AddIngredient(ItemType<BlightedGel>(), 75)
                    .AddTile(TileID.DemonAltar)
                    .Register();
            }
            //添加无尽催化剂的额外联动合成
            {
                for (int i = 0; i < Recipe.numRecipes; i++) {
                    Recipe recipe = Main.recipe[i];
                    if (CWRIDs.EternitySoul > ItemID.None) {
                        if (recipe.HasResult(ItemType<InfinityCatalyst>())) {
                            recipe.AddIngredient(CWRIDs.DeviatingEnergy, InfinityCatalyst.QFD(15));
                            recipe.AddIngredient(CWRIDs.AbomEnergy, InfinityCatalyst.QFD(15));
                            recipe.AddIngredient(CWRIDs.EternalEnergy, InfinityCatalyst.QFD(15));
                        }
                        if (recipe.HasResult(CWRIDs.EternitySoul)) {//永恒魂额外需要5个无尽锭来合成
                            recipe.AddIngredient(ItemType<InfiniteIngot>(), InfinityCatalyst.QFD(5));
                        }
                    }
                    if (CWRIDs.MetanovaBar > ItemID.None) {
                        if (recipe.HasResult(ItemType<InfinityCatalyst>())) {
                            recipe.AddIngredient(CWRIDs.MetanovaBar, InfinityCatalyst.QFD(15));
                        }
                    }
                }
            }
            //修改暴政的合成
            {
                for (int i = 0; i < Recipe.numRecipes; i++) {
                    Recipe recipe = Main.recipe[i];
                    if (recipe.HasResult(ItemType<CalamityMod.Items.Weapons.Melee.TheEnforcer>())) {
                        recipe.DisableRecipe();
                    }
                }
                Recipe.Create(ItemType<CalamityMod.Items.Weapons.Melee.TheEnforcer>())
                    .AddIngredient(ItemType<CalamityMod.Items.Weapons.Melee.HolyCollider>())
                    .AddIngredient(ItemType<CosmiliteBar>(), 5)
                    .AddTile(TileType<CosmicAnvil>())
                    .Register();
            }
            //添加圣火之刃的合成
            {
                Recipe.Create(ItemType<CalamityMod.Items.Weapons.Melee.HolyCollider>())
                .AddIngredient(ItemType<CalamityMod.Items.Weapons.Melee.CelestialClaymore>())
                .AddIngredient(ItemType<DivineGeode>(), 16)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
            }
            //添加风暴长矛的合成
            {
                Recipe.Create(ItemID.ThunderSpear)
                .AddIngredient(ItemID.Spear)
                .AddIngredient<StormlionMandible>(5)
                .AddTile(TileID.Anvils)
                .Register();
                Recipe.Create(ItemID.ThunderSpear)
                    .AddIngredient(ItemID.Trident)
                    .AddIngredient<StormlionMandible>(5)
                    .AddTile(TileID.Anvils)
                    .Register();
            }
            //添加水瓶的合成事件
            {
                for (int i = 0; i < Recipe.numRecipes; i++) {
                    Recipe recipe = Main.recipe[i];
                    if (recipe.HasResult(ItemID.BottledWater)) {
                        recipe.AddOnCraftCallback(RWaterBottle.OnRecipeBottle);
                    }
                }
            }
            //修改瘟疫系列的合成
            {
                for (int i = 0; i < Recipe.numRecipes; i++) {
                    Recipe recipe = Main.recipe[i];
                    //瘟疫大剑
                    if (recipe.HasResult(ItemType<CalamityMod.Items.Weapons.Melee.PlagueKeeper>())) {
                        recipe.RemoveIngredient(ItemID.LunarBar);//移除夜明锭的配方
                        recipe.AddIngredient(ItemType<CalamityMod.Items.Weapons.Melee.Hellkite>());//添加地狱龙锋
                        recipe.AddIngredient(ItemType<PestilenceIngot>(), 5);//添加瘟疫锭
                    }
                    //瘟疫
                    if (recipe.HasResult(ItemType<CalamityMod.Items.Weapons.Ranged.Contagion>())) {
                        recipe.RemoveIngredient(ItemType<PlagueCellCanister>());//移除瘟疫细胞罐的配方
                        recipe.AddIngredient(ItemType<PestilenceIngot>(), 15);//添加瘟疫锭
                    }
                    //瘟疫胸甲
                    if (recipe.HasResult(ItemType<CalamityMod.Items.Armor.Plaguebringer.PlaguebringerCarapace>())) {
                        recipe.RemoveIngredient(ItemType<InfectedArmorPlating>());//移除瘟疫装甲镀层的配方
                        recipe.RemoveIngredient(ItemType<PlagueCellCanister>());//移除瘟疫细胞罐的配方
                        recipe.AddIngredient(ItemType<PestilenceIngot>(), 8);//添加瘟疫锭
                    }
                    //瘟疫头盔
                    if (recipe.HasResult(ItemType<CalamityMod.Items.Armor.Plaguebringer.PlaguebringerVisor>())) {
                        recipe.RemoveIngredient(ItemType<InfectedArmorPlating>());//移除瘟疫装甲镀层的配方
                        recipe.RemoveIngredient(ItemType<PlagueCellCanister>());//移除瘟疫细胞罐的配方
                        recipe.AddIngredient(ItemType<PestilenceIngot>(), 5);//添加瘟疫锭
                    }
                    //瘟疫护腿
                    if (recipe.HasResult(ItemType<CalamityMod.Items.Armor.Plaguebringer.PlaguebringerPistons>())) {
                        recipe.RemoveIngredient(ItemType<InfectedArmorPlating>());//移除瘟疫装甲镀层的配方
                        recipe.RemoveIngredient(ItemType<PlagueCellCanister>());//移除瘟疫细胞罐的配方
                        recipe.AddIngredient(ItemType<PestilenceIngot>(), 5);//添加瘟疫锭
                    }
                }
                //添加瘟疫长矛的合成
                Recipe.Create(ItemType<CalamityMod.Items.Weapons.Melee.DiseasedPike>())
                    .AddIngredient(ItemType<CalamityMod.Items.Weapons.Melee.HellionFlowerSpear>(), 5)//添加花刺长矛
                    .AddIngredient(ItemType<PestilenceIngot>(), 5)//添加瘟疫锭
                    .AddTile(TileType<PlagueInfuser>())
                    .Register();

                //添加瘟疫悠悠球的合成
                Recipe.Create(ItemType<CalamityMod.Items.Weapons.Melee.Pandemic>())
                    .AddIngredient(ItemType< CalamityMod.Items.Weapons.Melee.SulphurousGrabber>())//添加硫磺掠夺者
                    .AddIngredient(ItemType<PestilenceIngot>(), 5)//添加瘟疫锭
                    .AddTile(TileType<PlagueInfuser>())
                    .Register();

                //添加毒针的合成
                Recipe.Create(ItemType<CalamityMod.Items.Weapons.Rogue.TheSyringe>())
                    .AddIngredient(ItemType<PestilenceIngot>(), 5)//添加瘟疫锭
                    .AddTile(TileType<PlagueInfuser>())
                    .Register();

                //添加疫病污染者的合成
                Recipe.Create(ItemType<CalamityMod.Items.Weapons.Ranged.PestilentDefiler>())
                    .AddIngredient(ItemType<PestilenceIngot>(), 8)//添加瘟疫锭
                    .AddTile(TileType<PlagueInfuser>())
                    .Register();

                //添加蜂巢发射器的合成
                Recipe.Create(ItemType<CalamityMod.Items.Weapons.Ranged.TheHive>())
                    .AddIngredient(ItemType<PestilenceIngot>(), 8)//添加瘟疫锭
                    .AddTile(TileType<PlagueInfuser>())
                    .Register();

                //添加枯萎散播者的合成
                Recipe.Create(ItemType<CalamityMod.Items.Weapons.Ranged.BlightSpewer>())
                    .AddIngredient(ItemType<PestilenceIngot>(), 8)//添加瘟疫锭
                    .AddTile(TileType<PlagueInfuser>())
                    .Register();

                //添加蜂毒弓的合成
                Recipe.Create(ItemType<CalamityMod.Items.Weapons.Ranged.Malevolence>())
                    .AddIngredient(ItemType<PestilenceIngot>(), 8)//添加瘟疫锭
                    .AddTile(TileType<PlagueInfuser>())
                    .Register();

                //添加瘟疫法杖的合成
                Recipe.Create(ItemType<CalamityMod.Items.Weapons.Magic.PlagueStaff>())
                    .AddIngredient(ItemType<PestilenceIngot>(), 8)//添加瘟疫锭
                    .AddTile(TileType<PlagueInfuser>())
                    .Register();

            }
            //修改月神P的合成
            {
                for (int i = 0; i < Recipe.numRecipes; i++) {
                    Recipe recipe = Main.recipe[i];
                    if (recipe.HasResult(ItemType<CalamityMod.Items.Weapons.Ranged.SomaPrime>())) {
                        recipe.AddIngredient(ItemType<CalamityMod.Items.Weapons.Ranged.Infinity>());//添加无穷
                    }
                }
            }
            //修改鹅卵石冲击波的合成
            {
                for (int i = 0; i < Recipe.numRecipes; i++) {
                    Recipe recipe = Main.recipe[i];
                    if (recipe.HasResult(ItemType<CalamityMod.Items.Weapons.Ranged.OpalStriker>())) {
                        recipe.AddIngredient(ItemType<Pebble>());//添加鹅卵石
                    }
                }
            }
            //添加闪光皇后鱼的配方
            {
                Recipe.Create(ItemType<CalamityMod.Items.Fishing.SunkenSeaCatches.SparklingEmpress>())
                    .AddRecipeGroup(FishGroup)
                    .AddIngredient(ItemType<SeaPrism>(), 15)
                    .AddIngredient(ItemType<PearlShard>(), 5)
                    .AddTile(TileID.Anvils)
                    .Register();
            }
            //添加硫火鱼的配方
            {
                Recipe.Create(ItemType<CalamityMod.Items.Fishing.BrimstoneCragCatches.DragoonDrizzlefish>())
                    .AddRecipeGroup(FishGroup)
                    .AddIngredient(ItemID.Hellstone, 15)
                    .AddIngredient(ItemType<PearlShard>(), 5)
                    .AddTile(TileID.Anvils)
                    .Register();
            }
            //添加鬼妖村正的合成
            {
                int _Mould = ItemType<MurasamaMould>();
                int murasamaItem = ItemType<CalamityMod.Items.Weapons.Melee.Murasama>();

                Recipe.Create(_Mould)
                    .AddIngredient(murasamaItem, 1)
                    .AddIngredient(ItemType<PlasmaDriveCore>(), 1)
                    .AddIngredient(ItemType<MysteriousCircuitry>(), 5)
                    .AddIngredient(ItemID.CrimtaneBar, 15)
                    .AddConsumeItemCallback((Recipe recipe, int type, ref int amount) => {
                        if (type == murasamaItem)
                            amount = 0;
                    })
                    .AddTile(TileID.Anvils)
                    .DisableDecraft()
                    .Register();

                Recipe.Create(_Mould)
                    .AddIngredient(ItemType<EncryptedSchematicHell>(), 1)
                    .AddIngredient(ItemID.CrimtaneBar, 15)
                    .AddConsumeItemCallback((Recipe recipe, int type, ref int amount) => {
                        if (type == murasamaItem)
                            amount = 0;
                    })
                    .AddTile(TileID.Anvils)
                    .DisableDecraft()
                    .Register();

                Recipe.Create(ItemType<CalamityMod.Items.Weapons.Melee.Murasama>())
                    .AddIngredient(ItemID.Muramasa, 1)
                    .AddIngredient(ItemID.MeteoriteBar, 1)
                    .AddIngredient(_Mould, 1)
                    .AddOnCraftCallback(MouldRecipeEvent)
                    .AddTile(TileID.Anvils)
                    .DisableDecraft()
                    .Register();

                Recipe.Create(ItemType<CalamityMod.Items.Weapons.Melee.Murasama>())
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
                Recipe.Create(ItemType<CalamityMod.Items.Accessories.LuxorsGift>())
                    .AddIngredient(ItemID.FossilOre, 5)
                    .AddIngredient(ItemType<PearlShard>(), 12)
                    .AddTile(TileID.Anvils)
                    .Register();
            }
            //添加苍穹破晓合成
            {
                Recipe.Create(ItemType<DawnshatterAzure>())
                    .AddIngredient(ItemID.FragmentSolar, 17)
                    .AddIngredient(ItemID.DayBreak, 1)
                    .AddIngredient<CalamityMod.Items.Weapons.Melee.RedSun>(1)
                    .AddIngredient<CalamityMod.Items.Weapons.Melee.DraconicDestruction>(1)
                    .AddIngredient<CalamityMod.Items.Weapons.Melee.DragonPow>(1)
                    .AddIngredient<CalamityMod.Items.Weapons.Melee.DragonRage>(1)
                    .AddIngredient<BlackMatterStick>(3)
                    .AddConsumeItemCallback((Recipe recipe, int type, ref int amount) => {
                        amount = 0;
                    })
                    .AddOnCraftCallback(SpawnAction)
                    .AddTile(TileType<TransmutationOfMatter>())
                    .Register();
            }
            //添加万变之星的相关配方，为了防止被额外修改或者再次被增删改动，这个部分的代码实现应该放在最后面
            /*
            {
                for (int i = 0; i < ItemLoader.ItemCount; i++) {
                    Item item = new Item(i);
                    if (item != null && item.type != ItemID.None) {
                        Console.WriteLine("添加合成结果ID:" + i + "-----" + item + "-----");
                        Recipe.Create(i)
                            .AddIngredient(CWRIDs.StarMyriadChanges)
                            .AddOnCraftCallback(StarMyriadChanges.RecipeEvent)
                            .DisableDecraft()//我们不能让游戏正常加载这个东西的微光分解配方，这不好
                            .Register();
                    }
                }
            }
            */
        }

        public static string Any => Language.GetTextValue("LegacyMisc.37");
        public static RecipeGroup ARGroup;
        public static RecipeGroup GodDWGroup;
        public static RecipeGroup FishGroup;

        public override void AddRecipeGroups() {
            void LoadGroup(ref RecipeGroup group, string key, int[] itemIDs) {
                string name = CWRLocText.GetTextValue(key);
                group = new RecipeGroup(() => $"{Any} {name}", itemIDs);
                RecipeGroup.RegisterGroup(name, group);
            }

            LoadGroup(ref ARGroup, "CWRRecipes_ApostolicRelics", new int[] { ItemType<ArmoredShell>()
                , ItemType<DarkPlasma>()
                , ItemType<TwistingNether>() 
            });

            LoadGroup(ref GodDWGroup, "CWRRecipes_GodEaterWeapon", new int[] {ItemType<CalamityMod.Items.Weapons.Melee.Excelsus>()
                , ItemType<CalamityMod.Items.Weapons.Melee.TheObliterator>()
                , ItemType<CalamityMod.Items.Weapons.Ranged.Deathwind>()
                , ItemType<CalamityMod.Items.Weapons.Magic.DeathhailStaff>()
                , ItemType<CalamityMod.Items.Weapons.Summon.StaffoftheMechworm>()
                , ItemType<CalamityMod.Items.Weapons.Rogue.Eradicator>()
                , ItemType<CalamityMod.Items.Weapons.Melee.CosmicDischarge>()
                , ItemType<CalamityMod.Items.Weapons.Ranged.Norfleet>() 
            });

            LoadGroup(ref FishGroup, "CWRRecipes_FishGroup", new int[] {ItemID.Fish
                , ItemID.Goldfish
                , ItemID.Bass
                , ItemID.Trout
                , ItemID.Salmon
                , ItemID.AtlanticCod
                , ItemID.Tuna
                , ItemID.RedSnapper
                , ItemID.NeonTetra
                , ItemID.ArmoredCavefish
                , ItemID.Damselfish
                , ItemID.CrimsonTigerfish
                , ItemID.FrostMinnow
                , ItemID.PrincessFish
                , ItemID.GoldenCarp
                , ItemID.SpecularFish
                , ItemID.Prismite
                , ItemID.VariegatedLardfish
                , ItemID.FlarefinKoi
                , ItemID.DoubleCod
                , ItemID.Honeyfin
                , ItemID.Obsidifish
                , ItemID.ChaosFish
                , ItemID.Stinkfish
            });
        }
    }
}
