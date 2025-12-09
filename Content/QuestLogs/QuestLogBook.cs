using CalamityOverhaul.Common;
using CalamityOverhaul.Content.UIs.OverhaulTheBible;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.QuestLogs
{
    [VaultLoaden(CWRConstant.Item + "Tools")]
    internal class QuestLogBook : ModItem
    {
        public override string Texture => CWRConstant.Item + "Tools/QuestLogBook_Close";
        private static Asset<Texture2D> QuestLogBook_Close = null;
        private static Asset<Texture2D> QuestLogBook_Open1 = null;
        private static Asset<Texture2D> QuestLogBook_Open2 = null;
        public override void SetDefaults() {
            Item.width = 58;
            Item.height = 48;
            Item.value = Item.buyPrice(0, 0, 5, 5);
            Item.rare = ItemRarityID.Orange;
            Item.useTime = Item.useAnimation = 30;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.value = Item.buyPrice(0, 0, 0, 1);
            Item.UseSound = CWRSound.ButtonZero with { Volume = 0.75f };
            ItemID.Sets.ShimmerTransformToItem[Type] = ItemID.Book;
        }

        public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale) {
            if (OverhaulTheBibleUI.Instance.Active) {
                TextureAssets.Item[Type] = QuestLogBook_Open1;
                spriteBatch.Draw(QuestLogBook_Open1.Value, position, null, Color.White, 0, TextureAssets.Item[Type].Value.Size() / 2, scale, SpriteEffects.None, 0);
                Color sengsColor = Color.White * Math.Abs(MathF.Sin(Main.GameUpdateCount * 0.05f));
                spriteBatch.Draw(QuestLogBook_Open2.Value, position, null, sengsColor, 0, TextureAssets.Item[Type].Value.Size() / 2, scale, SpriteEffects.None, 0);
            }
            else {
                TextureAssets.Item[Type] = QuestLogBook_Close;
                spriteBatch.Draw(QuestLogBook_Close.Value, position, null, Color.White, 0, TextureAssets.Item[Type].Value.Size() / 2, scale, SpriteEffects.None, 0);
            }
            return false;
        }

        public override bool? UseItem(Player player) {
            if (player.whoAmI == Main.myPlayer) {
                QuestLog.Instance.visible = !QuestLog.Instance.visible;
            }
            return true;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.Wood, 5)
                .AddIngredient(ItemID.CopperCoin, 5)
                .Register();
        }
    }

    internal class QuestLogBookPlayer : ModPlayer
    {
        private bool Change;
        public override void SaveData(TagCompound tag) {
            tag[nameof(Change)] = Change;
        }
        public override void LoadData(TagCompound tag) {
            Change = false;
            if (tag.TryGet(nameof(Change), out bool change)) {
                Change = change;
            }
        }
        public override void PostUpdateMiscEffects() {
            if (Change) {
                return;
            }
            foreach(var item in Player.inventory) {
                if (!item.Alives() || item.type != CWRID.Item_StarterBag) {
                    continue;
                }
                Change = true;
                item.ChangeItemType(ModContent.ItemType<QuestLogBook>());
            }
        }
    }
}
