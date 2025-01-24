using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI
{
    internal class ItemDisplayUI : UIHandle
    {
        public override bool Active => PiscicultureUI.Instance.Active || bordered_sengs > 0;
        public override float RenderPriority => 2;
        public static ItemDisplayUI Instance => UIHandleLoader.GetUIHandleOfType<ItemDisplayUI>();
        public int borderedWidth;
        public int borderedHeight;
        public float bordered_sengs;
        public float borderedSize;
        public Item Item = new Item();
        public override void Update() {
            if (Item == null) {
                Item = new Item();
            }

            DrawPosition = new Vector2((Main.screenWidth - borderedWidth) / 2, Main.screenHeight / 2 - 200);
            UIHitBox = new Rectangle((int)DrawPosition.X, (int)DrawPosition.Y, borderedWidth, borderedHeight);
            Rectangle mouseRec = new Rectangle((int)MousePosition.X, (int)MousePosition.Y, 1, 1);
            hoverInMainPage = mouseRec.Intersects(UIHitBox);
            if (PiscicultureUI.Instance.Active) {
                if (bordered_sengs < 0) {
                    bordered_sengs += 0.1f;
                }
                if (borderedWidth < 160) {
                    borderedWidth += 1;
                }
                if (borderedHeight < 160) {
                    borderedHeight += 1;
                }
            }
            else {
                if (bordered_sengs > 0) {
                    bordered_sengs -= 0.1f;
                }
                if (borderedWidth > 0) {
                    borderedWidth -= 1;
                }
                if (borderedHeight > 0) {
                    borderedHeight -= 1;
                }
            }

            float targetData = 1;
            if (hoverInMainPage) {
                targetData = 1.1f;
                player.mouseInterface = true;

                if (keyLeftPressState == KeyPressState.Pressed) {
                    Item mouseItem = Main.mouseItem.Clone();
                    Item uiItem = Item.Clone();
                    if (mouseItem.type > ItemID.None || uiItem.type > ItemID.None) {
                        SoundEngine.PlaySound(SoundID.Grab);
                    }
                    Main.mouseItem = uiItem;
                    Item = mouseItem;
                    PiscicultureUI.Dialogbox.sessionText_sengs = 0;
                }
            }

            borderedSize = MathHelper.Lerp(borderedSize, targetData, 0.2f);
            borderedSize = MathHelper.Clamp(borderedSize, 1, 1.1f);
        }

        public override void Draw(SpriteBatch spriteBatch) {
            VaultUtils.DrawBorderedRectangle(spriteBatch, CWRAsset.UI_JAR.Value, 4, DrawPosition, borderedWidth, borderedHeight
                    , Color.AliceBlue * 0.8f * PiscicultureUI._sengs, Color.Azure * 0.2f * PiscicultureUI._sengs, borderedSize);
            if (Item.type > ItemID.None) {
                float drawSize = Item.GetDrawItemSize(borderedWidth) * borderedSize;
                Vector2 drawPos = DrawPosition + new Vector2(borderedWidth, borderedHeight) / 2;
                VaultUtils.SimpleDrawItem(spriteBatch, Item.type, drawPos, drawSize, 0, Color.White);
            }
        }
    }
}
