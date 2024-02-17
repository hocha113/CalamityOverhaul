using CalamityOverhaul.Common;
using CalamityOverhaul.Content.RemakeItems.Core;
using CalamityOverhaul.Content.UIs.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Runtime.Intrinsics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.UIs.SupertableUIs
{
    internal class MouseTextContactPanel : CWRUIPanel
    {
        internal static MouseTextContactPanel Instance { get; private set; }

        public override void Load() {
            Instance = this;
            Instance.DrawPos = new Vector2(700, 100);
        }

        private static Vector2 origPos => InItemDrawRecipe.Instance.DrawPos;

        private Vector2 offset;

        public override Texture2D Texture => CWRUtils.GetT2DValue("CalamityOverhaul/Assets/UIs/SupertableUIs/MouseTextContactPanel");

        public void UpdateSets() {
            int Mus = DownStartR();
            if (Mus == 1) {
                SoundEngine.PlaySound(SoundID.Chat);
                InItemDrawRecipe.Instance.DrawBool = !InItemDrawRecipe.Instance.DrawBool;
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (InItemDrawRecipe.Instance.DrawPos == Vector2.Zero) {
                InItemDrawRecipe.Instance.DrawPos = new Vector2(700, 100);
            }
            DrawPos = origPos + offset;
            if (InItemDrawRecipe.Instance.DrawBool) {
                if (offset.Y > -30)
                    offset.Y -= 5;
            }
            else {
                if (offset.Y < 0)
                    offset.Y += 5;
            }
            Vector2 uiSize = new Vector2(1.5f, 0.6f);
            string text = CWRLocText.GetTextValue("MouseTextContactPanel_TextContent");
            Vector2 size = FontAssets.MouseText.Value.MeasureString(text);
            float overSizeX = size.X / (uiSize.X * Texture.Width);
            spriteBatch.Draw(Texture, DrawPos, null, Color.DarkGoldenrod, 0, Vector2.Zero, uiSize * new Vector2(overSizeX, 1), SpriteEffects.None, 0);//绘制出UI主体

            Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, CWRUtils.GetSafeText(text, size, Texture.Width * uiSize.X)
                , DrawPos.X + 3, DrawPos.Y + 3, Color.White, Color.Black, new Vector2(0.3f), 1f);
        }
    }
}
