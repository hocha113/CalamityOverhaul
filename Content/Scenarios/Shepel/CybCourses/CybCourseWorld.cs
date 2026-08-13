using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using SubworldLibrary;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Scenarios.Shepel.CybCourses
{
    internal class CybCourseWorld : Subworld, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Shepel";

        //加载屏文案（懒初始化：Subworld 无 SetStaticDefaults 钩）
        private LocalizedText _bootTag;
        private LocalizedText _bootTitle;
        private LocalizedText _bootSubtitle;
        private LocalizedText _bootStatus;
        private LocalizedText _bootBarLabel;

        private void EnsureBootTexts() {
            _bootTag ??= this.GetLocalization("BootTag", () => "神经直连 · 超梦节点");
            _bootTitle ??= this.GetLocalization("BootTitle", () => "接入超梦");
            _bootSubtitle ??= this.GetLocalization("BootSubtitle", () => "SHPC 沉浸训练协议");
            _bootStatus ??= this.GetLocalization("BootStatus", () => "正在编译训练空间");
            _bootBarLabel ??= this.GetLocalization("BootBarLabel", () => "同步率");
        }

        //宽度400够用
        public override int Width => 400;
        //高度须让地狱层(maxY-200)落甲板下
        //太矮则甲板判地狱,误触发探索/赠礼/巫毒
        //FloorY170+280=450,地狱层250,余量≈72
        public override int Height => CybCourseGen.FloorY + 280;

        public static bool Active => SubworldSystem.IsActive<CybCourseWorld>();

        public override List<GenPass> Tasks => [new CybCourseGen()];

        public static void Enter() => SubworldSystem.Enter<CybCourseWorld>();
        public static void Exit() => SubworldSystem.Exit();

        //加载计时,入子世界重置
        private static float _loadTime = 0f;
        //估时5.5s,超95%钉住
        private const float _estDuration = 5.5f;

        //入场揭示,-1=等OnEnter后首帧Update
        //0..Duration进行中,>=Duration结束
        private static float _entryRevealTime = -1f;
        private const float EntryHoldDuration = 0.18f;     //暗场蓄势
        private const float EntryExpandDuration = 1.95f;   //波前扩散
        private const float EntryFadeDuration = 0.55f;     //整体淡出
        private const float EntryRevealDuration =
            EntryHoldDuration + EntryExpandDuration + EntryFadeDuration;

        public static bool EntryRevealActive =>
            _entryRevealTime >= 0f && _entryRevealTime < EntryRevealDuration;

        public override void OnEnter() {
            _loadTime = 0f;
            //-1哨兵,首帧Update再启
            _entryRevealTime = -1f;
        }

        public override void OnExit() {
            //离世界清兜底态
            HackTime.InfiniteHack = false;
            _entryRevealTime = -1f;
            //清快照释内存
            CybCourseGen.ClearSnapshot();
        }

        public override void OnLoad() {
            HackTime.InfiniteHack = true;
            Main.dayTime = false;
            Main.time = 0;
            //worldSurface/rockLayer放甲板下,避地下/地狱/天空
            //FloorY+30=200地表线,甲板146-160在其上
            //maxY-200=250地狱层,同样在甲板下
            Main.worldSurface = CybCourseGen.FloorY + 30;
            Main.rockLayer = CybCourseGen.FloorY + 55;
        }

        public override void Update() {
            //Update仅游戏帧,适合作入场起点
            if (_entryRevealTime < 0f) {
                _entryRevealTime = 0f;
            }
            else if (_entryRevealTime < EntryRevealDuration) {
                //子世界固定60Hz
                _entryRevealTime += 1f / 60f;
            }
            for (int i = 0; i < Main.maxItems; i++) {
                Item item = Main.item[i];
                if (item.Alives() && item.CWR().InventoryTimer == 0) {
                    item.TurnToAir();
                }
            }
        }

        public override void DrawSetup(GameTime gameTime) {
            //限单帧增量避跳变
            _loadTime += 0.02f;

            PlayerInput.SetZoom_UI();
            Main.instance.GraphicsDevice.Clear(Color.Black);

            //shader背景,Immediate batch
            DrawLoadingBackground(_loadTime);

            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, Main.Rasterizer,
                null, Main.UIScaleMatrix);
            DrawMenu(gameTime);
            Main.DrawCursor(Main.DrawThickCursor());
            Main.spriteBatch.End();
        }

        //入场揭示,EntryRevealLayer最高层
        internal static void DrawEntryRevealOverlay(SpriteBatch sb) {
            if (!EntryRevealActive) {
                return;
            }
            var shader = EffectLoader.CybCourseEntryReveal?.Value;
            if (shader == null || VaultAsset.placeholder2 == null || VaultAsset.placeholder2.IsDisposed) {
                return;
            }

            float t = _entryRevealTime;
            //reveal三段,Hold/Expand/Fade
            float reveal;
            if (t < EntryHoldDuration) {
                reveal = 0f;
            }
            else if (t < EntryHoldDuration + EntryExpandDuration) {
                float u = (t - EntryHoldDuration) / EntryExpandDuration;
                reveal = MathHelper.SmoothStep(0f, 1f, u);
            }
            else {
                float u = (t - EntryHoldDuration - EntryExpandDuration) / EntryFadeDuration;
                reveal = 1f + MathHelper.Clamp(u, 0f, 1f) * 0.18f;
            }

            int w = Main.screenWidth;
            int h = Main.screenHeight;

            //Immediate应用shader
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.UIScaleMatrix);

            shader.Parameters["uTime"]?.SetValue(t);
            shader.Parameters["uReveal"]?.SetValue(reveal);
            shader.Parameters["uAspectRatio"]?.SetValue((float)w / h);
            shader.CurrentTechnique.Passes[0].Apply();

            sb.Draw(VaultAsset.placeholder2.Value, new Rectangle(0, 0, w, h), Color.White);

            sb.End();
            //还原Deferred batch
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.UIScaleMatrix);
        }

        private static void DrawLoadingBackground(float time) {
            //超梦专用 Boot 版；金色 CybCourseLoading 保留给旧网加载屏
            var shader = EffectLoader.CybCourseBoot?.Value;
            if (shader == null || VaultAsset.placeholder2 == null || VaultAsset.placeholder2.IsDisposed) {
                return;
            }
            int w = Main.screenWidth;
            int h = Main.screenHeight;
            float progress = MathHelper.SmoothStep(0f, 1f, MathHelper.Clamp(_loadTime / _estDuration, 0f, 0.95f));

            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, Main.Rasterizer,
                null, Main.UIScaleMatrix);

            shader.Parameters["uTime"]?.SetValue(time);
            shader.Parameters["uProgress"]?.SetValue(progress);
            shader.Parameters["uAspectRatio"]?.SetValue((float)w / h);
            shader.CurrentTechnique.Passes[0].Apply();

            Main.spriteBatch.Draw(VaultAsset.placeholder2.Value, new Rectangle(0, 0, w, h), Color.White);
            Main.spriteBatch.End();
        }

        public override void DrawMenu(GameTime gameTime) {
            EnsureBootTexts();
            int sw = Main.screenWidth;
            int sh = Main.screenHeight;
            float progress = MathHelper.SmoothStep(0f, 1f, MathHelper.Clamp(_loadTime / _estDuration, 0f, 0.95f));

            //SHPC 色板：青为体，琥珀只作强调
            Color cyan = new Color(120, 230, 250);
            Color amber = new Color(255, 170, 60);
            Color dim = new Color(150, 185, 200);

            DynamicSpriteFont titleFont = FontAssets.DeathText.Value;
            DynamicSpriteFont bodyFont = FontAssets.MouseText.Value;
            Texture2D px = VaultAsset.placeholder2.Value;

            DrawTopIdentifier(sw, sh, bodyFont, dim, _bootTag.Value);
            DrawTitleBlock(sw, sh, titleFont, bodyFont, cyan, dim, px, _bootTitle.Value, _bootSubtitle.Value);
            DrawDialPercentage(sw, sh, titleFont, bodyFont, cyan, amber, progress);
            DrawStatus(sw, sh, bodyFont, dim, _bootStatus.Value);
            DrawBarLabel(sw, sh, bodyFont, dim, _bootBarLabel.Value);
        }

        private static void DrawTopIdentifier(int sw, int sh, DynamicSpriteFont font, Color dim, string tag) {
            Main.spriteBatch.DrawString(font, tag,
                new Vector2(sw * 0.034f, sh * 0.090f), dim * 0.50f);
        }

        private static void DrawTitleBlock(int sw, int sh, DynamicSpriteFont titleFont,
            DynamicSpriteFont bodyFont, Color cyan, Color dim, Texture2D px, string title, string sub) {
            Vector2 titleSz = titleFont.MeasureString(title);
            Vector2 titlePos = new Vector2(sw * 0.5f - titleSz.X * 0.5f, sh * 0.180f);

            Main.spriteBatch.DrawString(titleFont, title,
                titlePos + new Vector2(2f, 3f), Color.Black * 0.55f);
            Main.spriteBatch.DrawString(titleFont, title,
                titlePos, cyan);

            Vector2 subSz = bodyFont.MeasureString(sub);
            Vector2 subPos = new Vector2(sw * 0.5f - subSz.X * 0.5f,
                                         titlePos.Y + titleSz.Y + 8f);
            Main.spriteBatch.DrawString(bodyFont, sub,
                subPos, dim * 0.65f);

            int ulY = (int)(subPos.Y + subSz.Y + 14f);
            int ulW = (int)(titleSz.X * 0.55f);
            int ulX = (int)(sw * 0.5f - ulW / 2f);
            int gap = 6;
            Main.spriteBatch.Draw(px, new Rectangle(ulX, ulY, ulW / 2 - gap, 1),
                new Rectangle(0, 0, 1, 1), cyan * 0.55f);
            Main.spriteBatch.Draw(px, new Rectangle(ulX + ulW / 2 + gap, ulY, ulW / 2 - gap, 1),
                new Rectangle(0, 0, 1, 1), cyan * 0.55f);

            int cx = ulX + ulW / 2;
            Main.spriteBatch.Draw(px, new Rectangle(cx - 1, ulY - 2, 2, 1),
                new Rectangle(0, 0, 1, 1), cyan * 0.95f);
            Main.spriteBatch.Draw(px, new Rectangle(cx - 2, ulY - 1, 4, 1),
                new Rectangle(0, 0, 1, 1), cyan);
            Main.spriteBatch.Draw(px, new Rectangle(cx - 3, ulY, 6, 1),
                new Rectangle(0, 0, 1, 1), cyan);
            Main.spriteBatch.Draw(px, new Rectangle(cx - 2, ulY + 1, 4, 1),
                new Rectangle(0, 0, 1, 1), cyan);
            Main.spriteBatch.Draw(px, new Rectangle(cx - 1, ulY + 2, 2, 1),
                new Rectangle(0, 0, 1, 1), cyan * 0.95f);
        }

        private static void DrawDialPercentage(int sw, int sh, DynamicSpriteFont titleFont,
            DynamicSpriteFont bodyFont, Color cyan, Color amber, float progress) {
            int pct = (int)(progress * 100);
            string num = pct.ToString("D2");
            Vector2 numSz = titleFont.MeasureString(num);
            Vector2 scale = new Vector2(0.92f);
            Vector2 numActSz = new Vector2(numSz.X * scale.X, numSz.Y * scale.Y);

            Vector2 dialCenter = new Vector2(sw * 0.5f, sh * 0.510f);
            float signOffset = 6f;
            Vector2 numPos = new Vector2(
                dialCenter.X - (numActSz.X + signOffset + 14f) * 0.5f,
                dialCenter.Y - numActSz.Y * 0.5f);

            Main.spriteBatch.DrawString(titleFont, num,
                numPos + new Vector2(2f, 3f), Color.Black * 0.55f,
                0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            Main.spriteBatch.DrawString(titleFont, num,
                numPos, cyan,
                0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

            string sign = "%";
            Vector2 signSz = bodyFont.MeasureString(sign);
            Vector2 signPos = new Vector2(
                numPos.X + numActSz.X + signOffset,
                numPos.Y + numActSz.Y - signSz.Y - 4f);
            Main.spriteBatch.DrawString(bodyFont, sign,
                signPos, amber * 0.92f);
        }

        private static void DrawStatus(int sw, int sh, DynamicSpriteFont font, Color dim, string fallback) {
            string status = Main.statusText ?? string.Empty;
            if (string.IsNullOrEmpty(status)) {
                status = fallback;
            }
            int dotN = (int)(_loadTime * 1.7f) % 4;
            string full = status + new string('.', dotN);

            Vector2 sz = font.MeasureString(full);
            Vector2 pos = new Vector2(sw * 0.5f - sz.X * 0.5f, sh * 0.785f);
            Main.spriteBatch.DrawString(font, full,
                pos + new Vector2(1f, 1f), Color.Black * 0.55f);
            Main.spriteBatch.DrawString(font, full,
                pos, dim * 0.85f);
        }

        private static void DrawBarLabel(int sw, int sh, DynamicSpriteFont font, Color dim, string label) {
            Main.spriteBatch.DrawString(font, label,
                new Vector2(sw * 0.034f, sh * 0.892f), dim * 0.55f);
        }
    }
}
