using CalamityOverhaul.Content.QuestLogs;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

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

        private static void AddResultContent() {
            //染缸
            {
                Recipe.Create(ItemID.DyeVat)
                .AddIngredient(ItemID.Wood, 50)
                    .AddTile(TileID.Sawmill)
                    .Register();
            }
            //地狱熔炉
            {
                Recipe.Create(ItemID.Hellforge)
                .AddIngredient(ItemID.Furnace)
                    .AddIngredient(ItemID.Hellstone, 10)
                    .AddTile(TileID.Anvils)
                    .Register();
            }
            //风暴长矛
            {
                if (CWRID.Item_StormlionMandible > 0) {
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
            }
            //闪光皇后鱼
            {
                if (CWRID.Item_SparklingEmpress > 0 && CWRID.Item_SeaPrism > 0 && CWRID.Item_PearlShard > 0) {
                    Recipe.Create(CWRID.Item_SparklingEmpress)
                        .AddRecipeGroup(FishGroup)
                        .AddIngredient(CWRID.Item_SeaPrism, 15)
                        .AddIngredient(CWRID.Item_PearlShard, 5)
                        .AddTile(TileID.Anvils)
                        .Register();
                }
            }
            //硫火鱼
            {
                if (CWRID.Item_DragoonDrizzlefish > 0 && CWRID.Item_PearlShard > 0) {
                    Recipe.Create(CWRID.Item_DragoonDrizzlefish)
                        .AddRecipeGroup(FishGroup)
                        .AddIngredient(ItemID.Hellstone, 15)
                        .AddIngredient(CWRID.Item_PearlShard, 5)
                        .AddTile(TileID.Anvils)
                        .Register();
                }
            }
            //卢克索礼物
            {
                if (CWRID.Item_LuxorsGift > 0 && CWRID.Item_PearlShard > 0) {
                    Recipe.Create(CWRID.Item_LuxorsGift)
                        .AddIngredient(ItemID.FossilOre, 5)
                        .AddIngredient(CWRID.Item_PearlShard, 12)
                        .AddTile(TileID.Anvils)
                        .Register();
                }
            }
            //雪球炮
            {
                Recipe.Create(ItemID.SnowballCannon)
                    .AddIngredient(ItemID.IllegalGunParts, 1)
                    .AddIngredient(ItemID.SnowBlock, 30)
                    .AddIngredient(ItemID.IceBlock, 50)
                    .AddTile(TileID.Anvils)
                    .Register();
            }
            //魔影系列
            {
                if (CWRID.Tile_DraedonsForge > 0 && CWRID.Item_AshesofAnnihilation > 0) {
                    //诘责
                    if (CWRID.Item_Condemnation > 0) {
                        Recipe.Create(CWRID.Item_Condemnation)
                            .AddIngredient(ItemID.HallowedRepeater)
                            .AddIngredient(CWRID.Item_AshesofAnnihilation, 12)
                            .AddTile(CWRID.Tile_DraedonsForge)
                            .Register();
                    }
                    //狞桀
                    if (CWRID.Item_Vehemence > 0 && CWRID.Item_ValkyrieRay > 0) {
                        Recipe.Create(CWRID.Item_Vehemence)
                            .AddIngredient(CWRID.Item_ValkyrieRay)
                            .AddIngredient(CWRID.Item_AshesofAnnihilation, 12)
                            .AddTile(CWRID.Tile_DraedonsForge)
                            .Register();
                    }
                    //恣睢
                    if (CWRID.Item_Violence > 0) {
                        Recipe.Create(CWRID.Item_Violence)
                            .AddIngredient(ItemID.Gungnir)
                            .AddIngredient(CWRID.Item_AshesofAnnihilation, 12)
                            .AddTile(CWRID.Tile_DraedonsForge)
                            .Register();
                    }
                    //恂戒
                    if (CWRID.Item_Vigilance > 0 && CWRID.Item_DeathstareRod > 0) {
                        Recipe.Create(CWRID.Item_Vigilance)
                            .AddIngredient(CWRID.Item_DeathstareRod)
                            .AddIngredient(CWRID.Item_AshesofAnnihilation, 12)
                            .AddTile(CWRID.Tile_DraedonsForge)
                            .Register();
                    }
                    //异端
                    if (CWRID.Item_Heresy > 0) {
                        Recipe.Create(CWRID.Item_Heresy)
                            .AddIngredient(ItemID.WaterBolt)
                            .AddIngredient(CWRID.Item_AshesofAnnihilation, 12)
                            .AddTile(CWRID.Tile_DraedonsForge)
                            .Register();
                    }
                }
            }
            //水矢
            {
                Recipe.Create(ItemID.WaterBolt)
                    .AddIngredient(ItemID.Book)
                    .AddIngredient(ItemID.BottledWater, 2)
                    .AddIngredient(ItemID.ManaCrystal, 2)
                    .AddTile(TileID.Bookcases)
                    .Register();
            }
        }

        public override void AddRecipes() {
            AddResultContent();
        }

        public override void PostAddRecipes() {
            for (int i = 0; i < Recipe.numRecipes; i++) {
                Main.recipe[i].AddOnCraftCallback(QLPlayer.CraftedItem);
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
