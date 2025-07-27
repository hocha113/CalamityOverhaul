using CalamityOverhaul.Common;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;

namespace CalamityOverhaul.Content.RangedModify.UI
{
    internal class ReloadingProgressUI : UIHandle
    {
        public override LayersModeEnum LayersMode => LayersModeEnum.Vanilla_Interface_Logic_1;
        public override bool Active => CWRServerConfig.Instance.ShowReloadingProgressUI
            && (player.CWR().PlayerIsKreLoadTime > 0 || overTime > 0);
        [VaultLoaden(CWRConstant.UI + "ReloadingProgress")]
        internal static Asset<Texture2D> Glow { get; private set; }
        [VaultLoaden(CWRConstant.UI + "ReloadingProgressFull")]
        internal static Asset<Texture2D> Full { get; private set; }
        private int overTime;
        public override void Update() {
            DrawPosition = Main.ScreenSize.ToVector2() / 2 - Glow.Size() / 2 + new Vector2(0, 66);
            UIHitBox = DrawPosition.GetRectangle(Glow.Size());
            if (player.CWR().PlayerIsKreLoadTime > 0) {
                overTime = 6;
            }
            else {
                overTime--;
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            spriteBatch.Draw(Glow.Value, UIHitBox, Color.White);

            int sengs = (int)(Full.Height() * player.CWR().ReloadingRatio);
            Rectangle full = new Rectangle(0, sengs, Full.Width(), Full.Height() - sengs);
            spriteBatch.Draw(Full.Value, DrawPosition + new Vector2(2, 2 + sengs), full, Color.White, 0, Vector2.Zero, 1, SpriteEffects.None, 0);
        }
    }
}
