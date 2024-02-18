using CalamityOverhaul;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI.Chat;

namespace CalamityOverhaul.Content.Items.Tools
{
    internal class DarkMatterBall : ModItem
    {
        public override string Texture => CWRConstant.Item + "Tools/DarkMatter";
        public List<int> dorpTypes = new List<int>();
        public List<Item> dorpItems = new List<Item>();
        public override void SetStaticDefaults() {
            Item.ResearchUnlockCount = 3;
        }

        public override void SetDefaults() {
            Item.maxStack = 99;
            Item.consumable = true;
            Item.width = 24;
            Item.height = 24;
            Item.rare = ItemRarityID.Purple;
            Item.CWR().OmigaSnyContent = SupertableRecipeDate.FullItems7;
        }

        public static void DrawItemIcon(SpriteBatch spriteBatch, Vector2 position, int Type, float alp = 1) {
            spriteBatch.Draw(CWRUtils.GetT2DValue(CWRConstant.Item + "Tools/DarkMatter"), position, null, Color.White, 0, TextureAssets.Item[Type].Value.Size() / 2, 1, SpriteEffects.None, 0);
            float sngs = Math.Abs(MathF.Sin(Main.GameUpdateCount * 0.01f));
            spriteBatch.Draw(CWRUtils.GetT2DValue(CWRConstant.Item + "Tools/Full"), position, null, Color.White * sngs * (0.5f + alp * 0.5f), 0, TextureAssets.Item[Type].Value.Size() / 2, 1, SpriteEffects.None, 0);
        }

        public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale) {
            DrawItemIcon(spriteBatch, position, Type);
            return false;
        }

        public override bool CanRightClick() {
            if (dorpItems.Count <= 0) {
                return false;
            }
            return true;
        }

        public void LoadDorp() {
            if (dorpItems == null) {
                dorpItems = new List<Item>();
            }
            if (dorpTypes == null) {
                dorpTypes = new List<int>();
            }
            if (dorpItems.Count < new HashSet<int>(dorpTypes).Count) {
                if (dorpTypes.Count > 0) {
                    dorpTypes.Sort();
                    var groupedDrops = dorpTypes.GroupBy(x => x);
                    foreach (var group in groupedDrops) {
                        List<int> items = group.ToList();
                        int types = items[0];
                        Item dorpItemValue = new Item(types);
                        dorpItemValue.stack = items.Count;
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
            dorpTypes = new List<int>();
            dorpItems = new List<Item>();
        }

        public override void OnStack(Item source, int numToTransfer) {
            DarkMatterBall darkMatterBall = (DarkMatterBall)source.ModItem;
            dorpTypes.AddRange(darkMatterBall.dorpTypes);
            LoadDorp();
        }

        public override bool PreDrawTooltipLine(DrawableTooltipLine line, ref int yOffset) {
            LoadDorp();
            //Main.NewText($"{dorpItems.Count} -- {dorpTypes.Count}");
            if (line.Name == "ItemName" && line.Mod == "Terraria") {
                Color rarityColor = Main.DiscoColor;
                Vector2 basePosition = Main.MouseWorld - Main.screenPosition + new Vector2(23, 23);
                for (int i = 0; i < dorpItems.Count; i++) {
                    Item item = dorpItems[i];
                    string text = item.HoverName;
                    ChatManager.DrawColorCodedString(Main.spriteBatch, line.Font, text, basePosition + new Vector2(0, 22 * i + 66), Color.White, 0f, Vector2.Zero, Vector2.One * 0.9f);
                }
            }
            return true;
        }
    }
}
