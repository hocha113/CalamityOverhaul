using CalamityOverhaul.Common;
using CalamityOverhaul.Content.UIs.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.UIs.SupertableUIs
{
    internal class OneClickUI : CWRUIPanel
    {
        public static OneClickUI Instance;
        public static Asset<Texture2D> MainValue;

        private SupertableUI mainUI => SupertableUI.Instance;

        private Rectangle mainRec;
        private bool onMainP;
        private bool checkSetO;
        public override void Load() {
            Instance = this;
            if (!Main.dedServ) {
                MainValue = CWRUtils.GetT2DAsset("CalamityOverhaul/Assets/UIs/SupertableUIs/OneClick");
            }
        }
        public override void Update(GameTime gameTime) {
            DrawPos = mainUI.DrawPos + new Vector2(500, 360);
            mainRec = new Rectangle((int)DrawPos.X, (int)DrawPos.Y, 30, 30);
            onMainP = mainRec.Intersects(new Rectangle((int)MouPos.X, (int)MouPos.Y, 1, 1));
            int mouseS = DownStartL();
            if (onMainP) {
                checkSetO = false;
                for (int i = 0; i < mainUI.items.Length; i++) {
                    Item preItem2 = mainUI.items[i];
                    if (preItem2 == null) {
                        preItem2 = new Item();
                    }
                    if (preItem2.type != ItemID.None) {
                        checkSetO = true;
                        break;
                    }
                }
                if (mouseS == 1 && RecipeUI.Instance.index > 0) {
                    SupertableUI.PlayGrabSound();
                    mainUI.OneClickPFunc();
                    mainUI.OutItem();
                }
            }
        }
        public override void Draw(SpriteBatch spriteBatch) {
            Color color = Color.White;
            if (onMainP) {
                color = Color.Gold;
            }
            if (RecipeUI.Instance.index == 0) {
                color = Color.White * 0.3f;
            }
            spriteBatch.Draw(MainValue.Value, DrawPos, null, color, 0, Vector2.Zero, 1, SpriteEffects.None, 0);
            if (onMainP) {
                Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value
                , checkSetO ? CWRLocText.GetTextValue("SupMUI_OneClick_Text2") : CWRLocText.GetTextValue("SupMUI_OneClick_Text1")
                , DrawPos.X - 30, DrawPos.Y + 30, Color.White, Color.Black, new Vector2(0.3f), 0.8f);
            }
        }
    }
}
