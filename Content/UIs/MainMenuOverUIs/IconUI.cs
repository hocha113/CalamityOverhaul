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
        private static Asset<Texture2D> icon;
        private static Asset<Texture2D> small;
        private static bool onText1;
        private static bool onText2;
        private static Rectangle Text1P;
        private static Rectangle Text2P;
        private static int Time;
        private static float sengs;
        string text1 => CWRLocText.GetTextValue("IconUI_Text1");
        Vector2 text1Vr => FontAssets.MouseText.Value.MeasureString(text1);
        Vector2 text1Pos => new Vector2(DrawPos.X - text1Vr.X, DrawPos.Y + 6);
        string text2 => CWRLocText.GetTextValue("IconUI_Text2");
        Vector2 text2Vr => FontAssets.MouseText.Value.MeasureString(text2);
        Vector2 text2Pos => new Vector2(text1Pos.X, text1Pos.Y + text1Vr.Y - 6);
        public override void Load() {
            icon = CWRUtils.GetT2DAsset("CalamityOverhaul/icon");
            small = CWRUtils.GetT2DAsset("CalamityOverhaul/icon_small");
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
            if (sengs < 1 && !CWRIDs.OnLoadContentBool) {//如果CWRID都加载好了，那么本地化肯定已经加载好了
                sengs += 0.01f;
            }
            DrawPos = new Vector2(Main.screenWidth - 82, -100 + sengs * 101);
            Text1P = new Rectangle((int)text1Pos.X, (int)text1Pos.Y, (int)text1Vr.X, (int)text1Vr.Y);
            Text2P = new Rectangle((int)text2Pos.X, (int)text2Pos.Y, (int)text2Vr.X, (int)text2Vr.Y);
            var mouseTarget = new Rectangle(Main.mouseX, Main.mouseY, 1, 1);
            onText1 = Text1P.Contains(mouseTarget);
            onText2 = Text2P.Contains(mouseTarget) && !onText1;
            int mouS = DownStartL();
            if (mouS == 1) {
                if (onText1) {
                    SoundEngine.PlaySound(SoundID.MenuOpen);
                    (CWRConstant.githubUrl + "/issues/new").WebRedirection(true);
                }
                else if (onText2) {
                    SoundEngine.PlaySound(SoundID.MenuOpen);
                    "https://steamcommunity.com/workshop/filedetails/discussion/3161388997/4366878568237971869/".WebRedirection(true);
                }
            }
            Time++;
        }

        public override void Draw(SpriteBatch spriteBatch) {
            spriteBatch.Draw(icon.Value, DrawPos, null, Color.White * 0.5f * sengs, 0f, Vector2.Zero, 0.56f, SpriteEffects.None, 0);

            Color color = CWRUtils.MultiStepColorLerp(Math.Abs(MathF.Sin(Time * 0.035f)), Color.Gold, Color.Green);
            Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.ItemStack.Value, CWRUtils.GetSafeText(text1, text1Vr, 1000)
                , text1Pos.X, text1Pos.Y, (onText1 ? color : new Color(190, 210, 200)) * sengs, Color.Black, new Vector2(0.2f), 1);
            Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.ItemStack.Value, CWRUtils.GetSafeText(text2, text1Vr, 1000)
                , text2Pos.X, text2Pos.Y, (onText2 ? color : new Color(190, 210, 200)) * sengs, Color.Black, new Vector2(0.2f), 1);
        }
    }
}
