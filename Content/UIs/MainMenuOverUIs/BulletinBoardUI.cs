using CalamityOverhaul.Common;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using ReLogic.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.UIs.MainMenuOverUIs
{
    //老实说，这样的完全不利于扩展，如果想加入一条新的词条会非常麻烦，更别说如果想更换词条之间的顺序，更好的选择是将每个词条抽象成类实例管理 -hocah113 2024/6/28
    internal class BulletinBoardUI : UIHandle, ICWRLoader
    {
        #region Date
        internal static Asset<Texture2D> icon;
        internal static Asset<Texture2D> small;
        private static bool onText1;
        private static bool onText2;
        private static bool onText3;
        private static bool onText4;
        private static bool onText5;
        private static int Time;
        private static float sengs;
        public Asset<DynamicSpriteFont> Font { get; private set; }
        public override LayersModeEnum LayersMode => LayersModeEnum.Mod_MenuLoad;

        private string text0 => CWRLocText.GetTextValue("IconUI_Text0");
        private Vector2 text0Vr => Font.Value.MeasureString(text0);

        private string text1 => Mod.Name + " v" + Mod.Version;
        private Vector2 text1Vr => Font.Value.MeasureString(text1);
        private Vector2 text1Pos => new Vector2(DrawPosition.X - text1Vr.X + 76, DrawPosition.Y + 6);

        private string text2 => CWRLocText.GetTextValue("IconUI_Text8");
        private Vector2 text2Vr => Font.Value.MeasureString(text2);
        private Vector2 text2Pos => new Vector2(DrawPosition.X - text2Vr.X + 76, text1Pos.Y + text1Vr.Y - 2);

        private string text3 => CWRLocText.GetTextValue("IconUI_Text1");
        private Vector2 text3Vr => Font.Value.MeasureString(text3);
        private Vector2 text3Pos => new Vector2(DrawPosition.X - text3Vr.X + 76, text2Pos.Y + text1Vr.Y - 2);

        private string text4 => CWRLocText.GetTextValue("IconUI_Text7");
        private Vector2 text4Vr => Font.Value.MeasureString(text4);
        private Vector2 text4Pos => new Vector2(DrawPosition.X - text4Vr.X + 76, text3Pos.Y + text1Vr.Y - 2);

        private string text5 => CWRLocText.GetTextValue("IconUI_Text2");
        private Vector2 text5Vr => Font.Value.MeasureString(text5);
        private Vector2 text5Pos => new Vector2(DrawPosition.X - text5Vr.X + 76, text4Pos.Y + text1Vr.Y - 2);

        internal bool safeStart => !FeedbackUI.Instance.OnActive() && !AcknowledgmentUI.Instance.OnActive();
        public override bool Active => CWRLoad.OnLoadContentBool;

        #endregion
        void ICWRLoader.LoadAsset() {
            icon = CWRUtils.GetT2DAsset("CalamityOverhaul/icon");
            small = CWRUtils.GetT2DAsset("CalamityOverhaul/icon_small");
        }
        public override void Load() {
            Font = FontAssets.MouseText;
            sengs = 0;
            Time = 0;
        }
        public override void UnLoad() {
            icon = null;
            small = null;
            Font = null;
            sengs = 0;
            Time = 0;
        }

        public override void Update() {
            if (sengs < 1 && CWRLoad.OnLoadContentBool) {//如果CWRLoad都加载好了，那么本地化肯定已经加载好了
                sengs += 0.01f;
            }
            DrawPosition = new Vector2(Main.screenWidth - 82, -120 + sengs * 121);
            var mouseTarget = new Rectangle(Main.mouseX, Main.mouseY, 1, 1);
            onText1 = new Rectangle((int)text1Pos.X, (int)text1Pos.Y, (int)text1Vr.X, (int)text1Vr.Y - 5).Contains(mouseTarget);
            onText2 = new Rectangle((int)text2Pos.X, (int)text2Pos.Y, (int)text2Vr.X, (int)text2Vr.Y - 8).Contains(mouseTarget);
            onText3 = new Rectangle((int)text3Pos.X, (int)text3Pos.Y, (int)text3Vr.X, (int)text3Vr.Y - 8).Contains(mouseTarget);
            onText4 = new Rectangle((int)text4Pos.X, (int)text4Pos.Y, (int)text4Vr.X, (int)text4Vr.Y - 8).Contains(mouseTarget);
            onText5 = new Rectangle((int)text5Pos.X, (int)text5Pos.Y, (int)text5Vr.X, (int)text5Vr.Y - 8).Contains(mouseTarget);
            if (!safeStart) {
                onText1 = onText2 = onText3 = onText4 = onText5 = false;
            }
            //int mouS = DownStartL();
            int mouS = (int)keyLeftPressState;

            if (mouS == 1 && safeStart) {
                if (onText1) {
                    SoundEngine.PlaySound(SoundID.MenuOpen);
                    CWRConstant.githubUrl.WebRedirection();
                }
                else if (onText2) {
                    SoundEngine.PlaySound(SoundID.MenuOpen);
                    CWRConstant.modWikiUrl.WebRedirection();
                }
                else if (onText3) {
                    SoundEngine.PlaySound(SoundID.MenuOpen);
                    FeedbackUI.Instance._active = true;
                }
                else if (onText4) {
                    SoundEngine.PlaySound(SoundID.MenuOpen);
                    CWRConstant.steamIssueUrl.WebRedirection();
                }
                else if (onText5) {
                    SoundEngine.PlaySound(SoundID.MenuOpen);
                    AcknowledgmentUI.Instance._active = true;
                }
            }
            Time++;
        }

        public override void Draw(SpriteBatch spriteBatch) {
            Color color = VaultUtils.MultiStepColorLerp(Math.Abs(MathF.Sin(Time * 0.035f)), Color.Gold, Color.Green);
            Color higtColor = Color.White;

            Color textColor1 = (onText1 ? color : higtColor);
            Color textColor2 = (onText2 ? color : higtColor);
            Color textColor3 = (onText3 ? color : higtColor);
            Color textColor4 = (onText4 ? color : higtColor);
            Color textColor5 = (onText5 ? color : higtColor);

            float sengs2 = 1f;
            if (onText1 && safeStart) {
                sengs2 *= 0.3f;
            }

            Utils.DrawBorderStringFourWay(spriteBatch, Font.Value, VaultUtils.WrapTextToWidth(text1, text1Vr, 1000)
                , text1Pos.X, text1Pos.Y, textColor1 * sengs, Color.Black, new Vector2(0.2f), 1);
            Utils.DrawBorderStringFourWay(spriteBatch, Font.Value, VaultUtils.WrapTextToWidth(text2, text2Vr, 1000)
                , text2Pos.X, text2Pos.Y, textColor2 * sengs * sengs2, Color.Black * sengs2, new Vector2(0.2f), 1);
            Utils.DrawBorderStringFourWay(spriteBatch, Font.Value, VaultUtils.WrapTextToWidth(text3, text3Vr, 1000)
                , text3Pos.X, text3Pos.Y, textColor3 * sengs * sengs2, Color.Black * sengs2, new Vector2(0.2f), 1);
            Utils.DrawBorderStringFourWay(spriteBatch, Font.Value, VaultUtils.WrapTextToWidth(text4, text4Vr, 1000)
                , text4Pos.X, text4Pos.Y, textColor4 * sengs * sengs2, Color.Black * sengs2, new Vector2(0.2f), 1);
            Utils.DrawBorderStringFourWay(spriteBatch, Font.Value, VaultUtils.WrapTextToWidth(text5, text5Vr, 1000)
                , text5Pos.X, text5Pos.Y, textColor5 * sengs * sengs2, Color.Black * sengs2, new Vector2(0.2f), 1);

            if (onText1 && safeStart) {
                float textX = MousePosition.X - 60;
                if (textX > Main.screenWidth - text0Vr.X) {
                    textX = Main.screenWidth - text0Vr.X;
                }
                Utils.DrawBorderStringFourWay(spriteBatch, Font.Value, VaultUtils.WrapTextToWidth(text0, text0Vr, 1000)
                , textX, MousePosition.Y + 20, color, Color.Black, new Vector2(0.2f), 1);
            }
        }
    }
}
