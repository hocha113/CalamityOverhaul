using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Content.UIs.OverhaulTheBible
{
    internal class SliderUI : UIHandle
    {
        public override LayersModeEnum LayersMode => LayersModeEnum.None;
        public static int Width => 18;
        public static int Height => 18;
        public OverhaulTheBibleUI mainUI => UIHandleLoader.GetUIHandleOfType<OverhaulTheBibleUI>();
        public bool onDrag;
        public override void Update() {
            DrawPosition = mainUI.DrawPosition + new Vector2(mainUI.boxWeith - 4, mainUI.rollerSengs - 4) * mainUI._sengs;
            UIHitBox = new Rectangle((int)DrawPosition.X, (int)DrawPosition.Y, Width, Height);
            Rectangle mouseHit = new Rectangle((int)MousePosition.X, (int)MousePosition.Y, 1, 1);
            if (UIHitBox.Intersects(mouseHit)) {
                if (keyLeftPressState == KeyPressState.Pressed && !onDrag) {
                    onDrag = true;
                }
            }

            if (onDrag) {
                float mouseY = MousePosition.Y - Height / 4;
                float sengs = mouseY - mainUI.DrawPosition.Y;
                sengs = MathHelper.Clamp(sengs, 1, mainUI.boxHeight) / mainUI.boxHeight;

                mainUI.rollerValue = sengs * mainUI.elementsPerColumn * ItemVidous.Height;
                if (keyLeftPressState == KeyPressState.Released) {
                    onDrag = false;
                }
                mainUI.rollerValue = MathHelper.Clamp(mainUI.rollerValue, 0, mainUI.elementsPerColumn * ItemVidous.Height);
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            VaultUtils.DrawBorderedRectangle(spriteBatch, CWRAsset.UI_JAR.Value, 4, DrawPosition, Width, Height, Color.Blue, Color.White, 1);
        }
    }
}
