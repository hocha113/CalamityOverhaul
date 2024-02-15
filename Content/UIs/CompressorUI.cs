using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.UIs
{
    internal class CompressorUI
    {
        public static CompressorUI Instance;

        public static Texture2D Panel;

        public static Texture2D Bar;

        public Vector2 DrawPos;

        public Rectangle MaterialSlotRec;

        public Rectangle FinishedSlot;

        public void Load() {
            Instance = this;
        }

        public void Initialize() {
            if (DrawPos == Vector2.Zero) {
                DrawPos = new Vector2(Main.screenWidth - Panel.Width, Main.screenHeight - Panel.Height + 130) / 2;
                MaterialSlotRec = new Rectangle((int)(DrawPos.X + 38), (int)(DrawPos.Y + 34), 18, 18);
                FinishedSlot = new Rectangle((int)(DrawPos.X + 113), (int)(DrawPos.Y + 31), 26, 26);
            }
        }

        public void Update() {
            Initialize();
        }

        public void Draw(SpriteBatch spriteBatch) {
            if (Panel == null) {
                Panel = CWRUtils.GetT2DValue("CalamityOverhaul/Assets/UIs/CompressorPanel", true);
                Bar = CWRUtils.GetT2DValue("CalamityOverhaul/Assets/UIs/CompressorBar", true);
            }
            spriteBatch.Draw(Panel, DrawPos, null, Color.White, 0, Vector2.Zero, 1, SpriteEffects.None, 0);
            spriteBatch.Draw(Bar, DrawPos + new Vector2(63, 36), null, Color.White, 0, Vector2.Zero, 1, SpriteEffects.None, 0);
        }
    }
}
