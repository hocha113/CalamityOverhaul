using CalamityOverhaul.Content.Items.Accessories;
using CalamityOverhaul.Content.Tiles;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using static Terraria.ModLoader.ModContent;

namespace CalamityOverhaul.Content.Items.Materials
{
    internal class InfinityCatalyst : ModItem, ICWRLoader
    {
        public override string Texture => CWRConstant.Item + "Materials/InfinityCatalyst";
        public static float QFH;
        void ICWRLoader.SetupData() {
            const float baseBonus = 1.0f;
            var modBonuses = new Dictionary<string, float>{
                    {"LightAndDarknessMod", 0.1f},
                    {"DDmod", 0.1f},
                    {"MaxStackExtra", 0.1f},
                    {"Wild", 0.1f},
                    {"Coralite", 0.1f},
                    {"AncientsAwakened", 0.1f},
                    {"NoxusBoss", 0.25f},
                    {"FargowiltasSouls", 0.25f},
                    {"MagicBuilder", 0.25f},
                    {"CalamityPostMLBoots", 0.25f},
                };
            float overMdgs = ModLoader.Mods.Length / 10f;
            overMdgs = overMdgs < 0.5f ? 0 : overMdgs;
            float totalBonus = modBonuses.Sum(pair => hasMod(pair.Key) ? pair.Value : 0);
            QFH = baseBonus + overMdgs + totalBonus;
        }
        private static bool hasMod(string name) => ModLoader.Mods.Any(mod => mod.Name == name);
        public override void SetStaticDefaults() {
            Item.ResearchUnlockCount = 64;
            ItemID.Sets.AnimatesAsSoul[Type] = true;
            Main.RegisterItemAnimation(Type, new DrawAnimationVertical(10, 6));
        }

        public override void SetDefaults() {
            Item.width = Item.height = 32;
            Item.maxStack = 99;
            Item.rare = CWRID.Rarity_HotPink;
            Item.value = Item.sellPrice(gold: 99999);
            Item.useAnimation = Item.useTime = 15;
            Item.useStyle = ItemUseStyleID.HoldUp;
        }

        public override bool PreDrawTooltipLine(DrawableTooltipLine line, ref int yOffset) {
            if (line.Name == "ItemName" && line.Mod == "Terraria") {
                InfiniteIngot.DrawColorText(Main.spriteBatch, line);
                return false;
            }
            return true;
        }

        public static int QFD(int num) => (int)(num * QFH);
        public override void AddRecipes() {
            if (!CWRRef.Has) {
                CreateRecipe()
                .AddIngredient(ItemID.FragmentSolar, QFD(5))
                .AddIngredient(ItemID.FragmentVortex, QFD(15))
                .AddIngredient(ItemID.Gel, QFD(50))
                .AddIngredient(ItemID.HellstoneBar, QFD(50))
                .AddIngredient(ItemID.SoulofNight, QFD(50))
                .AddIngredient(ItemID.Obsidian, QFD(50))
                .AddIngredient(ItemID.HallowedBar, QFD(50))
                .AddIngredient(ItemID.LunarBar, QFD(50))
                .AddIngredient(ItemID.LifeCrystal, QFD(50))
                .AddIngredient(ItemID.FallenStar, QFD(50))
                .AddIngredient(ItemID.Ectoplasm, QFD(50))
                .AddIngredient(ItemID.SoulofLight, QFD(50))
                .AddTile(TileType<DarkMatterCompressor>())
                .Register();
                return;
            }
            CreateRecipe()
                .AddIngredient(CWRID.Item_Rock, 1)
                .AddIngredient(CWRID.Item_MiracleFruit, QFD(5))
                .AddIngredient(CWRID.Item_ExoPrism, QFD(5))
                .AddIngredient(CWRID.Item_AshesofAnnihilation, QFD(5))
                .AddIngredient(ItemID.FragmentSolar, QFD(5))
                .AddIngredient(CWRID.Item_DarkPlasma, QFD(10))
                .AddIngredient(CWRID.Item_TwistingNether, QFD(10))
                .AddIngredient(CWRID.Item_ArmoredShell, QFD(10))
                .AddIngredient(ItemID.FragmentVortex, QFD(15))
                .AddIngredient(CWRID.Item_AshesofCalamity, QFD(20))
                .AddIngredient(ItemID.Gel, QFD(50))
                .AddIngredient(ItemID.HellstoneBar, QFD(50))
                .AddIngredient(ItemID.SoulofNight, QFD(50))
                .AddIngredient(CWRID.Item_DivineGeode, QFD(50))
                .AddIngredient(CWRID.Item_DubiousPlating, QFD(50))
                .AddIngredient(ItemID.Obsidian, QFD(50))
                .AddIngredient(ItemID.HallowedBar, QFD(50))
                .AddIngredient(ItemID.LunarBar, QFD(50))
                .AddIngredient(CWRID.Item_LifeAlloy, QFD(50))
                .AddIngredient(ItemID.LifeCrystal, QFD(50))
                .AddIngredient(ItemID.FallenStar, QFD(50))
                .AddIngredient(ItemID.Ectoplasm, QFD(50))
                .AddIngredient(CWRID.Item_Necroplasm, QFD(50))
                .AddIngredient(CWRID.Item_RuinousSoul, QFD(50))
                .AddIngredient(ItemID.SoulofLight, QFD(50))
                .AddTile(TileType<DarkMatterCompressor>())
                .Register();
        }
    }
}