using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;

namespace CalamityOverhaul.Content.Scenarios.OldNet.UI
{
    /// <summary>
    /// 旧网加载屏：神经深潜握手界面。<br/>
    /// 背景复用 CybCourseLoading 着色器（缺失时纯黑），文案为越墙主题；
    /// 接线方式照抄 Dungeonworld 的 A 路薄转发（OldNetWorld 内各一行）
    /// </summary>
    internal static class OldNetLoadingScreen
    {
        private static float loadTime;
        private static bool entering = true;
        //估时钉住 95%，实际完成由 SubLib 切场景
        private const float EstDuration = 4.5f;

        /// <summary>进入方向复位（EnterWorld 在 SubworldSystem.Enter 之前调）</summary>
        public static void Enter() {
            loadTime = 0f;
            entering = true;
        }

        /// <summary>退出方向复位；OnExit 兜底重复调用无害</summary>
        public static void Exit() {
            loadTime = 0f;
            entering = false;
        }

        public static void DrawSetup(GameTime gameTime) {
            //限单帧增量避跳变（CybCourse 先例）
            loadTime += 0.02f;

            PlayerInput.SetZoom_UI();
            Main.instance.GraphicsDevice.Clear(Color.Black);

            DrawShaderBackground();

            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, Main.Rasterizer,
                null, Main.UIScaleMatrix);
            DrawMenu();
            Main.DrawCursor(Main.DrawThickCursor());
            Main.spriteBatch.End();
        }

        public static bool ChangeAudio() {
            if (Main.gameMenu) {
                Main.newMusic = 0;
                return true;
            }
            return false;
        }

        private static float Progress =>
            MathHelper.SmoothStep(0f, 1f, MathHelper.Clamp(loadTime / EstDuration, 0f, 0.95f));

        private static void DrawShaderBackground() {
            var shader = EffectLoader.CybCourseLoading?.Value;
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (shader == null || px == null || px.IsDisposed) {
                return;
            }
            int w = Main.screenWidth;
            int h = Main.screenHeight;

            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, Main.Rasterizer,
                null, Main.UIScaleMatrix);
            shader.Parameters["uTime"]?.SetValue(loadTime);
            shader.Parameters["uProgress"]?.SetValue(Progress);
            shader.Parameters["uAspectRatio"]?.SetValue((float)w / h);
            shader.CurrentTechnique.Passes[0].Apply();
            Main.spriteBatch.Draw(px, new Rectangle(0, 0, w, h), Color.White);
            Main.spriteBatch.End();
        }

        private static void DrawMenu() {
            int sw = Main.screenWidth;
            int sh = Main.screenHeight;
            float progress = Progress;

            Color ember = new(235, 64, 44);      //黑墙红
            Color cold = new(140, 200, 210);     //旧网冷青
            Color dim = new(150, 160, 175);

            DynamicSpriteFont titleFont = FontAssets.DeathText.Value;
            DynamicSpriteFont bodyFont = FontAssets.MouseText.Value;
            Texture2D px = VaultAsset.placeholder2.Value;

            //顶部标识
            string tag = entering ? "// OLDNET LINK  BEYOND-BLACKWALL" : "// OLDNET LINK  DISCONNECT";
            Main.spriteBatch.DrawString(bodyFont, tag,
                new Vector2(sw * 0.034f, sh * 0.090f), dim * 0.50f);

            //标题块
            string title = entering ? "NEURAL  DIVE" : "LINK  SEVERED";
            Vector2 titleSz = titleFont.MeasureString(title);
            Vector2 titlePos = new(sw * 0.5f - titleSz.X * 0.5f, sh * 0.180f);
            Main.spriteBatch.DrawString(titleFont, title, titlePos + new Vector2(2f, 3f), Color.Black * 0.55f);
            Main.spriteBatch.DrawString(titleFont, title, titlePos, entering ? ember : cold);

            string sub = "OLD NET  PROTOCOL";
            Vector2 subSz = bodyFont.MeasureString(sub);
            Vector2 subPos = new(sw * 0.5f - subSz.X * 0.5f, titlePos.Y + titleSz.Y + 8f);
            Main.spriteBatch.DrawString(bodyFont, sub, subPos, dim * 0.65f);

            //分隔线
            int ulY = (int)(subPos.Y + subSz.Y + 14f);
            int ulW = (int)(titleSz.X * 0.55f);
            int ulX = (int)(sw * 0.5f - ulW / 2f);
            Main.spriteBatch.Draw(px, new Rectangle(ulX, ulY, ulW, 1), new Rectangle(0, 0, 1, 1), ember * 0.55f);

            //百分比
            int pct = (int)(progress * 100);
            string num = pct.ToString("D2");
            Vector2 numSz = titleFont.MeasureString(num);
            Vector2 scale = new(0.92f);
            Vector2 numPos = new(sw * 0.5f - numSz.X * scale.X * 0.5f, sh * 0.510f - numSz.Y * scale.Y * 0.5f);
            Main.spriteBatch.DrawString(titleFont, num, numPos + new Vector2(2f, 3f), Color.Black * 0.55f,
                0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            Main.spriteBatch.DrawString(titleFont, num, numPos, entering ? ember : cold,
                0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

            //状态行
            string status = (Main.statusText ?? string.Empty).ToUpperInvariant();
            if (string.IsNullOrEmpty(status)) {
                status = entering ? "CROSSING THE BLACKWALL" : "RETURNING TO MEATSPACE";
            }
            int dotN = (int)(loadTime * 1.7f) % 4;
            string full = status + new string('.', dotN);
            Vector2 sz = bodyFont.MeasureString(full);
            Vector2 pos = new(sw * 0.5f - sz.X * 0.5f, sh * 0.785f);
            Main.spriteBatch.DrawString(bodyFont, full, pos + new Vector2(1f, 1f), Color.Black * 0.55f);
            Main.spriteBatch.DrawString(bodyFont, full, pos, dim * 0.85f);

            //底部铭牌
            Main.spriteBatch.DrawString(bodyFont, "SIGNAL  ANCHOR",
                new Vector2(sw * 0.034f, sh * 0.892f), dim * 0.55f);
        }
    }
}
