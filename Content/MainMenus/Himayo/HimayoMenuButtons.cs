using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.MainMenus.Himayo
{
    /// <summary>自绘标题界面：左侧按钮列、标题簇、版本号与主题切换文字；按钮文案直接复用原版本地化</summary>
    internal static class HimayoMenuButtons
    {
        private readonly struct Entry(Func<string> label, Action action)
        {
            public readonly Func<string> Label = label;
            public readonly Action Action = action;
        }

        private static Entry[] entries;
        //悬停进度 0~1，逐钮缓动
        private static float[] hover;
        private static int hoverIndex = -1;
        private static bool prevLeftDown;
        private static bool prevRightDown;
        private static float themeSwitchHover;

        public static void Initialize() {
            entries = [
                new(() => Lang.menu[12].Value, HimayoMenuActions.OpenSinglePlayer),
                new(() => Lang.menu[13].Value, HimayoMenuActions.OpenMultiplayer),
                new(() => Lang.menu[131].Value, HimayoMenuActions.OpenAchievements),
                new(() => Language.GetTextValue("UI.Workshop"), HimayoMenuActions.OpenWorkshop),
                new(() => Lang.menu[14].Value, HimayoMenuActions.OpenSettings),
                new(() => Language.GetTextValue("UI.Credits"), HimayoMenuActions.OpenCredits),
                new(() => Lang.menu[15].Value, HimayoMenuActions.ExitGame),
            ];
            hover = new float[entries.Length];
            hoverIndex = -1;
        }

        public static void Reset() {
            if (hover != null) {
                Array.Clear(hover);
            }
            hoverIndex = -1;
            themeSwitchHover = 0f;
            prevLeftDown = Main.mouseLeft;
            prevRightDown = Main.mouseRight;
        }

        /// <summary>固定 60tick 更新悬停与点击；返回鼠标是否占用于按钮/切换文字（供花瓣交互让位）</summary>
        public static bool Tick(bool inputFree) {
            if (entries == null) {
                return false;
            }
            bool leftDown = Main.mouseLeft && Main.hasFocus;
            bool rightDown = Main.mouseRight && Main.hasFocus;
            bool leftClick = leftDown && !prevLeftDown;
            bool rightClick = rightDown && !prevRightDown;
            prevLeftDown = leftDown;
            prevRightDown = rightDown;

            Point mouse = new(Main.mouseX, Main.mouseY);
            int newHover = -1;
            if (inputFree) {
                for (int i = 0; i < entries.Length; i++) {
                    if (ButtonRect(i).Contains(mouse)) {
                        newHover = i;
                        break;
                    }
                }
            }
            if (newHover != hoverIndex && newHover >= 0) {
                SoundEngine.PlaySound(SoundID.MenuTick);
            }
            hoverIndex = newHover;
            for (int i = 0; i < entries.Length; i++) {
                float target = i == hoverIndex ? 1f : 0f;
                hover[i] += (target - hover[i]) * 0.22f;
            }
            if (leftClick && hoverIndex >= 0) {
                entries[hoverIndex].Action();
            }

            //主题切换文字：位置与交互复刻原版（左键下一款、右键上一款）；落地反射缺失时仅展示不可点
            bool switchHover = inputFree && HimayoMenuActions.ThemeSwitchReady
                && ThemeSwitchRect().Contains(mouse);
            themeSwitchHover += ((switchHover ? 1f : 0f) - themeSwitchHover) * 0.25f;
            if (switchHover) {
                if (leftClick) {
                    HimayoMenuActions.SwitchTheme(1);
                }
                else if (rightClick) {
                    HimayoMenuActions.SwitchTheme(-1);
                }
            }
            return hoverIndex >= 0 || switchHover;
        }

        private static Vector2 ButtonPos(int i) {
            float totalH = (entries.Length - 1) * HimayoMenuTheme.ButtonSpacing;
            float startY = Main.screenHeight * 0.5f - totalH * 0.5f + 26f;
            return new Vector2(HimayoMenuTheme.ButtonAnchorX, startY + i * HimayoMenuTheme.ButtonSpacing);
        }

        //命中盒用基础缩放测量，避免悬停放大引发的判定抖动
        private static Rectangle ButtonRect(int i) {
            Vector2 size = FontAssets.DeathText.Value.MeasureString(entries[i].Label()) * HimayoMenuTheme.ButtonTextScale;
            Vector2 pos = ButtonPos(i);
            return new Rectangle((int)pos.X - 10, (int)pos.Y - 4,
                (int)(size.X + HimayoMenuTheme.ButtonHoverSlide + 24f), (int)size.Y + 8);
        }

        public static void Draw(SpriteBatch spriteBatch, float fade) {
            if (entries == null) {
                return;
            }
            var font = FontAssets.DeathText.Value;
            Texture2D pixel = VaultAsset.placeholder2?.Value;
            float t = (float)Main.timeForVisualEffects * 0.016f;

            DrawTitle(spriteBatch, fade, pixel);

            for (int i = 0; i < entries.Length; i++) {
                string label = entries[i].Label();
                float hv = hover[i];
                Vector2 pos = ButtonPos(i);
                pos.X += hv * HimayoMenuTheme.ButtonHoverSlide;
                //呼吸微浮，逐钮相位错开
                pos.Y += MathF.Sin(t * 3.4f + i * 1.3f) * 1.6f;
                float scale = HimayoMenuTheme.ButtonTextScale + hv * HimayoMenuTheme.ButtonHoverScaleBonus;
                Color col = Color.Lerp(HimayoMenuTheme.TextDim, HimayoMenuTheme.TextIvory, 0.35f + hv * 0.65f) * fade;
                Utils.DrawBorderStringFourWay(spriteBatch, font, label, pos.X, pos.Y,
                    col, Color.Black * (0.72f * fade), Vector2.Zero, scale);

                //悬停下划线：亮粉细线自左向右展开（亮色描线，非暗色贴片）
                if (pixel != null && hv > 0.02f) {
                    Vector2 size = font.MeasureString(label) * scale;
                    int lw = (int)(size.X * hv);
                    int ly = (int)(pos.Y + size.Y * 0.80f);
                    spriteBatch.Draw(pixel, new Rectangle((int)pos.X, ly, lw, 2),
                        HimayoMenuTheme.AccentBloom * (0.85f * hv * fade));
                    spriteBatch.Draw(pixel, new Rectangle((int)pos.X, ly + 2, lw, 1),
                        HimayoMenuTheme.PetalPinkDeep * (0.35f * hv * fade));
                }
            }

            DrawVersion(spriteBatch, fade);
            DrawThemeSwitch(spriteBatch, fade);
        }

        private static void DrawTitle(SpriteBatch spriteBatch, float fade, Texture2D pixel) {
            var font = FontAssets.DeathText.Value;
            string title = CWRMod.Instance?.DisplayName ?? "Calamity Overhaul";
            Utils.DrawBorderStringFourWay(spriteBatch, font, title,
                HimayoMenuTheme.TitleX, HimayoMenuTheme.TitleY,
                HimayoMenuTheme.TextIvory * fade, Color.Black * (0.75f * fade), Vector2.Zero, 0.92f);

            Vector2 titleSize = font.MeasureString(title) * 0.92f;
            float ruleY = HimayoMenuTheme.TitleY + titleSize.Y * 0.86f;
            if (pixel != null) {
                //标题下细线，右端渐隐
                int ruleW = (int)MathF.Max(titleSize.X * 0.9f, 220f);
                spriteBatch.Draw(pixel, new Rectangle((int)HimayoMenuTheme.TitleX, (int)ruleY, ruleW, 1),
                    HimayoMenuTheme.AccentBloom * (0.55f * fade));
            }
            string sub = HimayoMenu.ThemeName?.Value ?? string.Empty;
            if (sub.Length > 0) {
                Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, sub,
                    HimayoMenuTheme.TitleX + 2f, ruleY + 8f,
                    HimayoMenuTheme.TextDim * fade, Color.Black * (0.6f * fade), Vector2.Zero, 0.95f);
            }
        }

        private static void DrawVersion(SpriteBatch spriteBatch, float fade) {
            //原版版本号绘制为私有方法，此处自绘等价信息
            string text = ModLoader.versionedName + "\nTerraria " + Main.versionNumber;
            Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, text,
                14f, Main.screenHeight - 56f,
                HimayoMenuTheme.TextDim * (0.85f * fade), Color.Black * (0.65f * fade), Vector2.Zero, 0.9f);
        }

        private static string ThemeSwitchText =>
            Language.GetTextValue("tModLoader.ModMenuSwap") + ": " + (MenuLoader.CurrentMenu?.DisplayName ?? "?");

        private static Rectangle ThemeSwitchRect() {
            Vector2 size = FontAssets.MouseText.Value.MeasureString(ThemeSwitchText);
            return new Rectangle((int)(Main.screenWidth * 0.5f - size.X * 0.5f),
                (int)(Main.screenHeight - 4f - size.Y), (int)size.X, (int)size.Y);
        }

        private static void DrawThemeSwitch(SpriteBatch spriteBatch, float fade) {
            Rectangle r = ThemeSwitchRect();
            Color baseCol = HimayoMenuActions.ThemeSwitchReady
                ? Color.Lerp(new Color(126, 116, 130), Main.OurFavoriteColor, themeSwitchHover)
                : new Color(90, 84, 96);
            Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, ThemeSwitchText,
                r.X, r.Y, baseCol * fade, Color.Black * (0.6f * fade), Vector2.Zero, 1f);
        }
    }
}
