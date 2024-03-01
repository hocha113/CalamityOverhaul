using CalamityMod;
using CalamityOverhaul.Content.UIs.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using static System.Net.Mime.MediaTypeNames;

namespace CalamityOverhaul.Content.UIs
{
    internal class CartridgeHolderUI : CWRUIPanel
    {
        public static CartridgeHolderUI Instance;
        public override Texture2D Texture => CWRUtils.GetT2DValue("CalamityOverhaul/Assets/UIs/BulletCard");
        public bool Active {
            get {
                Item handItem = player.ActiveItem();
                if (handItem.type == ItemID.None) {
                    return false;
                }
                return handItem.CWR().HasCartridgeHolder;
            }
        }

        private int bulletNum => player.ActiveItem().CWR().NumberBullets;

        public override void Load() => Instance = this;

        public override void Initialize() {
            DrawPos = new Vector2(20, Main.screenHeight - 100);
        }

        public override void Update(GameTime gameTime) {
            Initialize();
            time++;
        }

        public override void Draw(SpriteBatch spriteBatch) {
            //绘制UI主体
            spriteBatch.Draw(Texture, DrawPos, null, Color.White, 0f, Vector2.Zero, 2, SpriteEffects.None, 0);
            Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.ItemStack.Value, player.ActiveItem().CWR().NumberBullets.ToString()
                , DrawPos.X + 90, DrawPos.Y + 0, Color.White, Color.Black, Vector2.Zero, 1.5f);
        }
    }
}
