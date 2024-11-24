using CalamityMod.Items.LoreItems;
using CalamityMod.Items.Materials;
using CalamityOverhaul.Content.Tiles;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI.Chat;

namespace CalamityOverhaul.Content.Items.Tools
{
    internal class DarkMatterBall : ModItem, ICWRLoader
    {
        public override string Texture => CWRConstant.Item + "Tools/DarkMatter";
        internal static Asset<Texture2D> DarkMatter;
        internal static Asset<Texture2D> Full;
        public List<int> dorpTypes = [];
        public List<Item> dorpItems = [];
        public override ModItem Clone(Item newEntity) {
            DarkMatterBall ball = (DarkMatterBall)base.Clone(newEntity);
            ball.dorpTypes = new List<int>(dorpTypes);
            ball.dorpItems = new List<Item>(dorpItems);
            return ball;
        }
        void ICWRLoader.LoadAsset() {
            DarkMatter = CWRUtils.GetT2DAsset(CWRConstant.Item + "Tools/DarkMatter");
            Full = CWRUtils.GetT2DAsset(CWRConstant.Item + "Tools/Full");
        }
        void ICWRLoader.UnLoadData() {
            DarkMatter = null;
            Full = null;
        }
        public override void SetStaticDefaults() => Item.ResearchUnlockCount = 9999;
        public override void SetDefaults() {
            Item.maxStack = 99;
            Item.consumable = true;
            Item.width = 24;
            Item.height = 24;
            Item.rare = ItemRarityID.Purple;
            Item.CWR().OmigaSnyContent = SupertableRecipeDate.FullItems7;
        }

        public static void DrawItemIcon(SpriteBatch spriteBatch, Vector2 position, int Type, float alp = 1) {
            spriteBatch.Draw(DarkMatter.Value, position, null, Color.White * alp, 0, DarkMatter.Value.Size() / 2, 1, SpriteEffects.None, 0);
            float sngs = Math.Abs(MathF.Sin(Main.GameUpdateCount * 0.01f));
            spriteBatch.Draw(Full.Value, position, null, Color.White * sngs * (0.5f + alp * 0.5f), 0, Full.Value.Size() / 2, 1, SpriteEffects.None, 0);
        }

        public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale) {
            DrawItemIcon(spriteBatch, position, Type);
            return false;
        }

        public override bool CanRightClick() {
            return dorpItems.Count > 0;
        }

        public void LoadDorp() {
            if (dorpItems == null) {
                dorpItems = [];
            }
            if (dorpTypes == null) {
                dorpTypes = [];
            }
            if (dorpItems.Count < new HashSet<int>(dorpTypes).Count) {
                if (dorpTypes.Count > 0) {
                    dorpTypes.Sort();
                    var groupedDrops = dorpTypes.GroupBy(x => x);
                    foreach (var group in groupedDrops) {
                        List<int> items = group.ToList();
                        int types = items[0];
                        Item dorpItemValue = new Item(types) {
                            stack = items.Count
                        };
                        if (dorpItemValue != null)
                            dorpItems.Add(dorpItemValue);
                    }
                }
            }
        }

        public override void RightClick(Player player) {
            LoadDorp();
            foreach (var item in dorpItems) {
                player.QuickSpawnItem(player.parent(), item, item.stack);
            }
            player.QuickSpawnItem(player.parent(), new Item(Type));
            dorpTypes = [];
            dorpItems = [];
        }

        public override void OnStack(Item source, int numToTransfer) {
            DarkMatterBall darkMatterBall = (DarkMatterBall)source.ModItem;
            dorpTypes.AddRange(darkMatterBall.dorpTypes);
            LoadDorp();
        }

        public override bool PreDrawTooltipLine(DrawableTooltipLine line, ref int yOffset) {
            LoadDorp();
            if (line.Name == "ItemName" && line.Mod == "Terraria") {
                Vector2 basePosition = new Vector2(line.X, line.Y) + new Vector2(0, 80);
                for (int i = 0; i < dorpItems.Count; i++) {
                    Item item = dorpItems[i];
                    string text = item.HoverName;
                    ChatManager.DrawColorCodedString(Main.spriteBatch, line.Font, text, basePosition + new Vector2(0, 22 * i + 66), Color.White, 0f, Vector2.Zero, Vector2.One * 0.9f);
                }
            }
            return true;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<ShadowspecBar>(24)
                .AddIngredient<AuricBar>(20)
                .AddIngredient<MiracleMatter>(16)
                .AddIngredient<YharonSoulFragment>(8)
                .AddIngredient<LoreCynosure>()
                .AddBlockingSynthesisEvent()
                .AddTile(ModContent.TileType<TransmutationOfMatter>())
                .Register();
        }
    }
}
