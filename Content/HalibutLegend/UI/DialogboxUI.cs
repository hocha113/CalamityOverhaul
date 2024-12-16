using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.HalibutLegend.UI
{
    internal class DialogboxUI : UIHandle
    {
        public override LayersModeEnum LayersMode => LayersModeEnum.None;
        public Vector2 borderedDrawPos;
        public int borderedWidth;
        public int borderedHeight;
        public float bordered_sengs;
        public float sessionText_sengs;
        private float piscicultureUI_MainBox_sengs => PiscicultureUI._sengs;
        public override void Update() {
            if (piscicultureUI_MainBox_sengs >= 1) {
                if (bordered_sengs < 1) {
                    bordered_sengs += 0.1f;
                }
            }
            else {
                if (bordered_sengs > 0) {
                    bordered_sengs -= 0.1f;
                }
            }

            if (bordered_sengs >= 1) {
                if (sessionText_sengs < 1) {
                    sessionText_sengs += 0.001f;
                }
            }
            else {
                sessionText_sengs = 0;
            }

            borderedWidth = (int)(400 * bordered_sengs);
            borderedHeight = (int)(300 * bordered_sengs);
            borderedDrawPos = new Vector2((Main.screenWidth - borderedWidth) / 2, Main.screenHeight - 300);
        }

        public string GetDialogTextContent() {
            string strContent = "";
            return CWRUtils.GetTextProgressively(strContent, sessionText_sengs);
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (piscicultureUI_MainBox_sengs >= 1) {
                CWRUtils.DrawBorderedRectangle(spriteBatch, CWRAsset.Placeholder_Transparent.Value, 4, borderedDrawPos, borderedWidth, borderedHeight
                    , Color.GreenYellow * 0.8f * piscicultureUI_MainBox_sengs, Color.Azure * 0.2f * piscicultureUI_MainBox_sengs, 1);
            }
            if (bordered_sengs >= 1) {
                string strValue = GetDialogTextContent();
                Vector2 textSize = FontAssets.ItemStack.Value.MeasureString(strValue);
                strValue = CWRUtils.GetSafeText(strValue, textSize, borderedWidth - 20 * 2);
                Utils.DrawBorderStringFourWay(Main.spriteBatch, FontAssets.ItemStack.Value, strValue
                        , borderedDrawPos.X + 20, borderedDrawPos.Y + 20, Color.AliceBlue, Color.Black, Vector2.Zero, 1f);
            }
        }
    }
}
