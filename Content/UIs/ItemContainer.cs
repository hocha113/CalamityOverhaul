using CalamityOverhaul.Content.UIs.CompressorUIs;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.UIs
{
    internal abstract class ItemContainer : UIHandle
    {
        public override LayersModeEnum LayersMode => LayersModeEnum.None;
        public Item Item { get; set; } = new Item();
        public override void Update() {
            UIHitBox = new Rectangle((int)DrawPosition.X, (int)DrawPosition.Y, ItemConversion.Weith, ItemConversion.Height);
            Rectangle mouseHit = new Rectangle((int)MousePosition.X, (int)MousePosition.Y, 1, 1);
            hoverInMainPage = UIHitBox.Intersects(mouseHit);
            
            if (hoverInMainPage) {
                player.mouseInterface = true;

                if (keyLeftPressState == KeyPressState.Pressed) {
                    Item mouseItem = Main.mouseItem.Clone();
                    Item uiItem = Item.Clone();

                    if (mouseItem.type > ItemID.None || uiItem.type > ItemID.None) {
                        SoundEngine.PlaySound(SoundID.Grab);
                    }
                    if (mouseItem.type != uiItem.type) {
                        Main.mouseItem = uiItem;
                        Item = mouseItem;
                    }
                    else if (mouseItem.type == uiItem.type) {
                        Item.stack += Main.mouseItem.stack;
                        Main.mouseItem.TurnToAir();
                    }
                }
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {

        }
    }
}
