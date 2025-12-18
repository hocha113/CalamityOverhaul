using CalamityOverhaul.Common;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.UIs.MainMenuOverUIs
{
    internal class CompatibilityAlert : UIHandle, ILocalizedModType
    {
        public override LayersModeEnum LayersMode => LayersModeEnum.Mod_MenuLoad;

        public string LocalizationCategory => "UI";

        public static LocalizedText TitleText;
        public static LocalizedText ContentText;

        private static bool hasShown;
        private int timer;
        private const int SlideTime = 20;
        private const int DisplayTime = 600;

        private string wrappedContent;
        private float panelHeight;

        public override bool Active => VaultLoad.LoadenContent && !CWRRef.Has;

        public override void SetStaticDefaults() {
            TitleText = this.GetLocalization(nameof(TitleText), () => "当前正处于兼容模式");
            ContentText = this.GetLocalization(nameof(ContentText), () => "当前未启用部分模组或版本不匹配，模组的部分功能将被隐藏或者以兼容模式运行");
        }

        public override void Update() {
            if (CWRRef.Has) {
                return;
            }

            if (!hasShown) {
                hasShown = true;
                SoundEngine.PlaySound(CWRSound.Rollout);
            }

            if (wrappedContent == null) {
                RecalculateLayout();
            }

            if (hasShown) {
                if (timer < SlideTime + DisplayTime) {
                    CheckInput();
                }
                if (timer < SlideTime * 2 + DisplayTime) {
                    timer++;
                }
            }
        }

        private void RecalculateLayout() {
            string title = TitleText.Value;
            string content = ContentText.Value;
            float width = 300;
            float padding = 10;
            float iconSize = 60;

            float textWidth = width - (padding * 3 + iconSize);
            string[] lines = Utils.WordwrapString(content, FontAssets.MouseText.Value, (int)textWidth, 10, out int _);
            List<string> lines2 = [];
            foreach (var line in lines) {
                if (string.IsNullOrEmpty(line)) {
                    continue;
                }
                lines2.Add(line.TrimEnd('-', ' '));
            }
            wrappedContent = string.Join("\n", lines2);

            float titleScale = 0.9f;
            float contentScale = 0.75f;
            Vector2 titleSize = FontAssets.MouseText.Value.MeasureString(title) * titleScale;
            Vector2 contentSize = FontAssets.MouseText.Value.MeasureString(wrappedContent) * contentScale;

            panelHeight = MathHelper.Max(80, padding * 2 + titleSize.Y + contentSize.Y + 4);
        }

        private void CheckInput() {
            float progress = GetProgress(timer);
            float width = 300;
            float x = Main.screenWidth - width * progress;
            float y = Main.screenHeight * 0.3f;
            Rectangle rect = new Rectangle((int)x, (int)y, (int)width, (int)panelHeight);

            if (rect.Contains(Main.MouseScreen.ToPoint())) {
                if (keyLeftPressState == KeyPressState.Pressed) {
                    SoundEngine.PlaySound(CWRSound.ButtonZero with { Volume = 0.6f, Pitch = -0.2f });
                    timer = SlideTime + DisplayTime;
                }
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (!hasShown || timer >= SlideTime * 2 + DisplayTime) {
                return;
            }

            if (wrappedContent == null) {
                RecalculateLayout();
            }

            float progress = GetProgress(timer);
            string title = TitleText.Value;

            float width = 300;
            float height = panelHeight;
            float padding = 10;
            float iconSize = 60;

            //计算位置
            float x = Main.screenWidth - width * progress;
            float y = Main.screenHeight * 0.3f;

            Rectangle panelRect = new Rectangle((int)x, (int)y, (int)width, (int)height);

            //绘制背景
            spriteBatch.Draw(TextureAssets.MagicPixel.Value, panelRect, Color.Black * 0.7f);
            //简单的边框线条
            spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(panelRect.X, panelRect.Y, panelRect.Width, 2), Color.Gold);
            spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(panelRect.X, panelRect.Y + panelRect.Height - 2, panelRect.Width, 2), Color.Gold);
            spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(panelRect.X, panelRect.Y, 2, panelRect.Height), Color.Gold);

            //绘制图标
            Texture2D icon = CWRAsset.icon_small.Value;
            Vector2 iconPos = new Vector2(x + padding, y + padding);

            if (icon != null) {
                Rectangle src = icon.Frame();
                float scale = iconSize / MathHelper.Max(src.Width, src.Height);
                spriteBatch.Draw(icon, iconPos + new Vector2(iconSize / 2, iconSize / 2), src, Color.White, 0f, src.Size() / 2, scale, SpriteEffects.None, 0f);
            }

            //绘制文字
            float textX = x + padding * 2 + iconSize;
            float titleScale = 0.9f;
            float contentScale = 0.75f;
            Vector2 titleSize = FontAssets.MouseText.Value.MeasureString(title) * titleScale;

            Utils.DrawBorderString(spriteBatch, title, new Vector2(textX, y + padding), Color.Gold, titleScale);
            Utils.DrawBorderString(spriteBatch, wrappedContent, new Vector2(textX, y + padding + titleSize.Y + 2), Color.White, contentScale);
        }

        private float GetProgress(int timer) {
            if (timer < SlideTime) {
                float p = timer / (float)SlideTime;
                return 1f - (1f - p) * (1f - p);
            }
            else if (timer > SlideTime + DisplayTime) {
                float p = 1f - (timer - SlideTime - DisplayTime) / (float)SlideTime;
                return 1f - (1f - p) * (1f - p);
            }
            return 1f;
        }
    }
}