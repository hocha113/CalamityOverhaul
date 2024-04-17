using CalamityOverhaul.Common;
using CalamityOverhaul.Content.UIs.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.UIs.MainMenuOverUIs
{
    internal class IconUI : BaseMainMenuOverUI
    {
        private static Asset<Texture2D> icon;
        private static Asset<Texture2D> small;
        private static bool onMainP;
        private static Rectangle MainP;

        public override bool CanLoad() {
            return false;//这个UI还没有完成，因此不要让它出现在主页上
        }

        public override void Load() {
            icon = CWRUtils.GetT2DAsset("CalamityOverhaul/icon");
            small = CWRUtils.GetT2DAsset("CalamityOverhaul/icon_small");
            DrawPos = new Vector2(-100, 0);
        }

        public override void Update(GameTime gameTime) {
            if (DrawPos.X < 0) {
                DrawPos.X+=2;
            }
            MainP = new Rectangle((int)DrawPos.X, (int)DrawPos.Y, icon.Value.Width, icon.Value.Height);
            onMainP = MainP.Contains(new Rectangle(Main.mouseX, Main.mouseY, 1, 1));
            int mouS = DownStartL();
            if (mouS == 1 && onMainP) {
                SoundEngine.PlaySound(SoundID.MenuOpen);
                CWRConstant.githubUrl.WebRedirection(false);
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (onMainP) {
                spriteBatch.Draw(icon.Value, DrawPos, null, Color.Gold, 0f, Vector2.Zero, 1.03f, SpriteEffects.None, 0);
            }
            spriteBatch.Draw(icon.Value, DrawPos, null, Color.White, 0f, Vector2.Zero, 1, SpriteEffects.None, 0);
        }
    }
}
