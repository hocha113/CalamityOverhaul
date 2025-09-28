using CalamityOverhaul.Common;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.UIs.SupertableUIs
{
    //配方翻页UI
    [VaultLoaden("CalamityOverhaul/Assets/UIs/SupertableUIs/")]
    internal class RecipeUI : UIHandle, ICWRLoader
    {
        private static Texture2D BlueArrow = null;
        private static Texture2D BlueArrow2 = null;
        private static Texture2D RecPBook = null;
        public override Texture2D Texture => RecPBook;
        public override float RenderPriority => 2;
        public override bool Active {
            get {
                if (SupertableUI.Instance == null) {
                    return false;
                }
                return SupertableUI.Instance.Active;
            }
        }
        public static RecipeUI Instance { get; private set; }
        private static List<Item> itemTarget = [];
        private static List<string[]> itemNameString_FormulaContent_Values = [];
        private Rectangle mainRec;
        private Rectangle rAow;
        private Rectangle lAow;
        public int index;
        private bool onM;
        private bool onR;
        private bool onL;
        public override void Load() {
            Instance = this;
        }
        void ICWRLoader.UnLoadData() {
            Instance = null;
        }
        public static void LoadAllRecipes() {
            itemTarget.Clear();
            itemNameString_FormulaContent_Values.Clear();
            for (int i = 0; i < SupertableUI.AllRecipes.Count; i++) {
                itemTarget.Add(new Item(SupertableUI.AllRecipes[i].Target));
                itemNameString_FormulaContent_Values.Add(SupertableUI.AllRecipes[i].Values);
            }
        }
        private void Initialize() {
            if (SupertableUI.Instance != null) {
                DrawPosition = SupertableUI.Instance.DrawPosition + new Vector2(545, 80);
            }
            mainRec = new Rectangle((int)DrawPosition.X, (int)DrawPosition.Y, Texture.Width, Texture.Height);
            rAow = new Rectangle((int)DrawPosition.X + 62, (int)DrawPosition.Y + 20, 25, 25);
            lAow = new Rectangle((int)DrawPosition.X - 30, (int)DrawPosition.Y + 20, 25, 25);
            onM = mainRec.Intersects(new Rectangle((int)MousePosition.X, (int)MousePosition.Y, 1, 1));
            onR = rAow.Intersects(new Rectangle((int)MousePosition.X, (int)MousePosition.Y, 1, 1));
            onL = lAow.Intersects(new Rectangle((int)MousePosition.X, (int)MousePosition.Y, 1, 1));
        }
        public override void Update() {
            Initialize();
            if (onR || onL || onM) {
                DragButton.DontDragTime = 2;
            }
            int museS = (int)keyLeftPressState;
            if (museS == 1) {
                RecipeSidebarListViewUI recipeSidebarListView = UIHandleLoader.GetUIHandleOfType<RecipeSidebarListViewUI>();
                if (onR) {
                    SoundEngine.PlaySound(SoundID.Chat with { Pitch = 0.5f });
                    index += 1;
                    recipeSidebarListView.rollerValue += 64;
                    DragButton.DontDragTime = 2;
                }
                if (onL) {
                    SoundEngine.PlaySound(SoundID.Chat with { Pitch = -0.5f });
                    index -= 1;
                    recipeSidebarListView.rollerValue -= 64;
                    DragButton.DontDragTime = 2;
                }
                if (onM) {
                    DragButton.DontDragTime = 2;
                }
                if (index < 0) {
                    index = itemTarget.Count - 1;
                    recipeSidebarListView.rollerValue = recipeSidebarListView.recipeTargetElmts.Count * 64;
                }
                if (index > itemTarget.Count - 1) {
                    index = 0;
                    recipeSidebarListView.rollerValue = 0;
                }
                RecipeTargetElmt elmt = null;
                foreach (RecipeTargetElmt folwerElmt in recipeSidebarListView.recipeTargetElmts) {
                    if (folwerElmt.recipeData == SupertableUI.AllRecipes[index]) {
                        elmt = folwerElmt;
                    }
                }
                if (elmt != null) {
                    recipeSidebarListView.TargetPecipePointer = elmt;
                }
                LoadPreviewItems();
                if (SupertableUI.Instance.inputItem == null) {
                    SupertableUI.Instance.inputItem = new Item();
                }
                if (SupertableUI.Instance.inputItem.type != ItemID.None && SupertableUI.Instance.StaticFullItemNames != null) {
                    for (int i = 0; i < itemNameString_FormulaContent_Values.Count; i++) {
                        string[] formulaContent_Values = itemNameString_FormulaContent_Values[i];
                        bool match = true;
                        for (int j = 0; j < 80; j++) {
                            if (formulaContent_Values[j] != SupertableUI.Instance.StaticFullItemNames[j]) {
                                match = false;
                                break;
                            }
                        }
                        if (match) {
                            index = i;
                            LoadPreviewItems();
                            break;
                        }
                    }
                }
            }
        }
        public void LoadPreviewItems() {
            if (SupertableUI.Instance != null) {
                if (SupertableUI.Instance.previewItems == null) {
                    SupertableUI.Instance.previewItems = new Item[81];
                }
                if (SupertableUI.Instance.items == null) {
                    SupertableUI.Instance.items = new Item[81];
                }
                SupertableUI.Instance.previewItems = new Item[SupertableUI.Instance.items.Length];
                string[] names = itemNameString_FormulaContent_Values[index];
                if (names != null) {
                    for (int i = 0; i < 81; i++) {
                        string value = names[i];
                        SupertableUI.Instance.previewItems[i] = new Item(VaultUtils.GetItemTypeFromFullName(value, true));
                    }
                }
                else {
                    for (int i = 0; i < 81; i++) {
                        SupertableUI.Instance.previewItems[i] = new Item();
                    }
                }
            }
        }
        public override void Draw(SpriteBatch spriteBatch) {
            Texture2D arow = BlueArrow;
            Texture2D arow2 = BlueArrow2;
            spriteBatch.Draw(Texture, DrawPosition, null, Color.White * SupertableUI.Instance._sengs, 0, Vector2.Zero, 1, SpriteEffects.None, 0);
            spriteBatch.Draw(onR ? arow : arow2, DrawPosition + new Vector2(62, 20), null, Color.White * SupertableUI.Instance._sengs, 0, Vector2.Zero, 1, SpriteEffects.None, 0);
            spriteBatch.Draw(onL ? arow : arow2, DrawPosition + new Vector2(-30, 20), null, Color.White * SupertableUI.Instance._sengs, 0, Vector2.Zero, 1, SpriteEffects.FlipHorizontally, 0);
            string text2 = $"{index + 1} -:- {itemTarget.Count}";
            Vector2 text2Size = FontAssets.MouseText.Value.MeasureString(text2);
            Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, text2, DrawPosition.X - text2Size.X / 2 + Texture.Width / 2, DrawPosition.Y + 65, Color.White * SupertableUI.Instance._sengs, Color.Black * SupertableUI.Instance._sengs, new Vector2(0.3f), 0.8f);
            if (itemTarget != null && SupertableUI.Instance != null && index >= 0 && index < itemTarget.Count) {
                SupertableUI.DrawItemIcons(spriteBatch, itemTarget[index], DrawPosition + new Vector2(5, 5), alp: 0.6f * SupertableUI.Instance._sengs, overSlp: 1.5f * SupertableUI.Instance._sengs);
                string name = itemTarget[index].HoverName;
                string text = $"{CWRLocText.GetTextValue("SupertableUI_Text2")}：{(name == "" ? CWRLocText.GetTextValue("SupertableUI_Text3") : name)}";
                Vector2 textSize = FontAssets.MouseText.Value.MeasureString(text);
                Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, text, DrawPosition.X - textSize.X / 2 + Texture.Width / 2, DrawPosition.Y - 25, Color.White * SupertableUI.Instance._sengs, Color.Black * SupertableUI.Instance._sengs, new Vector2(0.3f), 0.8f);
            }
            if (onM) {
                Item overItem = itemTarget[index];
                if (overItem != null && overItem.type != ItemID.None) {
                    Main.HoverItem = overItem.Clone();
                    Main.hoverItemName = overItem.Name;
                }
            }
        }
    }
}
