using CalamityOverhaul.Content.UIs.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using static System.Net.Mime.MediaTypeNames;

namespace CalamityOverhaul.Content.UIs.SupertableUIs
{
    internal class RecipeErrorFullUI : CWRUIPanel
    {
        public static RecipeErrorFullUI Instance;
        public override Texture2D Texture => CWRUtils.GetT2DValue("CalamityOverhaul/Assets/UIs/SupertableUIs/CallFull");
        public override void Load() => Instance = this;
        SupertableUI mainUI => SupertableUI.Instance;
        public Rectangle MainRec;
        public bool onMainP;
        public bool eyEBool;
        public override void Update(GameTime gameTime) {
            DrawPos = mainUI.DrawPos + new Vector2(500, 390);
            MainRec = new Rectangle((int)DrawPos.X, (int)DrawPos.Y, 30, 30);
            onMainP = MainRec.Intersects(new Rectangle((int)MouPos.X, (int)MouPos.Y, 1, 1));
            if (onMainP) {
                int mouseS = DownStartL();
                if (mouseS == 1) {
                    eyEBool = !eyEBool;
                    if (eyEBool) {
                        SoundEngine.PlaySound(SoundID.Unlock with { Pitch = 0.5f });
                    }
                    else {
                        SoundEngine.PlaySound(SoundID.Unlock with { Pitch = -0.5f });
                    }
                }
            }
        }
        public override void Draw(SpriteBatch spriteBatch) {
            Texture2D eye0 = CWRUtils.GetT2DValue("CalamityOverhaul/Assets/UIs/SupertableUIs/Eye0");
            Texture2D eye1 = CWRUtils.GetT2DValue("CalamityOverhaul/Assets/UIs/SupertableUIs/Eye1");
            spriteBatch.Draw(eye0, DrawPos, null, Color.White, 0, Vector2.Zero, 1, SpriteEffects.None, 0);
            if (eyEBool) {
                spriteBatch.Draw(eye1, DrawPos, null, Color.White, 0, Vector2.Zero, 1, SpriteEffects.None, 0);
            }
            for (int i = 0; i < mainUI.items.Length; i++) {
                if (mainUI.items[i].type != mainUI.previewItems[i].type && mainUI.items[i].type != ItemID.None && eyEBool) {
                    Vector2 pos = mainUI.ArcCellPos(i) + new Vector2(-1, -2);
                    spriteBatch.Draw(Texture, pos, null, Color.White * 0.6f, 0, Vector2.Zero, 1, SpriteEffects.None, 0);
                }
            }
            if (onMainP) {
                Terraria.Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value
                , eyEBool ? "关闭摆放监视" : "开启摆放监视"
                , DrawPos.X - 30, DrawPos.Y - 20, Color.White, Color.Black, new Vector2(0.3f), 0.8f);
            }
        }
    }
}
