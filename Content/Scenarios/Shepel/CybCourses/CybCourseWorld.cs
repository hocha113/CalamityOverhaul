using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using SubworldLibrary;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Scenarios.Shepel.CybCourses
{
    internal class CybCourseWorld : Subworld
    {
        //世界宽高保持极小，够用就行，不浪费内存
        public override int Width => 400;
        public override int Height => 250;

        public static bool Active => SubworldSystem.IsActive<CybCourseWorld>();

        public override List<GenPass> Tasks => [new CybCourseGen()];

        public static void Enter() => SubworldSystem.Enter<CybCourseWorld>();
        public static void Exit() => SubworldSystem.Exit();

        //加载动画累计时间，每次进入子世界时重置
        private static float _loadTime = 0f;
        //预估加载时长（秒），进度条基于此动画，超出后钉在95%
        private const float _estDuration = 5.5f;

        //入场揭示动画累计时间。-1 = 等待真实进入（OnEnter 后、首帧 Update 前）
        //0..EntryRevealDuration = 进行中；>= EntryRevealDuration = 演出结束
        private static float _entryRevealTime = -1f;
        //入场演出阶段时长（秒）
        private const float EntryHoldDuration = 0.18f;     //开场短暂停留（暗场，蓄势）
        private const float EntryExpandDuration = 1.95f;   //波前从中心扩散到屏幕外
        private const float EntryFadeDuration = 0.55f;     //尾声整体淡出
        private const float EntryRevealDuration =
            EntryHoldDuration + EntryExpandDuration + EntryFadeDuration;

        public static bool EntryRevealActive =>
            _entryRevealTime >= 0f && _entryRevealTime < EntryRevealDuration;

        public override void OnEnter() {
            _loadTime = 0f;
            //置为哨兵值，等首帧 Update 真正进入游戏时再启动
            _entryRevealTime = -1f;
        }

        public override void OnExit() {
            //离开教程世界时清理兜底状态，避免被带回主世界造成异常
            HackTime.InfiniteHack = false;
            //避免下次进入时误用旧值
            _entryRevealTime = -1f;
        }

        public override void OnLoad() {
            //固定为永夜，强化赛博朋克氛围
            Main.dayTime = false;
            Main.time = 0;
            //把地表线和岩层线推到世界底部，令游戏认为整个世界处于地面以上
            //这样环境光照正常工作，不会出现地下黑暗
            Main.worldSurface = Height - 5;
            Main.rockLayer = Height - 4;
        }

        public override void Update() {
            //子世界 Update 仅在真正游戏帧调用（加载阶段不调用），适合作为入场演出的起点
            if (_entryRevealTime < 0f) {
                _entryRevealTime = 0f;
            }
            else if (_entryRevealTime < EntryRevealDuration) {
                //Terraria 子世界帧率固定 60Hz
                _entryRevealTime += 1f / 60f;
            }
            //清理掉落物
            for (int i = 0; i < Main.maxItems; i++) {
                Item item = Main.item[i];
                if (item.Alives() && item.CWR().InventoryTimer == 0) {
                    item.TurnToAir();
                }
            }
        }

        //完全接管加载界面的绘制逻辑
        public override void DrawSetup(GameTime gameTime) {
            //限制单帧增量避免首帧跳变
            _loadTime += 0.02f;

            PlayerInput.SetZoom_UI();
            Main.instance.GraphicsDevice.Clear(Color.Black);

            //先绘制着色器背景（单独的SpriteBatch，使用Immediate模式应用shader）
            DrawLoadingBackground(_loadTime);

            //文字层沿用base结构（Deferred + UIScaleMatrix）
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, Main.Rasterizer,
                null, Main.UIScaleMatrix);
            DrawMenu(gameTime);
            Main.DrawCursor(Main.DrawThickCursor());
            Main.spriteBatch.End();
        }

        //入场演出：进入子世界后第一秒到第二秒之间，从屏幕中心扩散一圈六角能量网格揭示真实场景
        //由 CybCourseEntryRevealLayer 在最高层级调用，盖住一切常规UI；演出结束后自动停止
        internal static void DrawEntryRevealOverlay(SpriteBatch sb) {
            if (!EntryRevealActive) {
                return;
            }
            var shader = EffectLoader.CybCourseEntryReveal?.Value;
            if (shader == null || VaultAsset.placeholder2 == null || VaultAsset.placeholder2.IsDisposed) {
                return;
            }

            float t = _entryRevealTime;
            //reveal 三段：[0,Hold]=0；[Hold,Hold+Expand] 0→1；[Hold+Expand,total] 1→1.18
            float reveal;
            if (t < EntryHoldDuration) {
                reveal = 0f;
            }
            else if (t < EntryHoldDuration + EntryExpandDuration) {
                float u = (t - EntryHoldDuration) / EntryExpandDuration;
                //先慢起步，过中段后稍快推进，再略微减速；让能量波前充满"展开"的仪式感
                reveal = MathHelper.SmoothStep(0f, 1f, u);
            }
            else {
                float u = (t - EntryHoldDuration - EntryExpandDuration) / EntryFadeDuration;
                reveal = 1f + MathHelper.Clamp(u, 0f, 1f) * 0.18f;
            }

            int w = Main.screenWidth;
            int h = Main.screenHeight;

            //接管 SpriteBatch，使用 Immediate 模式以应用 shader
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
            //还原至默认 Deferred 状态，匹配 Terraria 界面层调用约定
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.UIScaleMatrix);
        }

        //绘制全屏着色器背景（在DrawSetup内、文字层之前调用）
        private static void DrawLoadingBackground(float time) {
            var shader = EffectLoader.CybCourseLoading?.Value;
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

        //加载 UI 五块：识别码、标题、雷达百分比、状态行、进度条
        public override void DrawMenu(GameTime gameTime) {
            int sw = Main.screenWidth;
            int sh = Main.screenHeight;
            float progress = MathHelper.SmoothStep(0f, 1f, MathHelper.Clamp(_loadTime / _estDuration, 0f, 0.95f));

            Color gold = new Color(245, 197, 24);
            Color warm = new Color(255, 218, 130);
            Color dim = new Color(170, 185, 200);

            DynamicSpriteFont titleFont = FontAssets.DeathText.Value;
            DynamicSpriteFont bodyFont = FontAssets.MouseText.Value;
            Texture2D px = VaultAsset.placeholder2.Value;

            DrawTopIdentifier(sw, sh, bodyFont, dim);
            DrawTitleBlock(sw, sh, titleFont, bodyFont, gold, dim, px);
            DrawDialPercentage(sw, sh, titleFont, bodyFont, gold, warm, progress);
            DrawStatus(sw, sh, bodyFont, dim);
            DrawBarLabel(sw, sh, bodyFont, dim);
        }

        //顶部识别码（极淡，纯文字，不闪烁不动）
        private static void DrawTopIdentifier(int sw, int sh, DynamicSpriteFont font, Color dim) {
            string tag = "// CYBERSPACE  NODE-4082E";
            Main.spriteBatch.DrawString(font, tag,
                new Vector2(sw * 0.034f, sh * 0.090f), dim * 0.50f);
        }

        //中央标题：主标题（DeathText）+ 副标题 + 下划线锚点
        private static void DrawTitleBlock(int sw, int sh, DynamicSpriteFont titleFont,
            DynamicSpriteFont bodyFont, Color gold, Color dim, Texture2D px) {
            string title = "ENGRAM  LINK";
            Vector2 titleSz = titleFont.MeasureString(title);
            Vector2 titlePos = new Vector2(sw * 0.5f - titleSz.X * 0.5f, sh * 0.180f);

            //仅一层投影（避免色差/抖动等故障特效，保持高质感）
            Main.spriteBatch.DrawString(titleFont, title,
                titlePos + new Vector2(2f, 3f), Color.Black * 0.55f);
            Main.spriteBatch.DrawString(titleFont, title,
                titlePos, gold);

            string sub = "SUPERDREAM   PROTOCOL";
            Vector2 subSz = bodyFont.MeasureString(sub);
            Vector2 subPos = new Vector2(sw * 0.5f - subSz.X * 0.5f,
                                         titlePos.Y + titleSz.Y + 8f);
            Main.spriteBatch.DrawString(bodyFont, sub,
                subPos, dim * 0.65f);

            //下划线锚点：一条克制的金色细线 + 中央菱形（衔接副标题与下方雷达盘）
            int ulY = (int)(subPos.Y + subSz.Y + 14f);
            int ulW = (int)(titleSz.X * 0.55f);
            int ulX = (int)(sw * 0.5f - ulW / 2f);
            //左右两段，中央留出菱形位置
            int gap = 6;
            Main.spriteBatch.Draw(px, new Rectangle(ulX, ulY, ulW / 2 - gap, 1),
                new Rectangle(0, 0, 1, 1), gold * 0.55f);
            Main.spriteBatch.Draw(px, new Rectangle(ulX + ulW / 2 + gap, ulY, ulW / 2 - gap, 1),
                new Rectangle(0, 0, 1, 1), gold * 0.55f);

            //中心菱形（用三段水平像素拼接近似）
            int cx = ulX + ulW / 2;
            Main.spriteBatch.Draw(px, new Rectangle(cx - 1, ulY - 2, 2, 1),
                new Rectangle(0, 0, 1, 1), gold * 0.95f);
            Main.spriteBatch.Draw(px, new Rectangle(cx - 2, ulY - 1, 4, 1),
                new Rectangle(0, 0, 1, 1), gold);
            Main.spriteBatch.Draw(px, new Rectangle(cx - 3, ulY, 6, 1),
                new Rectangle(0, 0, 1, 1), gold);
            Main.spriteBatch.Draw(px, new Rectangle(cx - 2, ulY + 1, 4, 1),
                new Rectangle(0, 0, 1, 1), gold);
            Main.spriteBatch.Draw(px, new Rectangle(cx - 1, ulY + 2, 2, 1),
                new Rectangle(0, 0, 1, 1), gold * 0.95f);
        }

        //雷达盘心：放大百分比数字（焦点）+ 小号"%"符号
        //数字使用DeathText（与标题同源）以保证排版一致；scale<1降至适合内环框尺寸
        private static void DrawDialPercentage(int sw, int sh, DynamicSpriteFont titleFont,
            DynamicSpriteFont bodyFont, Color gold, Color warm, float progress) {
            int pct = (int)(progress * 100);
            string num = pct.ToString("D2");
            Vector2 numSz = titleFont.MeasureString(num);
            Vector2 scale = new Vector2(0.92f);
            Vector2 numActSz = new Vector2(numSz.X * scale.X, numSz.Y * scale.Y);

            Vector2 dialCenter = new Vector2(sw * 0.5f, sh * 0.510f);
            //数字基点：相对中心居中，整体略向左偏移以为右上方"%"符号留位
            float signOffset = 6f;
            Vector2 numPos = new Vector2(
                dialCenter.X - (numActSz.X + signOffset + 14f) * 0.5f,
                dialCenter.Y - numActSz.Y * 0.5f);

            //投影
            Main.spriteBatch.DrawString(titleFont, num,
                numPos + new Vector2(2f, 3f), Color.Black * 0.55f,
                0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            //主体
            Main.spriteBatch.DrawString(titleFont, num,
                numPos, gold,
                0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

            //"%"符号：小号，置于数字右上角，颜色稍暖
            string sign = "%";
            Vector2 signSz = bodyFont.MeasureString(sign);
            Vector2 signPos = new Vector2(
                numPos.X + numActSz.X + signOffset,
                numPos.Y + numActSz.Y - signSz.Y - 4f);
            Main.spriteBatch.DrawString(bodyFont, sign,
                signPos, warm * 0.92f);
        }

        //状态行：当前WorldGen进度文本 + 动画省略号；克制的描边阴影
        private static void DrawStatus(int sw, int sh, DynamicSpriteFont font, Color dim) {
            string status = (Main.statusText ?? string.Empty).ToUpperInvariant();
            if (string.IsNullOrEmpty(status)) {
                status = "ESTABLISHING NEURAL HANDSHAKE";
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

        //底部进度条左侧标签：极简，对应着色器进度条y≈0.928
        private static void DrawBarLabel(int sw, int sh, DynamicSpriteFont font, Color dim) {
            string label = "NEURAL  BRIDGE";
            Main.spriteBatch.DrawString(font, label,
                new Vector2(sw * 0.034f, sh * 0.892f), dim * 0.55f);
        }
    }
}
