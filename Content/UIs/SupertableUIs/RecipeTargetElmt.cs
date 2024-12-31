using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.UIs.SupertableUIs
{
    internal class RecipeTargetElmt : UIHandle
    {
        public override LayersModeEnum LayersMode => LayersModeEnum.None;
        internal RecipeData recipeData;
        private int borderedWidth;
        private int borderedHeight;
        private int borderedSize;
        private float borderedSizeF;
        private Color backColor = Color.Azure * 0.2f;
        private static RecipeSidebarListViewUI recipeSidebarListView => UIHandleLoader.GetUIHandleOfType<RecipeSidebarListViewUI>();
        public override void Update() {
            UIHitBox = new Rectangle((int)DrawPosition.X, (int)DrawPosition.Y, 64, 64);
            borderedHeight = borderedWidth = 64;
            
            UIHitBox = new Rectangle((int)DrawPosition.X, (int)DrawPosition.Y, borderedWidth, borderedHeight);
            Rectangle mouseRec = new Rectangle((int)MousePosition.X, (int)MousePosition.Y, 1, 1);
            hoverInMainPage = mouseRec.Intersects(UIHitBox) && mouseRec.Intersects(UIHandleLoader.GetUIHandleInstance<RecipeSidebarListViewUI>().UIHitBox);
            float targetSize = 1;
            Color targetColor = Color.Azure * 0.2f;
            if (hoverInMainPage) {
                if (recipeSidebarListView.PreviewTargetPecipePointer != this) {
                    SoundStyle sound = SoundID.Grab;
                    sound.Pitch = -0.6f;
                    sound.Volume = 0.6f;
                    SoundEngine.PlaySound(sound);
                    recipeSidebarListView.PreviewTargetPecipePointer = this;
                }

                if (keyLeftPressState == KeyPressState.Pressed) {
                    if (recipeSidebarListView.TargetPecipePointer != this) {
                        recipeSidebarListView.TargetPecipePointer = recipeSidebarListView.PreviewTargetPecipePointer;
                        SoundStyle sound = SoundID.Grab;
                        sound.Pitch = 0.6f;
                        SoundEngine.PlaySound(sound);

                        for (int i = 0; i < SupertableUI.AllRecipes.Count; i++) {
                            if (recipeData.Target == SupertableUI.AllRecipes[i].Target) {
                                RecipeUI.Instance.index = i;
                            }
                        }
                    }
                }

                Item item = new Item(recipeData.Target);
                if (item != null && item.type > ItemID.None) {
                    Main.HoverItem = item.Clone();
                    Main.hoverItemName = item.Name;
                }

                targetSize = 1.2f;
                targetColor = Color.LightGoldenrodYellow;
            }

            if (recipeSidebarListView.TargetPecipePointer == this) {
                targetSize = 1.2f;
                targetColor = Color.Gold;
            }

            backColor = Color.Lerp(backColor, targetColor, 0.1f);
            borderedSizeF = MathHelper.Lerp(borderedSizeF, targetSize, 0.1f);
            borderedSizeF = MathHelper.Clamp(borderedSizeF, 1, 1.2f);
        }

        public override void Draw(SpriteBatch spriteBatch) {
            VaultUtils.DrawBorderedRectangle(spriteBatch, CWRAsset.UI_JAR.Value, 4, DrawPosition, borderedWidth, borderedHeight
                    , Color.AliceBlue * 0.8f, backColor, borderedSizeF);
            Item item = new Item(recipeData.Target);

            if (item.type > ItemID.None) {
                float drawSize = VaultUtils.GetDrawItemSize(item, borderedWidth) * borderedSizeF;
                Vector2 drawPos = DrawPosition + new Vector2(borderedWidth, borderedHeight) / 2;
                VaultUtils.SimpleDrawItem(spriteBatch, item.type, drawPos, drawSize, 0, Color.White);
            }
        }
    }
}
