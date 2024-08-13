using CalamityMod.Items.DraedonMisc;
using CalamityMod.Items.Materials;
using CalamityMod.Items.Placeables;
using CalamityMod.Items.Weapons.Magic;
using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Items.Weapons.Summon;
using CalamityMod.Tiles.Furniture.CraftingStations;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Materials;
using CalamityOverhaul.Content.Items.Melee.Extras;
using CalamityOverhaul.Content.RemakeItems.Core;
using CalamityOverhaul.Content.Tiles;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using static Terraria.ModLoader.ModContent;

namespace CalamityOverhaul.Content
{
    internal class CWRRecipes : ModSystem
    {
        public static string Any => Language.GetTextValue("LegacyMisc.37");
        public static RecipeGroup ARGroup;
        public static RecipeGroup GodDWGroup;
        public static RecipeGroup FishGroup;
        public static void SpawnAction(Recipe recipe, Item item, List<Item> consumedItems, Item destinationStack) {
            item.TurnToAir();
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
                if (CWRServerConfig.Instance.AddExtrasContent) {
                    for (int i = 0; i < Recipe.numRecipes; i++) {
                        Recipe recipe = Main.recipe[i];
                        if (CWRLoad.EternitySoul > ItemID.None) {
                            if (recipe.HasResult(ItemType<InfinityCatalyst>())) {
                                recipe.AddIngredient(CWRLoad.DeviatingEnergy, InfinityCatalyst.QFD(15));
                                recipe.AddIngredient(CWRLoad.AbomEnergy, InfinityCatalyst.QFD(15));
                                recipe.AddIngredient(CWRLoad.EternalEnergy, InfinityCatalyst.QFD(15));
                            }
                            if (recipe.HasResult(CWRLoad.EternitySoul)) {//永恒魂额外需要5个无尽锭来合成
                                recipe.AddIngredient(ItemType<InfiniteIngot>(), 5);
                            }
                        }
                        if (CWRLoad.MetanovaBar > ItemID.None) {
                            if (recipe.HasResult(ItemType<InfinityCatalyst>())) {
                                recipe.AddIngredient(CWRLoad.MetanovaBar, InfinityCatalyst.QFD(15));
                            }
                        }
                    }
                }
            }
            //修改暴政的合成
            {
                for (int i = 0; i < Recipe.numRecipes; i++) {
                    Recipe recipe = Main.recipe[i];
                    if (recipe.HasResult(ItemType<TheEnforcer>())) {
                        recipe.DisableRecipe();
                    }
                }
                Recipe.Create(ItemType<TheEnforcer>())
                    .AddIngredient(ItemType<HolyCollider>())
                    .AddIngredient(ItemType<CosmiliteBar>(), 5)
                    .AddTile(TileType<CosmicAnvil>())
                    .Register();
            }
            //添加圣火之刃的合成
            {
                Recipe.Create(ItemType<HolyCollider>())
                .AddIngredient(ItemType<CelestialClaymore>())
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
            /*添加水瓶的合成事件
            {
                for (int i = 0; i < Recipe.numRecipes; i++) {
                    Recipe recipe = Main.recipe[i];
                    if (recipe.HasResult(ItemID.BottledWater)) {
                        recipe.AddOnCraftCallback(RWaterBottle.OnRecipeBottle);
                    }
                }
            }
            */
            //修改瘟疫系列的合成
            {
                for (int i = 0; i < Recipe.numRecipes; i++) {
                    Recipe recipe = Main.recipe[i];
                    //瘟疫大剑
                    if (recipe.HasResult(ItemType<PlagueKeeper>())) {
                        recipe.RemoveIngredient(ItemID.LunarBar);//移除夜明锭的配方
                        recipe.AddIngredient(ItemType<Hellkite>());//添加地狱龙锋
                        recipe.AddIngredient(ItemType<PestilenceIngot>(), 5);//添加瘟疫锭
                    }
                    //瘟疫
                    if (recipe.HasResult(ItemType<Contagion>())) {
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
                Recipe.Create(ItemType<DiseasedPike>())
                    .AddIngredient(ItemType<HellionFlowerSpear>())//添加花刺长矛
                    .AddIngredient(ItemType<PestilenceIngot>(), 5)//添加瘟疫锭
                    .AddTile(TileType<PlagueInfuser>())
                    .Register();

                //添加瘟疫悠悠球的合成
                Recipe.Create(ItemType<Pandemic>())
                    .AddIngredient(ItemType<SulphurousGrabber>())//添加硫磺掠夺者
                    .AddIngredient(ItemType<PestilenceIngot>(), 5)//添加瘟疫锭
                    .AddTile(TileType<PlagueInfuser>())
                    .Register();

                //添加毒针的合成
                Recipe.Create(ItemType<CalamityMod.Items.Weapons.Rogue.TheSyringe>())
                    .AddIngredient(ItemType<PestilenceIngot>(), 5)//添加瘟疫锭
                    .AddTile(TileType<PlagueInfuser>())
                    .Register();

                //添加疫病污染者的合成
                Recipe.Create(ItemType<PestilentDefiler>())
                    .AddIngredient(ItemType<PestilenceIngot>(), 8)//添加瘟疫锭
                    .AddTile(TileType<PlagueInfuser>())
                    .Register();

                //添加蜂巢发射器的合成
                Recipe.Create(ItemType<TheHive>())
                    .AddIngredient(ItemType<PestilenceIngot>(), 8)//添加瘟疫锭
                    .AddTile(TileType<PlagueInfuser>())
                    .Register();

                //添加枯萎散播者的合成
                Recipe.Create(ItemType<BlightSpewer>())
                    .AddIngredient(ItemType<PestilenceIngot>(), 8)//添加瘟疫锭
                    .AddTile(TileType<PlagueInfuser>())
                    .Register();

                //添加蜂毒弓的合成
                Recipe.Create(ItemType<Malevolence>())
                    .AddIngredient(ItemType<PestilenceIngot>(), 8)//添加瘟疫锭
                    .AddTile(TileType<PlagueInfuser>())
                    .Register();

                //添加瘟疫法杖的合成
                Recipe.Create(ItemType<PlagueStaff>())
                    .AddIngredient(ItemType<PestilenceIngot>(), 8)//添加瘟疫锭
                    .AddTile(TileType<PlagueInfuser>())
                    .Register();

            }
            //修改月神P的合成
            {
                for (int i = 0; i < Recipe.numRecipes; i++) {
                    Recipe recipe = Main.recipe[i];
                    if (recipe.HasResult(ItemType<SomaPrime>())) {
                        recipe.AddIngredient(ItemType<Infinity>());//添加无穷
                    }
                }
            }
            //修改鹅卵石冲击波的合成
            {
                for (int i = 0; i < Recipe.numRecipes; i++) {
                    Recipe recipe = Main.recipe[i];
                    if (recipe.HasResult(ItemType<OpalStriker>())) {
                        recipe.AddIngredient(ItemType<Pebble>(), 8);//添加鹅卵石
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
                int murasamaItem = ItemType<Murasama>();

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

                Recipe.Create(ItemType<Murasama>())
                    .AddIngredient(ItemID.Muramasa, 1)
                    .AddIngredient(ItemID.MeteoriteBar, 1)
                    .AddIngredient(_Mould, 1)
                    .AddOnCraftCallback(MouldRecipeEvent)
                    .AddTile(TileID.Anvils)
                    .DisableDecraft()
                    .Register();

                Recipe.Create(ItemType<Murasama>())
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
                if (CWRServerConfig.Instance.AddExtrasContent) {
                    Recipe.Create(ItemType<DawnshatterAzure>())
                    .AddIngredient(ItemID.FragmentSolar, 17)
                    .AddIngredient(ItemID.DayBreak, 1)
                    .AddIngredient<RedSun>(1)
                    .AddIngredient<DraconicDestruction>(1)
                    .AddIngredient<DragonPow>(1)
                    .AddIngredient<DragonRage>(1)
                    .AddIngredient<BlackMatterStick>(3)
                    .AddBlockingSynthesisEvent()
                    .AddTile(TileType<TransmutationOfMatter>())
                    .Register();
                }
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
                Recipe.Create(ItemType<EternalBlizzard>())
                    .AddIngredient(ItemType<Arbalest>())
                    .AddIngredient(ItemType<CoreofEleum>(), 6)
                    .AddIngredient(ItemID.IceBlock, 500)
                    .AddTile(TileID.IceMachine)
                    .Register();
            }
            //添加魔影系列的合成
            {
                //诘责
                Recipe.Create(ItemType<Condemnation>())
                    .AddIngredient(ItemID.HallowedRepeater)
                    .AddIngredient(ItemType<AshesofAnnihilation>(), 12)
                    .AddTile(TileType<DraedonsForge>())
                    .Register();
                //狞桀
                Recipe.Create(ItemType<Vehemence>())
                    .AddIngredient(ItemType<ValkyrieRay>())
                    .AddIngredient(ItemType<AshesofAnnihilation>(), 12)
                    .AddTile(TileType<DraedonsForge>())
                    .Register();
                //恣睢
                Recipe.Create(ItemType<Violence>())
                    .AddIngredient(ItemID.Gungnir)
                    .AddIngredient(ItemType<AshesofAnnihilation>(), 12)
                    .AddTile(TileType<DraedonsForge>())
                    .Register();
                //恂戒
                Recipe.Create(ItemType<Vigilance>())
                    .AddIngredient(ItemType<DeathstareRod>())
                    .AddIngredient(ItemType<AshesofAnnihilation>(), 12)
                    .AddTile(TileType<DraedonsForge>())
                    .Register();
            }
            //通用遍历修改部分
            {
                for (int i = 0; i < Recipe.numRecipes; i++) {
                    Recipe recipe = Main.recipe[i];
                    //修改凤凰爆破枪的合成
                    {
                        if (recipe.HasResult(ItemID.PhoenixBlaster)) {
                            recipe.AddIngredient(ItemType<PurifiedGel>(), 10);//添加纯净凝胶
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
                    //修改禅心剑的合成
                    {
                        if (recipe.HasResult(ItemType<Ataraxia>())) {
                            recipe.RemoveIngredient(ItemType<AuricBar>());//移除圣金源锭的配方
                        }
                    }
                    //修改天底的合成
                    {
                        if (recipe.HasResult(ItemType<Nadir>())) {
                            recipe.RemoveIngredient(ItemType<AuricBar>());//移除圣金源锭的配方
                            recipe.AddIngredient(ItemType<CosmiliteBar>(), 5);//添加宇宙锭
                        }
                    }
                    //修改天顶剑的合成
                    {
                        if (recipe.HasResult(ItemID.Zenith)) {
                            recipe.RemoveIngredient(ItemType<AuricBar>());//移除圣金源锭的配方
                            recipe.RemoveTile(134);
                            recipe.AddTile(TileID.LunarCraftingStation);
                        }
                    }
                    //修改诅咒手枪的合成
                    {
                        //if (recipe.HasResult(ItemType<CursedCapper>())) {
                        //    recipe.AddIngredient(ItemID.SoulofFright, 5);//添加恐惧之魂
                        //}//TODO:已经移除或者等待重做
                    }
                }
            }
        }

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

            LoadGroup(ref GodDWGroup, "CWRRecipes_GodEaterWeapon", new int[] {ItemType<Excelsus>()
                , ItemType<TheObliterator>()
                , ItemType<Deathwind>()
                , ItemType<DeathhailStaff>()
                , ItemType<CalamityMod.Items.Weapons.Summon.StaffoftheMechworm>()
                , ItemType<CalamityMod.Items.Weapons.Rogue.Eradicator>()
                , ItemType<CosmicDischarge>()
                , ItemType<Norfleet>()
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
