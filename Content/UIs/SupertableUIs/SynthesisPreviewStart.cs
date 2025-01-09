using CalamityOverhaul.Common;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.UIs.SupertableUIs
{
    internal class SynthesisPreviewStart : UIHandle
    {
        internal static SynthesisPreviewStart Instance { get; private set; }
        private bool oldLeftCtrlPressed;
        private static Vector2 origPos => SynthesisPreviewUI.Instance.DrawPosition;
        private Vector2 offset;
        internal float _sengs;
        internal bool uiIsActive => !SupertableUI.Instance.hoverInMainPage && CWRLoad.ItemIDToOmigaSnyContent[CWRUI.HoverItem.type] != null;
        public override bool Active => _sengs > 0 || uiIsActive;
        public override void Load() {
            Instance = this;
            Instance.DrawPosition = new Vector2(700, 100);
        }

        public override void Update() {
            if (uiIsActive) {
                if (_sengs < 1f) {
                    _sengs += 0.1f;
                }
            }
            else {
                if (_sengs > 0f) {
                    _sengs -= 0.1f;
                }
            }
            _sengs = MathHelper.Clamp(_sengs, 0, 1);

            bool leftCtrlPressed = Main.keyState.IsKeyDown(Keys.L);
            if (leftCtrlPressed && !oldLeftCtrlPressed) {
                SoundEngine.PlaySound(SoundID.Chat);
                SynthesisPreviewUI.Instance.DrawBool = !SynthesisPreviewUI.Instance.DrawBool;
            }
            oldLeftCtrlPressed = leftCtrlPressed;

            if (!SynthesisPreviewUI.Instance.DrawBool) {
                SynthesisPreviewUI.Instance.SetPosition();
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (SynthesisPreviewUI.Instance.DrawPosition == Vector2.Zero) {
                SynthesisPreviewUI.Instance.DrawPosition = new Vector2(700, 100);
            }

            DrawPosition = origPos + offset;

            if (SynthesisPreviewUI.Instance.DrawBool) {
                if (offset.Y > -30) {
                    offset.Y -= 5;
                }
            }
            else if (offset.Y < 0) {
                offset.Y += 5;
            }

            string text = CWRLocText.GetTextValue("MouseTextContactPanel_TextContent");
            text = text.Replace("[KEY]", "L");
            Vector2 size = FontAssets.MouseText.Value.MeasureString(text);

            VaultUtils.DrawBorderedRectangle(spriteBatch, CWRAsset.UI_JAR.Value, 4, DrawPosition
                , (int)(size.X + 22 * _sengs), (int)(size.Y * _sengs), Color.BlueViolet * 0.8f * _sengs, Color.Azure * 1.2f * _sengs, 0.8f + _sengs * 0.2f);
            Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, text, DrawPosition.X + 3, DrawPosition.Y + 3
                , Color.White * _sengs, Color.Black * _sengs, new Vector2(0.3f), 1f * _sengs);
        }
    }
}
