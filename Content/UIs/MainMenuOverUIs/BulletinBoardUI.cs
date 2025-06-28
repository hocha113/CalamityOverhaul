using CalamityOverhaul.Common;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.UIs.MainMenuOverUIs
{
    //老实说，这样的完全不利于扩展，如果想加入一条新的词条会非常麻烦，更别说如果想更换词条之间的顺序，更好的选择是将每个词条抽象成类实例管理 -hocah113 2024/6/28
    //Yes Yes Yes Yes Yes Yes Yes Yes Yes Yes Yes Yes 就是这样，这样才是优雅的 -hocah113 2025/2/1
    internal class BulletinBoardUI : UIHandle, ICWRLoader
    {
        #region Data
        [VaultLoaden("CalamityOverhaul/")]
        internal static Asset<Texture2D> icon = null;
        [VaultLoaden("CalamityOverhaul/")]
        internal static Asset<Texture2D> icon_small = null;
        public static int Time;
        public static float sengs;
        public static BulletinBoardUI Instance => UIHandleLoader.GetUIHandleOfType<BulletinBoardUI>();
        internal static bool SafeStart => !FeedbackUI.Instance.OnActive() && !AcknowledgmentUI.Instance.OnActive();
        public static Asset<DynamicSpriteFont> Font { get; private set; }
        public static List<BulletinBoardElement> bulletinBoardElements = [];
        private static string HoverText => CWRLocText.Instance.IconUI_Text0.Value;
        private static Vector2 HoverTextSize => Font.Value.MeasureString(HoverText);
        private string ModNameAndVersion => Mod.Name + " v" + Mod.Version;
        private Vector2 ModNameSize => Font.Value.MeasureString(ModNameAndVersion);
        internal Vector2 TrueDrawPos => new Vector2(Main.screenWidth - ModNameSize.X - 4, DrawPosition.Y);
        public override bool Active => CWRLoad.OnLoadContentBool;
        public override LayersModeEnum LayersMode => LayersModeEnum.Mod_MenuLoad;
        #endregion
        public override void Load() {
            Font = FontAssets.MouseText;
            sengs = 0;
            Time = 0;
        }
        void ICWRLoader.SetupData() {
            bulletinBoardElements = [];

            BulletinBoardElement wikiBulletinBoard = new BulletinBoardElement()
                .Setproperty(CWRLocText.Instance.IconUI_Text8, () => CWRConstant.modWikiUrl.WebRedirection());
            bulletinBoardElements.Add(wikiBulletinBoard);

            BulletinBoardElement feedbackUIbulletinBoard = new BulletinBoardElement()
                .Setproperty(CWRLocText.Instance.IconUI_Text1, () => FeedbackUI.Instance._active = true);
            bulletinBoardElements.Add(feedbackUIbulletinBoard);

            BulletinBoardElement logBulletinBoard = new BulletinBoardElement()
                .Setproperty(CWRLocText.Instance.IconUI_Text7, () => CWRConstant.steamLogUrl.WebRedirection());
            bulletinBoardElements.Add(logBulletinBoard);

            BulletinBoardElement acknowledgmentNulletinBoard = new BulletinBoardElement()
                .Setproperty(CWRLocText.Instance.IconUI_Text2, () => AcknowledgmentUI.Instance._active = true);
            bulletinBoardElements.Add(acknowledgmentNulletinBoard);
        }
        public override void UnLoad() {
            Font = null;
            sengs = 0;
            Time = 0;
            bulletinBoardElements?.Clear();
        }

        public override void Update() {
            if (sengs < 1 && CWRLoad.OnLoadContentBool) {//如果CWRLoad都加载好了，那么本地化肯定已经加载好了
                sengs += 0.01f;
            }
            UIHitBox = TrueDrawPos.GetRectangle(ModNameSize);
            hoverInMainPage = UIHitBox.Intersects(MousePosition.GetRectangle(1));
            DrawPosition = new Vector2(Main.screenWidth - 82, -120 + sengs * 121);
            if (hoverInMainPage && keyLeftPressState == KeyPressState.Pressed && SafeStart) {
                SoundEngine.PlaySound(SoundID.MenuOpen);
                CWRConstant.githubUrl.WebRedirection();
            }

            float loaderY = 0;
            if (bulletinBoardElements != null) {
                for (int i = 0; i < bulletinBoardElements.Count; i++) {
                    BulletinBoardElement bulletinBoardElement = bulletinBoardElements[i];
                    bulletinBoardElement.DrawPosition = DrawPosition + new Vector2(0, loaderY + ModNameSize.Y);
                    bulletinBoardElement.Update();
                    loaderY += bulletinBoardElement.TextSize.Y;
                }
            }

            Time++;
        }

        public override void Draw(SpriteBatch spriteBatch) {
            Color color = VaultUtils.MultiStepColorLerp(Math.Abs(MathF.Sin(Time * 0.035f)), Color.Gold, Color.Green);
            Color higtColor = Color.White;
            Color textColor = hoverInMainPage ? color : higtColor;

            float sengs2 = 1f;
            if (hoverInMainPage && SafeStart) {
                sengs2 *= 0.3f;
            }

            for (int i = 0; i < bulletinBoardElements.Count; i++) {
                BulletinBoardElement bulletinBoardElement = bulletinBoardElements[i];
                bulletinBoardElement.Draw(spriteBatch);
            }           

            Utils.DrawBorderStringFourWay(spriteBatch, Font.Value, VaultUtils.WrapTextToWidth(ModNameAndVersion, ModNameSize, 1000)
                , Main.screenWidth - ModNameSize.X - 4, DrawPosition.Y, textColor * sengs, Color.Black, new Vector2(0.2f), 1);

            if (hoverInMainPage && SafeStart) {
                float textX = MousePosition.X - 60;
                if (textX > Main.screenWidth - HoverTextSize.X) {
                    textX = Main.screenWidth - HoverTextSize.X;
                }
                Utils.DrawBorderStringFourWay(spriteBatch, Font.Value, VaultUtils.WrapTextToWidth(HoverText, HoverTextSize, 1000)
                , textX, MousePosition.Y + 20, hoverInMainPage ? color : higtColor, Color.Black, new Vector2(0.2f), 1);
            }
        }
    }
}
