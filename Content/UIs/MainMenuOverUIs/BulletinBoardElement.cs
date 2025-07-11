using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;

namespace CalamityOverhaul.Content.UIs.MainMenuOverUIs
{
    internal class BulletinBoardElement : UIHandle
    {
        public override LayersModeEnum LayersMode => LayersModeEnum.None;
        public LocalizedText textContent;
        public Action downFunc;
        public bool becauseNewVersionDisco;
        public Vector2 TextSize => BulletinBoardUI.Font.Value.MeasureString(textContent.Value);
        public Vector2 trueDrawPos => new Vector2(Main.screenWidth - TextSize.X - 4, DrawPosition.Y);
        public BulletinBoardElement Setproperty(LocalizedText textContent, Action downFunc, bool becauseNewVersionDisco = false) {
            this.textContent = textContent;
            this.downFunc = downFunc;
            this.becauseNewVersionDisco = becauseNewVersionDisco;
            return this;
        }
        public override void Update() {
            UIHitBox = trueDrawPos.GetRectangle(TextSize);
            hoverInMainPage = UIHitBox.Intersects(MousePosition.GetRectangle(1));
            if (hoverInMainPage && keyLeftPressState == KeyPressState.Pressed && BulletinBoardUI.SafeStart) {
                SoundEngine.PlaySound(SoundID.MenuOpen);
                downFunc.Invoke();
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            Color color = VaultUtils.MultiStepColorLerp(Math.Abs(MathF.Sin(BulletinBoardUI.Time * 0.035f)), Color.Gold, Color.Green);
            Color higtColor = Color.White;
            Color textColor = hoverInMainPage ? color : higtColor;
            if (!hoverInMainPage && becauseNewVersionDisco && CheckedVersions.IsNewVersion) {
                textColor = Main.DiscoColor;
            }
            if (BulletinBoardUI.Instance.hoverInMainPage) {
                textColor *= 0.3f;
            }
            Utils.DrawBorderStringFourWay(spriteBatch, BulletinBoardUI.Font.Value, VaultUtils.WrapTextToWidth(textContent.Value, TextSize, 1000)
                , trueDrawPos.X, trueDrawPos.Y, textColor * BulletinBoardUI.sengs, Color.Black, new Vector2(0.2f), 1);
        }
    }
}
