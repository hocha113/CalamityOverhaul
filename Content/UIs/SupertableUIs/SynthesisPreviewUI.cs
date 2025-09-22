using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Pipelines;
using CalamityOverhaul.Content.Items.Placeable;
using CalamityOverhaul.Content.Items.Tools;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.UIs.SupertableUIs
{
    public class SynthesisPreviewUI : UIHandle, ICWRLoader
    {
        public static SynthesisPreviewUI Instance => UIHandleLoader.GetUIHandleOfType<SynthesisPreviewUI>();
        internal static int Width => 564;
        internal static int Height => 564;
        [VaultLoaden(CWRConstant.UI + "SupertableUIs/MainValueInCell")]
        internal static Asset<Texture2D> MainValueInCell = null;
        internal string[] OmigaSnyContent = [];
        internal float _sengs;
        internal bool humerdFrame;
        internal bool DrawBool;
        internal bool uiIsActive => DrawBool && !SupertableUI.Instance.hoverInMainPage && CWRLoad.ItemIDToOmigaSnyContent[CWRUI.HoverItem.type] != null;
        public override bool Active => _sengs > 0 || uiIsActive;
        void ICWRLoader.UnLoadData() => OmigaSnyContent = [];
        // 在只利用一个数字索引的情况下反向计算出对应的格坐标
        public Vector2 ArcCellPos(int index, Vector2 pos) {
            int y = index / 9;
            int x = index - (y * 9);
            return (new Vector2(x, y) * new Vector2(48, 46)) + pos;
        }

        public Vector2 Prevention(Vector2 pos) {
            float maxW = Width;
            float maxH = Height;
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

        private void SetOmigaSnyContent(string[] omigaSnyContent) {
            if (omigaSnyContent == null) {
                return;
            }

            OmigaSnyContent = omigaSnyContent;

            if (Main.GameUpdateCount % 60 == 0) {
                humerdFrame = !humerdFrame;
            }

            if (!humerdFrame) {
                return;
            }

            if (CWRUI.HoverItem.type == ModContent.ItemType<DarkMatterBall>()) {
                OmigaSnyContent = SupertableRecipeDate.FullItems_DarkMatterBall2;
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
            OmigaSnyContent = SupertableRecipeDate.FullItems_Null;

            if (CWRUI.HoverItem.type > ItemID.None) {
                SetOmigaSnyContent(CWRLoad.ItemIDToOmigaSnyContent[CWRUI.HoverItem.type]);
            }

            RecipeSidebarListViewUI recipeSidebarListView = UIHandleLoader.GetUIHandleOfType<RecipeSidebarListViewUI>();
            if (recipeSidebarListView.hoverInMainPage && recipeSidebarListView.PreviewTargetPecipePointer != null) {
                OmigaSnyContent = recipeSidebarListView.PreviewTargetPecipePointer.recipeData.Values;
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (!SupertableUI.Instance.Active) {
                SupertableUI.Instance.hoverInMainPage =
                SupertableUI.Instance.hoverInPutItemCellPage =
                SupertableUI.Instance.onInputP =
                SupertableUI.Instance.onCloseP = false;
            }

            Vector2 offset = new Vector2(90, 100);
            Item[] items = new Item[OmigaSnyContent.Length];
            Item targetItem = SupertableUI.InStrGetItem(OmigaSnyContent[^1], true);
            for (int i = 0; i < OmigaSnyContent.Length - 1; i++) {
                string name = OmigaSnyContent[i];
                Item item = SupertableUI.InStrGetItem(name, true);
                items[i] = item;
            }

            Vector2 drawMainUISize = new Vector2(2.2f, 2.6f);

            VaultUtils.DrawBorderedRectangle(spriteBatch, CWRAsset.UI_JAR.Value, 4, DrawPosition
                , (int)(Width * _sengs), (int)(Height * _sengs), Color.BlueViolet * 0.8f * _sengs, Color.Azure * 0.2f * _sengs, 1);
            VaultUtils.DrawBorderedRectangle(spriteBatch, CWRAsset.Placeholder_White.Value, 4, DrawPosition
                , (int)(Width * _sengs), (int)(Height * _sengs), Color.BlueViolet * 0 * _sengs, Color.CadetBlue * 0.6f * _sengs, 1);

            spriteBatch.Draw(MainValueInCell.Value, DrawPosition + new Vector2(-25, -25) + offset * _sengs, null, Color.White * 0.8f * _sengs, 0, Vector2.Zero, _sengs, SpriteEffects.None, 0);

            Vector2 drawTOMItemIconPos = DrawPosition + new Vector2(-20 * _sengs, MainValueInCell.Value.Height * _sengs + 10) + offset;
            VaultUtils.SimpleDrawItem(spriteBatch, ModContent.ItemType<TransmutationOfMatterItem>(), drawTOMItemIconPos, 1 * _sengs, 0, Color.White * _sengs);

            Vector2 drawText1 = new Vector2(DrawPosition.X - 20 * _sengs, DrawPosition.Y - 60 * _sengs) + offset;
            Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value,
                $"{CWRLocText.GetTextValue("SPU_Text0") + VaultUtils.GetLocalizedItemName<TransmutationOfMatterItem>() + CWRLocText.GetTextValue("SPU_Text1")}："
                , drawText1.X, drawText1.Y, Color.White * _sengs, Color.Black * _sengs, new Vector2(0.3f), 1f * _sengs);

            if (targetItem != null && targetItem.type > ItemID.None && targetItem.CWR().OmigaSnyContent != null && _sengs >= 1) {
                Vector2 drawText2 = new Vector2(DrawPosition.X + 16 * _sengs, DrawPosition.Y + 420 * _sengs) + offset;
                string text = $"{CWRLocText.GetTextValue("SPU_Text2") + VaultUtils.GetLocalizedItemName(targetItem.type)}";
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

            if (_sengs < 1) {
                return;
            }

            for (int i = 0; i < items.Length - 1; i++) {//遍历绘制出UI格中的所有预览物品
                if (items[i] == null) {
                    continue;
                }

                Item item = items[i];

                if (item == null) {
                    continue;
                }

                SupertableUI.DrawItemIcons(spriteBatch, item, ArcCellPos(i, DrawPosition + offset), new Vector2(0.0001f, 0.0001f), Color.White * 0.9f * _sengs);
            }
        }
    }
}
