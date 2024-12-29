using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.HalibutLegend.UI
{
    internal class DialogboxUI : UIHandle
    {
        public override LayersModeEnum LayersMode => LayersModeEnum.None;
        public Rectangle HitBox;
        public Vector2 borderedDrawPos;
        public int borderedWidth;
        public int borderedHeight;
        public float bordered_sengs;
        public float sessionText_sengs;
        public int GreetingIndex;
        public string DialogTextContent;
        public bool hoverInMainPage;
        public float borderedSize;
        public const int MaxGreetingCount = 13;
        private static float piscicultureUI_MainBox_sengs => PiscicultureUI._sengs;
        public override void Update() {
            UpdateElement();
            HanderBorderedSize();
            if (hoverInMainPage) {
                player.mouseInterface = true;
            }
            DialogTextContent = GetDialogTextContent();
            HanderGreetingData();
        }
        /// <summary>
        /// 设置基本UI元素的属性，这个函数的更新一般靠在最前
        /// </summary>
        internal void UpdateElement() {
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

            borderedWidth = (int)(400 * bordered_sengs);
            borderedHeight = (int)(300 * bordered_sengs);
            borderedDrawPos = new Vector2((Main.screenWidth - borderedWidth) / 2, Main.screenHeight - 300);

            HitBox = new Rectangle((int)(borderedDrawPos.X), (int)(borderedDrawPos.Y), borderedWidth, borderedHeight);
            Rectangle mouseRec = new Rectangle((int)MousePosition.X, (int)MousePosition.Y, 1, 1);
            hoverInMainPage = mouseRec.Intersects(HitBox);
        }
        /// <summary>
        /// 管理对话框尺寸
        /// </summary>
        private void HanderBorderedSize() {
            float targetBorderedSize = 1;
            if (hoverInMainPage) {
                targetBorderedSize = 1.02f;
            }
            borderedSize = MathHelper.Lerp(borderedSize, targetBorderedSize, 0.2f);
            //实际上这种情况有可能发生，在某些更新情况下，如果不判断这个，对话框可能会闪烁
            if (borderedSize < 1f || borderedSize > 1.1f) {
                borderedSize = 1f;
            }
        }
        /// <summary>
        /// 管理对话数据，根据用户的输入切换对话进度
        /// </summary>
        private void HanderGreetingData() {
            if (hoverInMainPage) {
                if (keyLeftPressState == KeyPressState.Pressed) {
                    if (sessionText_sengs < 1f) {
                        sessionText_sengs = 1f;
                        return;
                    }
                    if (sessionText_sengs >= 1f) {
                        if (++GreetingIndex > MaxGreetingCount) {
                            GreetingIndex = 0;
                        }
                        sessionText_sengs = 0;
                        HanderFishItem.HanderPressed();
                    }
                }
            }
        }
        /// <summary>
        /// 随机将GreetingIndex切换
        /// </summary>
        internal void StochasticSwitchingGreeting() => GreetingIndex = Main.rand.Next(0, MaxGreetingCount);
        /// <summary>
        /// 获取对话文本
        /// </summary>
        /// <returns></returns>
        public string GetDialogTextContent() {
            // 获取文本内容
            string textContent = HalibutText.GetTextValue($"Greeting{GreetingIndex}");

            HanderFishItem.HanderItemText(ref textContent);

            float textLengthFactor = textContent.Length / 22.0f; // 根据文本长度动态调整，20 是一个基准长度
            float progressSpeed = 0.01f * (1.0f / textLengthFactor); // 控制速度，越短的文本，progressSpeed 越大

            if (bordered_sengs >= 1) {
                if (sessionText_sengs < 1) {
                    sessionText_sengs += progressSpeed; // 使用动态调整的进度速度
                    sessionText_sengs = MathHelper.Clamp(sessionText_sengs, 0.0f, 1.0f); // 防止超过1.0f
                }
            }
            else {
                sessionText_sengs = 0;
            }

            textContent = CWRUtils.GetTextProgressively(textContent, sessionText_sengs);
            Vector2 textSize = FontAssets.MouseText.Value.MeasureString(textContent);
            textContent = CWRUtils.GetSafeText(textContent, textSize, borderedWidth - 22 * 2);

            return textContent;
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (piscicultureUI_MainBox_sengs >= 1) {
                VaultUtils.DrawBorderedRectangle(spriteBatch, CWRAsset.UI_JAR.Value, 4, borderedDrawPos, borderedWidth, borderedHeight
                    , Color.BlueViolet * 0.8f * piscicultureUI_MainBox_sengs, Color.Azure * 0.2f * piscicultureUI_MainBox_sengs, borderedSize);
            }
            if (bordered_sengs >= 1) {
                Utils.DrawBorderStringFourWay(Main.spriteBatch, FontAssets.MouseText.Value, DialogTextContent
                        , borderedDrawPos.X + 20, borderedDrawPos.Y + 20, Color.AliceBlue, Color.Black, Vector2.Zero, 1f);
            }
        }
    }
}
