using CalamityMod.Items.Materials;
using CalamityMod.Items.PermanentBoosters;
using CalamityMod.Rarities;
using CalamityOverhaul.Content.Tiles;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
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
            Item.value = Item.sellPrice(gold: 99999);
            Item.useAnimation = Item.useTime = 15;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.consumable = true;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.createTile = TileType<InfiniteIngotTile>();
            Item.CWR().isInfiniteItem = true;
            Item.CWR().OmigaSnyContent = SupertableRecipeDate.FullItems8;
        }

        public override bool PreDrawTooltipLine(DrawableTooltipLine line, ref int yOffset) {
            if (line.Name == "ItemName" && line.Mod == "Terraria") {
                DrawColorText(Main.spriteBatch, line);
                return false;
            }

            return true;
        }

        public static void DrawColorText(SpriteBatch sb, DrawableTooltipLine line) {
            Effect effect = CWRUtils.GetEffectValue("Crystal");

            Matrix projection = Matrix.CreateOrthographicOffCenter(0, Main.screenWidth, Main.screenHeight, 0, -1, 1);

            Texture2D noiseTex = CWRUtils.GetT2DValue(CWRConstant.Masking + "SplitTrail");

            effect.Parameters["transformMatrix"].SetValue(projection);
            effect.Parameters["basePos"].SetValue(new Vector2(line.X, line.Y));
            effect.Parameters["scale"].SetValue(new Vector2(1.2f / Main.GameZoomTarget));
            effect.Parameters["uTime"].SetValue((float)Main.timeForVisualEffects * 0.02f);
            effect.Parameters["lightRange"].SetValue(0.15f);
            effect.Parameters["lightLimit"].SetValue(0.45f);
            effect.Parameters["addC"].SetValue(0.75f);
            effect.Parameters["highlightC"].SetValue(Color.White.ToVector4());
            effect.Parameters["brightC"].SetValue(Main.DiscoColor.ToVector4());
            effect.Parameters["darkC"].SetValue(Main.DiscoColor.ToVector4());

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointWrap, DepthStencilState.Default, RasterizerState.CullNone, effect, Main.UIScaleMatrix);

            Main.graphics.GraphicsDevice.Textures[1] = noiseTex;
            ChatManager.DrawColorCodedStringWithShadow(Main.spriteBatch, line.Font, line.Text, new Vector2(line.X, line.Y)
                , Color.White, line.Rotation, line.Origin, line.BaseScale, line.MaxWidth, line.Spread);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointWrap, DepthStencilState.Default, RasterizerState.CullNone, null, Main.UIScaleMatrix);
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
