using CalamityOverhaul.Content.UIs.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.UIs.MainMenuOverUIs
{
    internal class IconUI : BaseMainMenuOverUI
    {
        private static Asset<Texture2D> icon;
        private static Asset<Texture2D> small;

        public override void Load() {
            icon = CWRUtils.GetT2DAsset("CalamityOverhaul/icon");
            small = CWRUtils.GetT2DAsset("CalamityOverhaul/icon_small");
        }

        public override void Update(GameTime gameTime) {
        }

        public override void Draw(SpriteBatch spriteBatch) {
        }
    }
}
