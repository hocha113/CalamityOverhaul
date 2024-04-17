using CalamityMod.UI;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Events;
using CalamityOverhaul.Content.UIs.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.Localization;
using static Terraria.GameContent.Animations.IL_Actions.Sprites;

namespace CalamityOverhaul.Content.UIs
{
    internal class TungstenRiotUI : CWRUIPanel
    {
        public static TungstenRiotUI Instance;
        public static Asset<Texture2D> icon;
        private string name => CWRLocText.GetTextValue("Event_TungstenRiot_Name");
        public bool Active {
            get {
                bool value = TungstenRiot.Instance.TungstenRiotIsOngoing;
                if (value) {
                    if (snegs < 1) {
                        snegs += 0.01f;
                    }
                    return true;
                }
                else {
                    if (snegs > 0) {
                        snegs -= 0.01f;
                    }
                    else {
                        return false;
                    }
                    return true;
                }
            }
        }
        private float snegs;
        public override void Load() {
            Instance = this;
            icon = CWRUtils.GetT2DAsset(CWRConstant.Asset + "Events/TungstenRiotIcon");
        }

        public override void Update(GameTime gameTime) {
            DrawPos = new Vector2(Main.screenWidth - 60, Main.screenHeight - 60);
        }

        public override void Draw(SpriteBatch spriteBatch) {
            DrawMainBar(spriteBatch, snegs);
        }

        public void DrawMainBar(SpriteBatch spriteBatch, float size) {
            if (snegs < 0.05f) {
                return;
            }

            float ration = TungstenRiot.Instance.EventKillRatio;

            Vector2 textMeasurement = FontAssets.MouseText.Value.MeasureString(name);
            float x = 120f;
            if (textMeasurement.X > 200f) {
                x += textMeasurement.X - 200f;
            }
            Rectangle iconRectangle = Utils.CenteredRectangle(new Vector2(Main.screenWidth - x, Main.screenHeight - 80 + 1), textMeasurement + new Vector2(icon.Value.Width + 12, 6f));
            Utils.DrawInvBG(spriteBatch, iconRectangle, TungstenRiot.Instance.MainColor * 0.5f * snegs);
            spriteBatch.Draw(icon.Value, iconRectangle.Left() + Vector2.UnitX * 8f, null, Color.White * snegs, 0f, Vector2.UnitY * icon.Value.Height / 2, 0.8f * size, SpriteEffects.None, 0f);
            Utils.DrawBorderString(spriteBatch, name, iconRectangle.Right() + Vector2.UnitX * -16f, Color.White * snegs, 0.9f * snegs, 1f, 0.4f, -1);

            int barWidth = 200;
            int barHeight = 45;

            DrawPos += new Vector2(-100, 20);

            Rectangle screenCoordsRectangle = new Rectangle((int)DrawPos.X - barWidth / 2, (int)DrawPos.Y - barHeight / 2, barWidth, barHeight);
            Texture2D barTexture = TextureAssets.ColorBar.Value;

            Utils.DrawInvBG(spriteBatch, screenCoordsRectangle, new Color(6, 80, 84, 255) * 0.785f * snegs);
            spriteBatch.Draw(barTexture, DrawPos, null, Color.White * snegs, 0f, new Vector2(barTexture.Width / 2, 0f), 1f * snegs, SpriteEffects.None, 0f);

            string progressText = (100 * ration).ToString($"N{1}") + "%";
            progressText = Language.GetTextValue("Game.WaveCleared", progressText);

            Vector2 textSize = FontAssets.MouseText.Value.MeasureString(progressText);
            float progressTextScale = 1f;
            if (textSize.Y > 22f)
                progressTextScale *= 22f / textSize.Y;

            DrawPos.Y += 10;
            Utils.DrawBorderString(spriteBatch, progressText, DrawPos - Vector2.UnitY * 4f, Color.White * snegs, progressTextScale, 0.5f, 1f, -1);

            float barDrawOffsetX = 169f;
            Vector2 barDrawPosition = DrawPos + Vector2.UnitX * (ration - 0.5f) * barDrawOffsetX;
            spriteBatch.Draw(TextureAssets.MagicPixel.Value, barDrawPosition, new Rectangle(0, 0, 1, 1), new Color(255, 241, 51) * snegs, 0f, new Vector2(1f, 0.5f), new Vector2(barDrawOffsetX * ration, 8) * snegs, SpriteEffects.None, 0f);
            spriteBatch.Draw(TextureAssets.MagicPixel.Value, barDrawPosition, new Rectangle(0, 0, 1, 1), new Color(255, 165, 0, 127) * snegs, 0f, new Vector2(1f, 0.5f), new Vector2(2f, 8) * snegs, SpriteEffects.None, 0f);
            spriteBatch.Draw(TextureAssets.MagicPixel.Value, barDrawPosition, new Rectangle(0, 0, 1, 1), Color.Black * snegs, 0f, Vector2.UnitY * 0.5f, new Vector2(barDrawOffsetX * (1f - ration), 8) * snegs, SpriteEffects.None, 0f);
        }
    }
}
