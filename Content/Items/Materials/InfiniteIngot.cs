using CalamityMod.Items.Materials;
using CalamityMod.Items.PermanentBoosters;
using CalamityMod.Rarities;
using CalamityOverhaul.Content.Items.Ranged.Extras;
using CalamityOverhaul.Content.Tiles;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI.Chat;
using static Terraria.ModLoader.ModContent;

namespace CalamityOverhaul.Content.Items.Materials
{
    internal class InfiniteIngot : ModItem
    {
        public override string Texture => CWRConstant.Item + "Materials/InfiniteIngot";
        //public float QFH {
        //    get {
        //        const float baseBonus = 1.0f;
        //        var modBonuses = new Dictionary<string, float>{
        //            {"LightAndDarknessMod", 0.1f},
        //            {"DDmod", 0.1f},
        //            {"MaxStackExtra", 0.1f},
        //            {"Wild", 0.1f},
        //            {"Coralite", 0.1f},
        //            {"AncientsAwakened", 0.1f},
        //            {"NoxusBoss", 0.25f},
        //            {"FargowiltasSouls", 0.25f},
        //            {"MagicBuilder", 0.25f},
        //            {"CalamityPostMLBoots", 0.25f},
        //            {"仆从暴击", 0.25f}
        //        };
        //        float overMdgs = Instance.LoadMods.Count / 10f;
        //        overMdgs = overMdgs < 0.5f ? 0 : overMdgs;
        //        float totalBonus = modBonuses.Sum(pair => hasMod(pair.Key) ? pair.Value : 0);
        //        return baseBonus + overMdgs + totalBonus;
        //    }
        //}
        //private bool hasMod(string name) {
        //    return Instance.LoadMods.Any(mod => mod.Name == name);
        //}

        public override void SetStaticDefaults() {
            Item.ResearchUnlockCount = 9999;
            ItemID.Sets.SortingPriorityMaterials[Type] = 114;
            ItemID.Sets.AnimatesAsSoul[Type] = true;
            Main.RegisterItemAnimation(Type, new DrawAnimationVertical(6, 11));
        }

        public override void SetDefaults() {
            Item.width = Item.height = 25;
            Item.maxStack = 99;
            Item.rare = RarityType<HotPink>();
            Item.value = Terraria.Item.sellPrice(gold: 99999);
            Item.useAnimation = Item.useTime = 15;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.consumable = true;
            Item.createTile = TileType<InfiniteIngotTile>();
            Item.CWR().isInfiniteItem = true;
            Item.CWR().OmigaSnyContent = SupertableRecipeDate.FullItems8;
        }

        public override bool PreDrawTooltipLine(DrawableTooltipLine line, ref int yOffset) {
            if (line.Name == "ItemName" && line.Mod == "Terraria") {
                Vector2 basePosition = new Vector2(line.X, line.Y);
                string text = Language.GetTextValue("Mods.CalamityOverhaul.Items.InfiniteIngot.DisplayName");
                drawColorText(Main.spriteBatch, line, text, basePosition);
                return false;
            }
            return true;
        }

        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI) {
            return base.PreDrawInWorld(spriteBatch, lightColor, alphaColor, ref rotation, ref scale, whoAmI);
        }

        public static void drawColorText(SpriteBatch sb, DrawableTooltipLine line, string text, Vector2 basePosition) {
            ChatManager.DrawColorCodedStringWithShadow(sb, line.Font, line.Text, basePosition
                , VaultUtils.MultiStepColorLerp(Main.GameUpdateCount % 120 / 120f, HeavenfallLongbow.rainbowColors)
                , line.Rotation, line.Origin, line.BaseScale * 1.05f, line.MaxWidth, line.Spread);
            /*
            //ChatManager.DrawColorCodedString(sb, line.Font, line.Text, basePosition, 
            //    CWRUtils.MultiLerpColor(Main.GameUpdateCount  % 90 / 90f, HeavenfallLongbow.rainbowColors), 0f, Vector2.Zero, new Vector2(1.1f, 1.1f));
            //EffectsRegistry.ColourModulationShader.Parameters["uTime"].SetValue(Main.GlobalTimeWrappedHourly * 0.25f);
            //Main.instance.GraphicsDevice.Textures[1] = EffectsRegistry.Ticoninfinity;
            //ChatManager.DrawColorCodedString(sb, line.Font, line.Text, basePosition, Color.White, 0f, Vector2.Zero, new Vector2(1.1f, 1.1f));
            //sb.End();
            //sb.Begin(SpriteSortMode.Immediate, sb.GraphicsDevice.BlendState, sb.GraphicsDevice.SamplerStates[0],
            //    sb.GraphicsDevice.DepthStencilState, sb.GraphicsDevice.RasterizerState, EffectsRegistry.ColourModulationShader, Main.UIScaleMatrix);
            //ChatManager.DrawColorCodedString(sb, line.Font, line.Text, basePosition, Color.White, 0f, Vector2.Zero, new Vector2(1.1f, 1.1f));
            //ChatManager.DrawColorCodedStringWithShadow(sb, line.Font, line.Text, basePosition
            //    , CWRUtils.MultiLerpColor(Main.GameUpdateCount % 120 / 120f, HeavenfallLongbow.rainbowColors)
            //    , line.Rotation, line.Origin, line.BaseScale * 1.05f, line.MaxWidth, line.Spread);
            //sb.End();
            //sb.Begin(SpriteSortMode.Deferred, sb.GraphicsDevice.BlendState, sb.GraphicsDevice.SamplerStates[0],
            //    sb.GraphicsDevice.DepthStencilState, sb.GraphicsDevice.RasterizerState, null, Main.UIScaleMatrix);
            */
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<InfinityCatalyst>(1)
                .AddIngredient<MiracleFruit>(1)
                .AddIngredient<Elderberry>(1)
                .AddIngredient<BloodOrange>(1)
                .AddIngredient<Dragonfruit>(1)
                .AddIngredient<MiracleMatter>(6)
                .AddIngredient<ShadowspecBar>(9)
                .AddIngredient<BlackMatterStick>(11)
                .AddBlockingSynthesisEvent()
                .AddTile(TileType<TransmutationOfMatter>())
                .Register();
        }
    }
}
