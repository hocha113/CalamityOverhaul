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
    internal class MouseTextContactPanel : UIHandle
    {
        internal static MouseTextContactPanel Instance { get; private set; }
        public override Texture2D Texture => CWRUtils.GetT2DValue("CalamityOverhaul/Assets/UIs/SupertableUIs/MouseTextContactPanel");
        public override LayersModeEnum LayersMode => LayersModeEnum.None;
        internal static bool doDraw;
        private bool oldLeftCtrlPressed;
        private static Vector2 origPos => SynthesisPreviewUI.Instance.DrawPos;
        private Vector2 offset;

        public override void Load() {
            Instance = this;
            Instance.DrawPosition = new Vector2(700, 100);
        }

        public override void Update() {
            bool leftCtrlPressed = Main.keyState.IsKeyDown(Keys.LeftControl);
            if (leftCtrlPressed && !oldLeftCtrlPressed) {
                SoundEngine.PlaySound(SoundID.Chat);
                SynthesisPreviewUI.Instance.DrawBool = !SynthesisPreviewUI.Instance.DrawBool;
            }
            oldLeftCtrlPressed = leftCtrlPressed;
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (SynthesisPreviewUI.Instance.DrawPos == Vector2.Zero) {
                SynthesisPreviewUI.Instance.DrawPos = new Vector2(700, 100);
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

            Vector2 uiSize = new Vector2(1.5f, 0.6f);
            string text = CWRLocText.GetTextValue("MouseTextContactPanel_TextContent");
            Vector2 size = FontAssets.MouseText.Value.MeasureString(text);
            float overSizeX = size.X / (uiSize.X * Texture.Width);

            if (!doDraw) {
                return;
            }

            spriteBatch.Draw(Texture, DrawPosition, null, Color.DarkGoldenrod, 0, Vector2.Zero, uiSize * new Vector2(overSizeX * 1.1f, 1), SpriteEffects.None, 0);//绘制出UI主体

            Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, text, DrawPosition.X + 3, DrawPosition.Y + 3, Color.White, Color.Black, new Vector2(0.3f), 1f);
        }
    }
}
