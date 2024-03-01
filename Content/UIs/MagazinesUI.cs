using CalamityMod;
using CalamityOverhaul.Content.UIs.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.UIs
{
    internal class MagazinesUI : CWRUIPanel
    {
        public static MagazinesUI Instance;
        public override Texture2D Texture => CWRUtils.GetT2DValue("CalamityOverhaul/Assets/UIs/Magazines");
        public bool Active {
            get {
                Item handItem = player.ActiveItem();
                if (handItem.type == ItemID.None) {
                    return false;
                }
                if (handItem.CWR().CartridgeEnum != CartridgeUIEnum.Magazines) {
                    return false;
                }
                return handItem.CWR().HasCartridgeHolder;
            }
        }

        private int bulletNum => player.ActiveItem().CWR().NumberBullets;

        public override void Load() => Instance = this;

        public override void Initialize() {
            DrawPos = new Vector2(60, Main.screenHeight - 100);
        }

        public override void Draw(SpriteBatch spriteBatch) {
            //绘制UI主体
            Initialize();
            spriteBatch.Draw(Texture, DrawPos, CWRUtils.GetRec(Texture, 6 - bulletNum, 7), Color.White, 0f, CWRUtils.GetOrig(Texture, 7), 2, SpriteEffects.None, 0);
        }
    }
}
