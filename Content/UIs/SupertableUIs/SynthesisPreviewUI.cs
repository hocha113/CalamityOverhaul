using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Placeable;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.UIs.SupertableUIs
{
    public class SynthesisPreviewUI : UIHandle
    {
        public static SynthesisPreviewUI Instance => UIHandleLoader.GetUIHandleOfType<SynthesisPreviewUI>();
        internal Texture2D mainBookPValue => CWRUtils.GetT2DValue("CalamityOverhaul/Assets/UIs/SupertableUIs/BookPans");
        internal Texture2D mainCellValue => CWRUtils.GetT2DValue("CalamityOverhaul/Assets/UIs/SupertableUIs/MainValue3");
        internal Texture2D TOMTex => CWRUtils.GetT2DValue(CWRConstant.Asset + "Items/Placeable/" + "TransmutationOfMatterItem");
        internal string[] OmigaSnyContent = [];
        internal float _sengs;
        internal bool DrawBool;
        internal bool uiIsActive => DrawBool &&!SupertableUI.Instance.hoverInMainPage 
            && CWRLoad.ItemIDToOmigaSnyContent[CWRUI.HoverItem.type] != null;
        public override bool Active => _sengs > 0 || uiIsActive;
        // 在只利用一个数字索引的情况下反向计算出对应的格坐标
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

        public void SetPosition() {
            DrawPosition = new Vector2(580, 100);
            DrawPosition = Prevention(DrawPosition);
            if (SupertableUI.Instance.Active) {
                RecipeSidebarListViewUI recipeSidebarListViewUI = UIHandleLoader.GetUIHandleOfType<RecipeSidebarListViewUI>();
                DrawPosition = recipeSidebarListViewUI.DrawPosition + new Vector2(64, 0);
            }
        }

        public override void Update() {
            if (uiIsActive) {
                if (_sengs < 1f) {
                    _sengs += 0.1f;
                }
            }
            else {
                if (_sengs > 0f) {
                    _sengs -= 0.1f;
                }
            }

            _sengs = MathHelper.Clamp(_sengs, 0, 1);
            SetPosition();
            OmigaSnyContent = SupertableRecipeDate.FullItems;
            if (CWRUI.HoverItem.type > ItemID.None) {
                string[] _omigaSnyContent = CWRLoad.ItemIDToOmigaSnyContent[CWRUI.HoverItem.type];
                if (_omigaSnyContent != null) {
                    OmigaSnyContent = _omigaSnyContent;
                }
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (!SupertableUI.Instance.Active) {
                SupertableUI.Instance.hoverInMainPage =
                SupertableUI.Instance.hoverInPutItemCellPage =
                SupertableUI.Instance.onInputP =
                SupertableUI.Instance.onCloseP = false;
            }
            
            Vector2 offset = new Vector2(100, 100);
            Item[] items = new Item[OmigaSnyContent.Length];
            Item targetItem = SupertableUI.InStrGetItem(OmigaSnyContent[^1], true);
            for (int i = 0; i < OmigaSnyContent.Length - 1; i++) {
                string name = OmigaSnyContent[i];
                Item item = SupertableUI.InStrGetItem(name, true);
                items[i] = item;
            }

            Vector2 drawMainUISize = new Vector2(2.2f, 2.6f);

            VaultUtils.DrawBorderedRectangle(spriteBatch, CWRAsset.UI_JAR.Value, 4, DrawPosition
                , (int)(mainBookPValue.Width * 2.2f * _sengs), (int)(mainBookPValue.Height * 2.5f * _sengs), Color.BlueViolet * 0.8f * _sengs, Color.Azure * 0.2f * _sengs, 1);
            VaultUtils.DrawBorderedRectangle(spriteBatch, CWRAsset.Placeholder_White.Value, 4, DrawPosition
                , (int)(mainBookPValue.Width * 2.2f * _sengs), (int)(mainBookPValue.Height * 2.5f * _sengs), Color.BlueViolet * 0 * _sengs, Color.CadetBlue * 0.6f * _sengs, 1);

            spriteBatch.Draw(mainCellValue, DrawPosition + new Vector2(-25, -25) + offset * _sengs, null, Color.White * 0.8f * _sengs, 0, Vector2.Zero, _sengs, SpriteEffects.None, 0);

            Vector2 drawTOMItemIconPos = DrawPosition + new Vector2(-20 * _sengs, mainCellValue.Height * _sengs + 10) + offset;
            VaultUtils.SimpleDrawItem(spriteBatch, ModContent.ItemType<TransmutationOfMatterItem>(), drawTOMItemIconPos, 1 * _sengs, 0, Color.White * _sengs);

            Vector2 drawText1 = new Vector2(DrawPosition.X - 20 * _sengs, DrawPosition.Y - 60 * _sengs) + offset;
            Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value,
                $"{CWRLocText.GetTextValue("SPU_Text0") + CWRUtils.SafeGetItemName<TransmutationOfMatterItem>() + CWRLocText.GetTextValue("SPU_Text1")}："
                , drawText1.X, drawText1.Y, Color.White * _sengs, Color.Black * _sengs, new Vector2(0.3f), 1f * _sengs);

            if (targetItem != null && targetItem.type > ItemID.None && targetItem.CWR().OmigaSnyContent != null && _sengs >= 1) {
                Vector2 drawText2 = new Vector2(DrawPosition.X + 16 * _sengs, DrawPosition.Y + 420 * _sengs) + offset;
                string text = $"{CWRLocText.GetTextValue("SPU_Text2") + CWRUtils.SafeGetItemName(targetItem.type)}";
                Vector2 size = FontAssets.MouseText.Value.MeasureString(text);
                Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, text, drawText2.X, drawText2.Y
                    , Color.White * _sengs, Color.Black * _sengs, new Vector2(0.3f), 1f * _sengs);
                Vector2 drawItemPos = drawText2 + new Vector2(size.X + 20 * _sengs, 8);
                SupertableUI.DrawItemIcons(spriteBatch, targetItem, drawItemPos, new Vector2(0.0001f, 0.0001f), Color.White * _sengs);

                if (targetItem.type == ModContent.ItemType<InfiniteToiletItem>()) {
                    text = CWRLocText.GetTextValue("OnlyZenith");
                    Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, text, drawText2.X, drawText2.Y + size.Y
                        , Color.Coral * _sengs, Color.Black * _sengs, new Vector2(0.3f), 1f * _sengs);
                }
            }

            if (_sengs >= 1) {
                for (int i = 0; i < items.Length - 1; i++) {//遍历绘制出UI格中的所有预览物品
                    if (items[i] != null) {
                        Item item = items[i];
                        if (item != null) {
                            SupertableUI.DrawItemIcons(spriteBatch, item, ArcCellPos(i, DrawPosition + offset), new Vector2(0.0001f, 0.0001f), Color.White * 0.9f * _sengs);
                        }
                    }
                }
            }
        }
    }
}
