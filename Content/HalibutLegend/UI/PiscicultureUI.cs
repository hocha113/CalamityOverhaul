using CalamityMod.Items.Weapons.Ranged;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.HalibutLegend.UI
{
    internal class PiscicultureUI : UIHandle
    {
        public static PiscicultureUI Instance => (PiscicultureUI)UIHandleLoader.GetUIHandleInstance<PiscicultureUI>();
        public static DialogboxUI Dialogbox => (DialogboxUI)UIHandleLoader.GetUIHandleInstance<DialogboxUI>();
        public override bool Active => IsOnpen || _sengs > 0;
        public static bool IsOnpen;
        public static float _sengs;
        public Vector2 halibutBodyDrawPos;

        public override void Update() {
            if (!IsOnpen && _sengs > 0) {
                _sengs -= 0.1f;
            }
            if (IsOnpen && _sengs < 1) {
                _sengs += 0.1f;
            }

            halibutBodyDrawPos = new Vector2(Main.screenWidth / 2, Main.screenHeight - 400 * _sengs);
            //对话框的逻辑更新需要在主逻辑之后，以避免更新延迟现象
            Dialogbox.Update();
        }

        public override void Draw(SpriteBatch spriteBatch) {
            //绘制一个泛泛的背景
            Texture2D halibutValue = TextureAssets.Item[ModContent.ItemType<HalibutCannon>()].Value;
            spriteBatch.Draw(CWRAsset.Placeholder_Transparent.Value, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), Color.Blue * _sengs * 0.2f);
            spriteBatch.Draw(halibutValue, halibutBodyDrawPos, null, Color.White * _sengs, 0, halibutValue.Size() / 2, 2, SpriteEffects.None, 0);
            //调用对话框的绘制
            Dialogbox.Draw(spriteBatch);
        }
    }
}
