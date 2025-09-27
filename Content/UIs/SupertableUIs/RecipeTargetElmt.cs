using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.UIs.SupertableUIs
{
    //配方元素
    internal class RecipeTargetElmt : UIHandle
    {
        public override LayersModeEnum LayersMode => LayersModeEnum.None;
        internal RecipeData recipeData;
        private int borderedWidth;
        private int borderedHeight;
        private float borderedSize;
        private Color backColor = Color.Azure * 0.2f;
        private static RecipeSidebarListViewUI recipeSidebarListView => UIHandleLoader.GetUIHandleOfType<RecipeSidebarListViewUI>();
        public override void Update() {
            UIHitBox = new Rectangle((int)DrawPosition.X, (int)DrawPosition.Y, 64, 64);
            borderedHeight = borderedWidth = 64;
            UIHitBox = new Rectangle((int)DrawPosition.X, (int)DrawPosition.Y, borderedWidth, borderedHeight);
            Rectangle mouseRec = new Rectangle((int)MousePosition.X, (int)MousePosition.Y, 1, 1);
            hoverInMainPage = mouseRec.Intersects(UIHitBox) && mouseRec.Intersects(UIHandleLoader.GetUIHandleInstance<RecipeSidebarListViewUI>().UIHitBox);
            float targetSize = 1f;
            Color targetColor = Color.Azure * 0.2f;
            if (hoverInMainPage) {
                player.mouseInterface = true;
                if (recipeSidebarListView.PreviewTargetPecipePointer != this) {
                    SoundStyle sound = SoundID.Grab with { Pitch = -0.6f, Volume = 0.4f };
                    SoundEngine.PlaySound(sound);
                    recipeSidebarListView.PreviewTargetPecipePointer = this;
                }
                if (keyLeftPressState == KeyPressState.Pressed) {
                    if (recipeSidebarListView.TargetPecipePointer != this) {
                        recipeSidebarListView.TargetPecipePointer = this;
                        SoundStyle sound = SoundID.Grab with { Pitch = 0.6f, Volume = 0.8f };
                        SoundEngine.PlaySound(sound);
                        for (int i = 0; i < SupertableUI.AllRecipes.Count; i++) {
                            if (recipeData == SupertableUI.AllRecipes[i]) {
                                RecipeUI.Instance.index = i;
                            }
                        }
                    }
                }
                Item item = new Item(recipeData.Target);
                if (item != null && item.type > ItemID.None) {
                    CWRUI.HoverItem = item;
                    CWRUI.DontSetHoverItem = true;
                }
                targetSize = 1.2f;
                targetColor = Color.LightGoldenrodYellow;
            }
            if (recipeSidebarListView.TargetPecipePointer == this) {
                targetSize = 1.2f;
                targetColor = Color.Gold;
            }
            backColor = Color.Lerp(backColor, targetColor, 0.1f);
            borderedSize = MathHelper.Lerp(borderedSize, targetSize, 0.1f);
            borderedSize = MathHelper.Clamp(borderedSize, 1, 1.2f);
        }
        public override void Draw(SpriteBatch spriteBatch) {
            VaultUtils.DrawBorderedRectangle(spriteBatch, CWRAsset.UI_JAR.Value, 4, DrawPosition, borderedWidth, borderedHeight, Color.AliceBlue * 0.8f * SupertableUI.Instance._sengs, backColor * SupertableUI.Instance._sengs, borderedSize);
            Item item = new Item(recipeData.Target);
            if (item.type > ItemID.None) {
                float drawSize = VaultUtils.GetDrawItemSize(item, borderedWidth) * borderedSize;
                Vector2 drawPos = DrawPosition + new Vector2(borderedWidth, borderedHeight) / 2f;
                VaultUtils.SimpleDrawItem(spriteBatch, item.type, drawPos, drawSize, 0, Color.White * SupertableUI.Instance._sengs);
            }
        }
    }
}
