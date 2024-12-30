using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.UIs.CompressorUIs
{
    internal class ArrowLock : UIHandle
    {
        public override LayersModeEnum LayersMode => LayersModeEnum.None;
        internal bool hoverInMainUI;
        internal bool IsLock;
        public override void Update() {
            UIHitBox = new Rectangle((int)DrawPosition.X, (int)DrawPosition.Y, ItemConversion.Weith, ItemConversion.Height);
            Rectangle mouseHit = new Rectangle((int)MousePosition.X, (int)MousePosition.Y, 1, 1);
            hoverInMainUI = UIHitBox.Intersects(mouseHit);
            if (hoverInMainUI) {
                player.mouseInterface = true;

                if (keyLeftPressState == KeyPressState.Pressed) {
                    IsLock = !IsLock;
                    SoundEngine.PlaySound(SoundID.Unlock);
                }
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            Texture2D arrow = CWRUtils.GetT2DValue("CalamityOverhaul/Assets/UIs/SupertableUIs/InputArrow2");
            spriteBatch.Draw(arrow, DrawPosition + new Vector2(32, 0), null, Color.White, MathHelper.PiOver2, new Vector2(0, arrow.Size().Y / 2), 1, SpriteEffects.None, 0);
            VaultUtils.DrawBorderedRectangle(spriteBatch, CWRAsset.UI_JAR.Value, 4, DrawPosition, ItemConversion.Weith, ItemConversion.Height
                    , Color.AliceBlue, Color.Azure * 0.8f, 1);
            if (IsLock) {
                Texture2D lockValue = CWRUtils.GetT2DValue("CalamityMod/UI/ModeIndicator/ModeIndicatorLock");
                spriteBatch.Draw(lockValue, DrawPosition + new Vector2(ItemConversion.Weith, ItemConversion.Height) / 2
                    , null, Color.White, 0, lockValue.Size() / 2, 2, SpriteEffects.None, 0);
            }
        }
    }
}
