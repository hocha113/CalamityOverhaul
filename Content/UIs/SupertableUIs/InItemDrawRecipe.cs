using CalamityOverhaul.Content.Items.Placeable;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.UIs.SupertableUIs
{
    public class InItemDrawRecipe : ICWRLoader
    {
        public static InItemDrawRecipe Instance { get; private set; }
        public Vector2 DrawPos;
        public Texture2D mainBookPValue => CWRUtils.GetT2DValue("CalamityOverhaul/Assets/UIs/SupertableUIs/BookPans");
        public Texture2D mainCellValue => CWRUtils.GetT2DValue("CalamityOverhaul/Assets/UIs/SupertableUIs/MainValue3");
        public Texture2D TOMTex => CWRUtils.GetT2DValue(CWRConstant.Asset + "Items/Placeable/" + "TransmutationOfMatterItem");
        public bool DrawBool;
        public bool OnSupTale => SupertableUI.Instance.onMainP || SupertableUI.Instance.onMainP2 || SupertableUI.Instance.onInputP || SupertableUI.Instance.onCloseP;
        void ICWRLoader.LoadData() => Instance = this;
        void ICWRLoader.UnLoadData() => Instance = null;
        /// <summary>
        /// 在只利用一个数字索引的情况下反向计算出对应的格坐标
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public Vector2 ArcCellPos(int index, Vector2 pos) {
            int y = index / 9;
            int x = index - (y * 9);
            return (new Vector2(x, y) * new Vector2(48, 46)) + pos;
        }

        public Vector2 Prevention(Vector2 pos) {
            float maxW = mainBookPValue.Width * 2.2f;
            float maxH = mainBookPValue.Height * 2.5f;
            if (pos.X < 0) {
                pos.X = 0;
            }
            if (pos.X + maxW > Main.screenWidth) {
                pos.X = Main.screenWidth - maxW;
            }
            if (pos.Y < 0) {
                pos.Y = 0;
            }
            if (pos.Y + maxH > Main.screenHeight) {
                pos.Y = Main.screenHeight - maxH;
            }
            return pos;
        }

        public void Draw(SpriteBatch spriteBatch, Vector2 drawPos, string[] names) {
            if (DrawBool) {
                if (!SupertableUI.Instance.Active) {
                    SupertableUI.Instance.onMainP =
                    SupertableUI.Instance.onMainP2 =
                    SupertableUI.Instance.onInputP =
                    SupertableUI.Instance.onCloseP = false;
                }
                DrawPos = Prevention(drawPos);
                Vector2 offset = new Vector2(100, 100);
                Item[] items = new Item[names.Length];
                Item targetItem = SupertableUI.InStrGetItem(names[names.Length - 1], true);
                for (int i = 0; i < names.Length - 1; i++) {
                    string name = names[i];
                    Item item = SupertableUI.InStrGetItem(name, true);
                    items[i] = item;
                }

                Vector2 drawMainUISize = new Vector2(2.2f, 2.6f);
                spriteBatch.Draw(mainBookPValue, DrawPos, null, Color.DarkGoldenrod, 0, Vector2.Zero, drawMainUISize, SpriteEffects.None, 0);//绘制出UI主体
                spriteBatch.Draw(mainCellValue, DrawPos + new Vector2(-25, -25) + offset, null, Color.DarkGoldenrod, 0, Vector2.Zero, 1, SpriteEffects.None, 0);
                Vector2 drawTOMItemIconPos = DrawPos + new Vector2(-50, mainCellValue.Height) + offset;
                VaultUtils.SimpleDrawItem(spriteBatch, ModContent.ItemType<TransmutationOfMatterItem>(), drawTOMItemIconPos, 1, 0, Color.White);

                Vector2 drawText1 = new Vector2(DrawPos.X - 20, DrawPos.Y - 60) + offset;
                Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value,
                    $"{VaultUtils.Translation("在", "In") + CWRUtils.SafeGetItemName<TransmutationOfMatterItem>() + VaultUtils.Translation("进行终焉合成", "Perform final synthesis")}："
                    , drawText1.X, drawText1.Y, Color.White, Color.Black, new Vector2(0.3f), 1f);

                if (targetItem != null) {
                    SupertableUI.DrawItemIcons(spriteBatch, targetItem, DrawPos + new Vector2(450, 520), new Vector2(0.0001f, 0.0001f), Color.White, 1, 1.5f);

                    Vector2 drawText2 = new Vector2(DrawPos.X - 20, DrawPos.Y + 410) + offset;
                    string text = $"{VaultUtils.Translation("合成获得：", "Synthetic acquisition：") + CWRUtils.SafeGetItemName(targetItem.type)}";
                    Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, text, drawText2.X, drawText2.Y, Color.White, Color.Black, new Vector2(0.3f), 1f);
                }

                for (int i = 0; i < items.Length - 1; i++) {//遍历绘制出UI格中的所有预览物品
                    if (items[i] != null) {
                        Item item = items[i];
                        if (item != null) {
                            SupertableUI.DrawItemIcons(spriteBatch, item, ArcCellPos(i, DrawPos + offset), new Vector2(0.0001f, 0.0001f));
                        }
                    }
                }
            }
        }
    }
}
