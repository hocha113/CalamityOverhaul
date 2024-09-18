using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Placeable;
using CalamityOverhaul.Content.UIs.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.UIs.SupertableUIs
{
    internal class RecipeUI : CWRUIPanel
    {
        public static RecipeUI Instance { get; private set; }

        public int index;

        private static List<Item> itemTarget = [];

        private static List<string[]> itemNameString_FormulaContent_Values = [];

        private Rectangle mainRec;

        private Rectangle rAow;

        private Rectangle lAow;

        private bool onM;

        private bool onR;

        private bool onL;

        public override Texture2D Texture => CWRUtils.GetT2DValue("CalamityOverhaul/Assets/UIs/SupertableUIs/RecPBook");

        public override void Initialize() {
            if (SupertableUI.Instance != null) {
                DrawPosition = SupertableUI.Instance.DrawPosition + new Vector2(545, 80);
            }
            mainRec = new Rectangle((int)(DrawPosition.X), (int)(DrawPosition.Y), Texture.Width, Texture.Height);
            rAow = new Rectangle((int)DrawPosition.X + 65, (int)DrawPosition.Y + 20, 25, 25);
            lAow = new Rectangle((int)(DrawPosition.X - 30), (int)(DrawPosition.Y + 20), 25, 25);
            onM = mainRec.Intersects(new Rectangle((int)MousePosition.X, (int)MousePosition.Y, 1, 1));
            onR = rAow.Intersects(new Rectangle((int)MousePosition.X, (int)MousePosition.Y, 1, 1));
            onL = lAow.Intersects(new Rectangle((int)MousePosition.X, (int)MousePosition.Y, 1, 1));
        }

        public override void Load() {
            Instance = this;

            for (int i = 0; i < SupertableUI.AllRecipes.Count; i++) {
                //CWRMod.Instance.Logger.Info($"正在装载配方：{i} --:-- {SupertableUI.AllRecipes.Count}");
                itemTarget.Add(new Item(SupertableUI.AllRecipes[i].Target));
                itemNameString_FormulaContent_Values.Add(SupertableUI.AllRecipes[i].Values);
            }
        }

        public void LoadZenithWRecipes() {
            if (Main.zenithWorld) {
                if (itemTarget.Count < SupertableUI.AllRecipes.Count) {
                    int index = SupertableUI.AllRecipes.Count - 1;
                    itemTarget.Add(new Item(SupertableUI.AllRecipes[index].Target));
                    itemNameString_FormulaContent_Values.Add(SupertableUI.AllRecipes[index].Values);
                }
            }
            else {
                for (int i = 0; i < itemTarget.Count; i++) {
                    if (itemTarget[i].type == ModContent.ItemType<InfiniteToiletItem>()) {
                        itemTarget.RemoveAt(i);
                        itemNameString_FormulaContent_Values.RemoveAt(i);
                    }
                }
            }
        }

        public override void Update(GameTime gameTime) {
            Initialize();
            int museS = DownStartL();
            if (museS == 1) {
                if (onR) {
                    SoundEngine.PlaySound(SoundID.Chat with { Pitch = 0.5f });
                    index += 1;
                }
                if (onL) {
                    SoundEngine.PlaySound(SoundID.Chat with { Pitch = -0.5f });
                    index -= 1;
                }
                if (index < 0) {
                    index = itemTarget.Count - 1;
                }
                if (index > itemTarget.Count - 1) {
                    index = 0;
                }

                LoadPsreviewItems();

                if (SupertableUI.Instance.inputItem == null) { //如果输出物品是Null，进行初始化防止空报错
                    SupertableUI.Instance.inputItem = new Item();
                }
                    
                if (SupertableUI.Instance.inputItem.type != ItemID.None && SupertableUI.Instance.StaticFullItemNames != null) {//如果输出物品不是空物品，进行遍历检测预装填列表
                    for (int i = 0; i < itemNameString_FormulaContent_Values.Count; i++) {
                        string[] formulaContent_Values = itemNameString_FormulaContent_Values[i];
                        for (int j = 0; j < 80; j++) {
                            string fullName = formulaContent_Values[j];
                            if (fullName != SupertableUI.Instance.StaticFullItemNames[j]) {
                                goto End;
                            }
                        }
                        index = i;
                        LoadPsreviewItems();
End:;
                    }
                }
            }
        }

        public void LoadPsreviewItems() {
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
                        SupertableUI.Instance.previewItems[i] = new Item(SupertableUI.InStrGetItemType(value, true));
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
            if (DragButton.Instance != null) {
                if (DragButton.Instance.onDrag) {//为了防止拖拽状态下出现位置更新的延迟所导致的果冻感，这里用于在拖拽状态下进行一次额外的更新
                    Initialize();
                }
            }
            Texture2D arow = CWRUtils.GetT2DValue("CalamityOverhaul/Assets/UIs/SupertableUIs/BlueArrow");
            Texture2D arow2 = CWRUtils.GetT2DValue("CalamityOverhaul/Assets/UIs/SupertableUIs/BlueArrow2");
            spriteBatch.Draw(Texture, DrawPosition, null, Color.White, 0, Vector2.Zero, 1, SpriteEffects.None, 0);//绘制出UI主体
            spriteBatch.Draw(onR ? arow : arow2, DrawPosition + new Vector2(65, 20), null, Color.White, 0, Vector2.Zero, 1, SpriteEffects.None, 0);
            spriteBatch.Draw(onL ? arow : arow2, DrawPosition + new Vector2(-30, 20), null, Color.White, 0, Vector2.Zero, 1, SpriteEffects.FlipHorizontally, 0);
            string text2 = $"{index + 1} -:- {itemTarget.Count}";
            Terraria.Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, text2, DrawPosition.X - text2.Length * 5 + 40, DrawPosition.Y + 65, Color.White, Color.Black, new Vector2(0.3f), 0.8f);
            if (itemTarget != null && SupertableUI.Instance != null && index >= 0 && index < itemTarget.Count) {
                SupertableUI.DrawItemIcons(spriteBatch, itemTarget[index], DrawPosition + new Vector2(5, 5), alp: 0.6f, overSlp: 1.5f);
                string name = itemTarget[index].HoverName;
                string text = $"{CWRLocText.GetTextValue("SupertableUI_Text2")}：{(name == "" ? CWRLocText.GetTextValue("SupertableUI_Text3") : name)}";
                Terraria.Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, text, DrawPosition.X - text.Length * 5, DrawPosition.Y - 25, Color.White, Color.Black, new Vector2(0.3f), 0.8f);
            }
            if (onM) { //处理鼠标在UI格中查看物品的事情
                Item overItem = itemTarget[index];
                if (overItem != null && overItem?.type != ItemID.None) {
                    Main.HoverItem = overItem.Clone();
                    Main.hoverItemName = overItem.Name;
                }
            }
        }
    }
}
