using CalamityMod.Items;
using CalamityMod.Items.Materials;
using CalamityMod.Rarities;
using CalamityOverhaul.Content.Tiles;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using static CalamityOverhaul.CWRMod;
using static Terraria.ModLoader.ModContent;

namespace CalamityOverhaul.Content.Items.Materials
{
    internal class InfinityCatalyst : ModItem
    {
        public override string Texture => CWRConstant.Item + "Materials/InfinityCatalyst";
        public static float QFH {
            get {
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
                    {"仆从暴击", 0.25f},
                };
                float overMdgs = LoadMods.Count / 10f;
                overMdgs = overMdgs < 0.5f ? 0 : overMdgs;
                float totalBonus = modBonuses.Sum(pair => hasMod(pair.Key) ? pair.Value : 0);
                return baseBonus + overMdgs + totalBonus;
            }
        }
        private static bool hasMod(string name) => LoadMods.Any(mod => mod.Name == name);
        public override void SetStaticDefaults() {
            Item.ResearchUnlockCount = 9999;
            ItemID.Sets.AnimatesAsSoul[Type] = true;
            Main.RegisterItemAnimation(Type, new DrawAnimationVertical(10, 6));
        }

        public override void SetDefaults() {
            Item.width = Item.height = 32;
            Item.maxStack = 99;
            Item.rare = RarityType<HotPink>();
            Item.value = Terraria.Item.sellPrice(gold: 99999);
            Item.useAnimation = Item.useTime = 15;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.CWR().isInfiniteItem = true;
        }

        public override bool PreDrawTooltipLine(DrawableTooltipLine line, ref int yOffset) {
            if (line.Name == "ItemName" && line.Mod == "Terraria") {
                InfiniteIngot.DrawColorText(Main.spriteBatch, line);
                return false;
            }
            return true;
        }

        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI) {
            return base.PreDrawInWorld(spriteBatch, lightColor, alphaColor, ref rotation, ref scale, whoAmI);
        }

        public static int QFD(int num) => (int)(num * QFH);
        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<Rock>(1)//古恒石
                .AddIngredient<MiracleMatter>(QFD(5))//奇迹物质
                .AddIngredient<ExoPrism>(QFD(5))//星流棱晶
                .AddIngredient<AuricBar>(QFD(5))//圣金源锭
                .AddIngredient<ShadowspecBar>(QFD(5))//影魔锭
                .AddIngredient<AshesofAnnihilation>(QFD(5))//至尊灾厄精华
                .AddIngredient<YharonSoulFragment>(QFD(5))//龙魂
                .AddIngredient<AerialiteBar>(QFD(5))//水华锭
                .AddIngredient<AstralBar>(QFD(5))//彗星锭
                .AddIngredient<CosmiliteBar>(QFD(5))//宇宙锭
                .AddIngredient<CryonicBar>(QFD(5))//极寒锭
                .AddIngredient<PerennialBar>(QFD(5))//永恒锭
                .AddIngredient<UelibloomBar>(QFD(5))//龙篙锭
                .AddIngredient<ScoriaBar>(QFD(5))//岩浆锭
                .AddIngredient<PestilenceIngot>(QFD(5))//瘟疫锭
                .AddIngredient<DarkPlasma>(QFD(10))//暗物质
                .AddIngredient<TwistingNether>(QFD(10))//扭曲虚空
                .AddIngredient<ArmoredShell>(QFD(10))//装甲心脏
                .AddIngredient<EffulgentFeather>(QFD(15))//闪耀金羽
                .AddIngredient<AshesofCalamity>(QFD(20))//灾厄尘
                .AddIngredient<MurkyPaste>(QFD(50))//泥浆杂草混合物质
                .AddIngredient<MolluskHusk>(QFD(50))//生物质
                .AddIngredient<DepthCells>(QFD(50))//深渊生物组织
                .AddIngredient<DivineGeode>(QFD(50))//圣神晶石
                .AddIngredient<DubiousPlating>(QFD(50))//废弃装甲
                .AddIngredient<BloodstoneCore>(QFD(50))//血石核心
                .AddIngredient<CoreofCalamity>(QFD(50))//灾劫精华
                .AddIngredient<AscendantSpiritEssence>(QFD(50))//化神精魄
                .AddIngredient<LifeAlloy>(QFD(50))//生命合金
                .AddIngredient<LivingShard>(QFD(50))//生命物质
                .AddIngredient<Lumenyl>(QFD(50))//流明晶
                .AddIngredient<MeldConstruct>(QFD(50))//幻塔物质
                .AddIngredient<Necroplasm>(QFD(50))//幻魂
                .AddIngredient<RuinousSoul>(QFD(50))//幽花之魂
                .AddIngredient<UnholyEssence>(QFD(50))//灼火精华
                .AddTile(TileType<DarkMatterCompressor>())
                .Register();
        }

        public static void SpawnAction(Recipe recipe, Item item, List<Item> consumedItems, Item destinationStack) {
            item.CWR().noDestruct = true;
            SoundEngine.PlaySound(new SoundStyle(CWRConstant.Sound + "Pewatermagic"));
        }
    }
}
