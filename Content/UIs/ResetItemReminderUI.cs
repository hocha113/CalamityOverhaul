using CalamityOverhaul.Common;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.UIs
{
    internal class ResetItemReminderUI : ICWRLoader
    {
        public static ResetItemReminderUI Instance { get; private set; }
        public Texture2D mainBookPValue => CWRUtils.GetT2DValue("CalamityOverhaul/Assets/UIs/SupertableUIs/BookPans");
        void ICWRLoader.LoadData() => Instance = this;
        void ICWRLoader.UnLoadData() => Instance = null;
        public void Draw(SpriteBatch spriteBatch, Vector2 drawPos) {
            string textValue = CWRLocText.GetTextValue("RemakeItem_Remind_TextContent");
            spriteBatch.Draw(mainBookPValue, drawPos, null, Color.White, 0, Vector2.Zero, new Vector2(3.8f, 2.3f), SpriteEffects.None, 0);//绘制出UI主体
            Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, textValue, drawPos.X + 30, drawPos.Y + 30, Color.White, Color.Black, new Vector2(0.3f), 1f);
        }
    }
}
