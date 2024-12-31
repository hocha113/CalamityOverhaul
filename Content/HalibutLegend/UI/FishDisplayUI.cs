using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.HalibutLegend.UI
{
    internal class FishDisplayUI : UIHandle
    {
        public override bool Active => PiscicultureUI.Instance.Active || bordered_sengs > 0;
        public override LayersModeEnum LayersMode => LayersModeEnum.None;
        public override float RenderPriority => 2;
        public int borderedWidth;
        public int borderedHeight;
        public float bordered_sengs;
        public float borderedSize;
        public bool hoverInMainPage;
        public FishSkill FishSkill;
        public override void Update() {
            UIHitBox = new Rectangle((int)DrawPosition.X, (int)DrawPosition.Y, borderedWidth, borderedHeight);
            Rectangle mouseRec = new Rectangle((int)MousePosition.X, (int)MousePosition.Y, 1, 1);
            hoverInMainPage = mouseRec.Intersects(UIHitBox);
            if (PiscicultureUI.Instance.Active) {
                if (bordered_sengs < 0) {
                    bordered_sengs += 0.1f;
                }
                if (borderedWidth < 80) {
                    borderedWidth += 2;
                }
                if (borderedHeight < 80) {
                    borderedHeight += 2;
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
                    SoundEngine.PlaySound(SoundID.Grab);
                    if (HanderFishItem.TargetFish != null) {
                        if (HanderFishItem.TargetFish == this) {
                            HanderFishItem.TargetFish = new FishDisplayUI();
                        }
                        else {
                            HanderFishItem.TargetFish = this;
                        }
                    }

                }
            }

            borderedSize = MathHelper.Lerp(borderedSize, targetData, 0.2f);
            borderedSize = MathHelper.Clamp(borderedSize, 1, 1.1f);
        }

        public override void Draw(SpriteBatch spriteBatch) {
            Color backColor = Color.Azure * 0.2f * PiscicultureUI._sengs;
            if (HanderFishItem.TargetFish != null && HanderFishItem.TargetFish == this) {
                backColor = Color.Gold;
            }
            VaultUtils.DrawBorderedRectangle(spriteBatch, CWRAsset.UI_JAR.Value
                , 4, DrawPosition, borderedWidth, borderedHeight
                , FishSkill.Item.GetColor(Color.White) * 0.8f * PiscicultureUI._sengs, backColor, borderedSize);
            if (FishSkill.Item.type > ItemID.None) {
                float drawSize = VaultUtils.GetDrawItemSize(FishSkill.Item, borderedWidth) * borderedSize;
                Vector2 drawPos = DrawPosition + new Vector2(borderedWidth, borderedHeight) / 2;
                VaultUtils.SimpleDrawItem(spriteBatch, FishSkill.Item.type, drawPos, drawSize, 0, Color.White);
            }
        }

        public void DrawSkillIntroduction(SpriteBatch spriteBatch) {
            Color backColor = Color.Gold * 0.2f;
            Vector2 drawPos = PiscicultureUI.Dialogbox.borderedDrawPos
                - new Vector2(borderedWidth * 4 + 22, 0);

            VaultUtils.DrawBorderedRectangle(spriteBatch, CWRAsset.UI_JAR.Value
                , 4, drawPos, borderedWidth * 4, borderedHeight * 3
                , Color.AliceBlue * 0.8f * PiscicultureUI._sengs, backColor, borderedSize);

            string textContent = FishSkill.Introduce;
            Vector2 textSize = FontAssets.MouseText.Value.MeasureString(textContent);
            textContent = CWRUtils.GetSafeText(textContent, textSize, borderedWidth * 4 - 22 * 2);
            Utils.DrawBorderStringFourWay(Main.spriteBatch, FontAssets.MouseText.Value, textContent
                , drawPos.X + 20, drawPos.Y + 20, Color.AliceBlue, Color.Black, Vector2.Zero, 1f);
        }
    }
}
