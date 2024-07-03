using CalamityOverhaul.Common;
using CalamityOverhaul.Content.UIs.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.UIs.MainMenuOverUIs
{
    internal class IconUI : BaseMainMenuOverUI
    {
        #region Date
        private static Asset<Texture2D> icon;
        private static Asset<Texture2D> small;
        private static bool onText1;
        private static bool onText2;
        private static bool onText3;
        private static Rectangle Text1P;
        private static Rectangle Text2P;
        private static Rectangle Text3P;
        private static int Time;
        private static float sengs;
        private string text1 => CWRMod.Instance.Name + " v" + CWRMod.Instance.Version;
        private Vector2 text1Vr => FontAssets.MouseText.Value.MeasureString(text1);
        private Vector2 text1Pos => new Vector2(DrawPos.X - text1Vr.X + 90, DrawPos.Y + 6);
        private string text2 => CWRLocText.GetTextValue("IconUI_Text1");
        private Vector2 text2Vr => FontAssets.MouseText.Value.MeasureString(text2);
        private Vector2 text2Pos => new Vector2(DrawPos.X - text2Vr.X + 90, text1Pos.Y + text1Vr.Y - 6);
        private string text3 => CWRLocText.GetTextValue("IconUI_Text2");
        private Vector2 text3Vr => FontAssets.MouseText.Value.MeasureString(text3);
        private Vector2 text3Pos => new Vector2(DrawPos.X - text3Vr.X + 90, text2Pos.Y + text1Vr.Y - 6);
        #endregion
        public override void Load() {
            if (!Main.dedServ) {
                icon = CWRUtils.GetT2DAsset("CalamityOverhaul/icon");
                small = CWRUtils.GetT2DAsset("CalamityOverhaul/icon_small");
            }
            sengs = 0;
            Time = 0;
        }
        public override void UnLoad() {
            icon = null;
            small = null;
            sengs = 0;
            Time = 0;
        }

        public override void Update(GameTime gameTime) {
            if (sengs < 1 && CWRLoad.OnLoadContentBool) {//如果CWRLoad都加载好了，那么本地化肯定已经加载好了
                sengs += 0.01f;
            }
            DrawPos = new Vector2(Main.screenWidth - 82, -100 + sengs * 101);
            Text1P = new Rectangle((int)text1Pos.X, (int)text1Pos.Y, (int)text1Vr.X, (int)text1Vr.Y - 5);
            Text2P = new Rectangle((int)text2Pos.X, (int)text2Pos.Y, (int)text2Vr.X, (int)text2Vr.Y - 8);
            Text3P = new Rectangle((int)text3Pos.X, (int)text3Pos.Y, (int)text3Vr.X, (int)text3Vr.Y - 8);
            var mouseTarget = new Rectangle(Main.mouseX, Main.mouseY, 1, 1);
            onText1 = Text1P.Contains(mouseTarget);
            onText2 = Text2P.Contains(mouseTarget);
            onText3 = Text3P.Contains(mouseTarget);
            int mouS = DownStartL();
            if (mouS == 1 && !OpenUI.Instance.OnActive() && !AcknowledgmentUI.Instance.OnActive()) {
                if (onText1) {
                    SoundEngine.PlaySound(SoundID.MenuOpen);
                }
                else if (onText2) {
                    SoundEngine.PlaySound(SoundID.MenuOpen);
                    OpenUI.Instance._active = true;
                }
                else if (onText3) {
                    SoundEngine.PlaySound(SoundID.MenuOpen);
                    AcknowledgmentUI.Instance._active = true;
                }
            }
            Time++;
        }

        public override void Draw(SpriteBatch spriteBatch) {
            Color color = CWRUtils.MultiStepColorLerp(Math.Abs(MathF.Sin(Time * 0.035f)), Color.Gold, Color.Green);
            Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.ItemStack.Value, CWRUtils.GetSafeText(text1, text1Vr, 1000)
                , text1Pos.X, text1Pos.Y, (onText1 ? color : new Color(190, 210, 200)) * sengs, Color.Black, new Vector2(0.2f), 1);
            Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.ItemStack.Value, CWRUtils.GetSafeText(text2, text2Vr, 1000)
                , text2Pos.X, text2Pos.Y, (onText2 ? color : new Color(190, 210, 200)) * sengs, Color.Black, new Vector2(0.2f), 1);
            Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.ItemStack.Value, CWRUtils.GetSafeText(text3, text3Vr, 1000)
                , text3Pos.X, text3Pos.Y, (onText3 ? color : new Color(190, 210, 200)) * sengs, Color.Black, new Vector2(0.2f), 1);
        }
    }
}
