using CalamityMod;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.UIs.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.UIs
{
    internal class CartridgeHolderUI : CWRUIPanel
    {
        public static CartridgeHolderUI Instance;
        public override Texture2D Texture => CWRUtils.GetT2DValue("CalamityOverhaul/Assets/UIs/BulletCard");
        private Item handItem => player.ActiveItem();
        private int bulletNum => player.ActiveItem().CWR().NumberBullets;
        public bool Active {
            get {
                if (handItem.type == ItemID.None) {
                    return false;
                }
                return handItem.CWR().HasCartridgeHolder;
            }
        }

        public override void Load() => Instance = this;

        public override void Initialize() {
            if (handItem.CWR().CartridgeEnum == CartridgeUIEnum.CartridgeHolder)
                DrawPos = new Vector2(20, Main.screenHeight - 100);
            if (handItem.CWR().CartridgeEnum == CartridgeUIEnum.Magazines)
                DrawPos = new Vector2(60, Main.screenHeight - 100);

            DrawPos += new Vector2(ContentConfig.Instance.CartridgeUI_Offset_X_Value, ContentConfig.Instance.CartridgeUI_Offset_Y_Value);
        }

        public override void Draw(SpriteBatch spriteBatch) {
            Initialize();
            if (handItem.CWR().CartridgeEnum == CartridgeUIEnum.CartridgeHolder) {
                spriteBatch.Draw(Texture, DrawPos, null, Color.White, 0f, Vector2.Zero, 1, SpriteEffects.None, 0);//绘制UI主体
                Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.ItemStack.Value, bulletNum.ToString()
                    , DrawPos.X + 50, DrawPos.Y + 0, Color.AliceBlue, Color.Black, Vector2.Zero, 1.3f);
                Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.ItemStack.Value, "Max"
                    , DrawPos.X + 50, DrawPos.Y + 22, Color.Gold, Color.Black, Vector2.Zero, 1f);
                Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.ItemStack.Value, player.ActiveItem().CWR().AmmoCapacity.ToString()
                    , DrawPos.X + 85, DrawPos.Y + 22, Color.Gold, Color.Black, Vector2.Zero, 1.05f);
            }
            if (handItem.CWR().CartridgeEnum == CartridgeUIEnum.Magazines) {
                Texture2D texture2 = CWRUtils.GetT2DValue("CalamityOverhaul/Assets/UIs/Magazines");
                spriteBatch.Draw(texture2, DrawPos, CWRUtils.GetRec(texture2, 6 - bulletNum, 7), Color.White
                    , 0f, CWRUtils.GetOrig(texture2, 7), 2, SpriteEffects.None, 0);
            }
        }
    }
}
