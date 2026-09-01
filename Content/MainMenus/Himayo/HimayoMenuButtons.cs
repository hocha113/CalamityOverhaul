using CalamityOverhaul.Common;
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
        //悬停湿润度 0~1：停得越久朱墨越洇、垂痕越长；移开快速沥干
        private static float[] hoverWet;
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
            hoverWet = new float[entries.Length];
            hoverIndex = -1;
        }

        public static void Reset() {
            if (hover != null) {
                Array.Clear(hover);
            }
            if (hoverWet != null) {
                Array.Clear(hoverWet);
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
                //约4秒洇满；移开约半秒沥干
                hoverWet[i] = MathHelper.Clamp(
                    hoverWet[i] + (i == hoverIndex ? 1f / 240f : -1f / 30f), 0f, 1f);
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

        /// <summary>退出钮文字底边，供音乐提示锚在其下</summary>
        public static float ExitBottomY {
            get {
                if (entries == null || entries.Length == 0) {
                    return Main.screenHeight * 0.5f;
                }
                int i = entries.Length - 1;
                Vector2 size = FontAssets.DeathText.Value.MeasureString(entries[i].Label())
                    * HimayoMenuTheme.ButtonTextScale;
                return ButtonPos(i).Y + size.Y;
            }
        }

        public static void Draw(SpriteBatch spriteBatch, float fade) {
            if (entries == null) {
                return;
            }
            var font = FontAssets.DeathText.Value;
            Texture2D pixel = VaultAsset.placeholder2?.Value;
            float t = (float)Main.timeForVisualEffects * 0.016f;
            //朱墨着色器就绪则悬停走墨痕笔画；缺席退回双细线
            bool inkReady = EffectLoader.HimayoInkStroke?.Value != null
                && CWRAsset.PerlinNoise?.Value != null && pixel != null;

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

                //回退下划线：亮粉细线自左向右展开（亮色描线，非暗色贴片）
                if (!inkReady && pixel != null && hv > 0.02f) {
                    Vector2 size = font.MeasureString(label) * scale;
                    int lw = (int)(size.X * hv);
                    int ly = (int)(pos.Y + size.Y * 0.80f);
                    spriteBatch.Draw(pixel, new Rectangle((int)pos.X, ly, lw, 2),
                        HimayoMenuTheme.AccentBloom * (0.85f * hv * fade));
                    spriteBatch.Draw(pixel, new Rectangle((int)pos.X, ly + 2, lw, 1),
                        HimayoMenuTheme.PetalPinkDeep * (0.35f * hv * fade));
                }
            }

            if (inkReady) {
                DrawInkMarks(spriteBatch, pixel, font, t, fade);
            }

            DrawVersion(spriteBatch, fade);
            DrawThemeSwitch(spriteBatch, fade);
        }

        //====== 悬停墨痕：朱墨笔画着色器逐行绘制；进入时批次已开启，返回前交还同参批次 ======
        private static void DrawInkMarks(SpriteBatch spriteBatch, Texture2D pixel,
            ReLogic.Graphics.DynamicSpriteFont font, float t, float fade) {

            bool any = false;
            for (int i = 0; i < entries.Length; i++) {
                if (hover[i] > 0.02f) {
                    any = true;
                    break;
                }
            }
            if (!any) {
                return;
            }

            Effect ink = EffectLoader.HimayoInkStroke.Value;
            GraphicsDevice graphicsDevice = Main.instance.GraphicsDevice;

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, Main.Rasterizer,
                null, Main.UIScaleMatrix);
            try {
                graphicsDevice.Textures[1] = CWRAsset.PerlinNoise.Value;
                graphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
                ink.CurrentTechnique = ink.Techniques["TechInk"];

                const float QuadH = 46f;
                for (int i = 0; i < entries.Length; i++) {
                    float hv = hover[i];
                    if (hv <= 0.02f) {
                        continue;
                    }
                    //与文字同一套位置推导（含悬停滑移与呼吸浮动）
                    Vector2 pos = ButtonPos(i);
                    pos.X += hv * HimayoMenuTheme.ButtonHoverSlide;
                    pos.Y += MathF.Sin(t * 3.4f + i * 1.3f) * 1.6f;
                    float scale = HimayoMenuTheme.ButtonTextScale + hv * HimayoMenuTheme.ButtonHoverScaleBonus;
                    Vector2 size = font.MeasureString(entries[i].Label()) * scale;

                    float quadW = size.X + 18f;
                    float quadX = pos.X - 8f;
                    //笔迹脊线约在 quad 高 26% 处，对齐旧下划线位置
                    float quadY = pos.Y + size.Y * 0.80f - QuadH * 0.22f;

                    ink.Parameters["uTime"]?.SetValue(t);
                    ink.Parameters["uReveal"]?.SetValue(hv);
                    ink.Parameters["uWet"]?.SetValue(hoverWet[i]);
                    ink.Parameters["uSeed"]?.SetValue(1.7f + i * 2.31f);
                    ink.Parameters["uAspect"]?.SetValue(quadW / QuadH);
                    ink.Parameters["uFade"]?.SetValue(fade);
                    ink.CurrentTechnique.Passes[0].Apply();
                    spriteBatch.Draw(pixel, new Vector2(quadX, quadY), null, Color.White, 0f,
                        Vector2.Zero, new Vector2(quadW / pixel.Width, QuadH / pixel.Height),
                        SpriteEffects.None, 0f);
                }
            } finally {
                spriteBatch.End();
                graphicsDevice.Textures[1] = null;
                //交还 chrome 常规批次
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                    SamplerState.LinearClamp, DepthStencilState.None, Main.Rasterizer,
                    null, Main.UIScaleMatrix);
            }
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
