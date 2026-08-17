using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Scenarios.Kiyume.UI
{
    /// <summary>
    /// 鬼梦加载屏：红黑穹顶下的湖畔村剪影，雾海随进度涨上来把村子淹掉。<br/>
    /// 纯 CPU 绘制零 shader 依赖（加载期 shader 资产未必就绪）；
    /// 接线走 A 路薄转发（KiyumeWorld 内各一行）
    /// </summary>
    internal static class KiyumeLoadingScreen
    {
        private static readonly Rectangle PixelSrc = new(0, 0, 1, 1);

        //鬼梦色板，与 KikasaDreamSky.fx 同源
        private static readonly Color SkyTop = new(13, 2, 3);
        private static readonly Color SkyMid = new(69, 11, 10);
        private static readonly Color Horizon = new(143, 30, 14);
        private static readonly Color SilFar = new(23, 6, 7);
        private static readonly Color SilNear = new(8, 2, 3);
        private static readonly Color Ember = new(242, 87, 36);
        private static readonly Color FogBody = new(46, 13, 14);
        private static readonly Color FogRim = new(126, 40, 36);
        private static readonly Color TextDim = new(168, 132, 128);

        private static float loadTime;
        private static bool entering = true;
        //估时钉住 95%，实际完成由 SubLib 切场景
        private const float EstDuration = 6f;

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
            //加载期 ModSystem.Update 不跑，SLib 传进来的 GameTime 增量不可信，钉死单帧步进
            loadTime += 0.02f;

            Rectangle rectangle = Main.instance.GraphicsDevice.ScissorRectangle;

            PlayerInput.SetZoom_UI();
            Main.instance.GraphicsDevice.Clear(Color.Black);

            SpriteBatch sb = Main.spriteBatch;

            //景：原始屏幕空间，铺满的画不该跟着 UI 缩放跑偏
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone);
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            DrawScene(sb, rectangle.Width, rectangle.Height);
            sb.End();

            //字：跟随 UI 缩放，坐标换算到逻辑尺寸免得高缩放下偏出屏
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, Main.Rasterizer,
                null, Main.UIScaleMatrix);
            float scale = MathHelper.Max(Main.UIScale, 0.05f);
            DrawMenu(sb, rectangle.Width / scale, rectangle.Height / scale);
            Main.DrawCursor(Main.DrawThickCursor());
            sb.End();
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

        //====== 景 ======

        private static void DrawScene(SpriteBatch sb, int w, int h) {
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null || px.IsDisposed) {
                return;
            }
            float horizonY = h * 0.615f;

            DrawDome(sb, px, w, h, horizonY);
            //远排：小而密，雾里只剩个大概；近排：大而疏，黑得实
            DrawVillageRow(sb, px, w, horizonY + h * 0.012f, h * 0.052f, 3, seed: 37, SilFar, 0.62f);
            DrawVillageRow(sb, px, w, horizonY + h * 0.062f, h * 0.092f, 7, seed: 911, SilNear, 1f);
            DrawEmberMotes(sb, px, w, horizonY);
            DrawFogSea(sb, px, w, h, horizonY);
        }

        //穹顶：红黑竖向层次 + 地平一线烬光
        private static void DrawDome(SpriteBatch sb, Texture2D px, int w, int h, float horizonY) {
            const int bands = 48;
            int bandH = h / bands + 1;
            for (int i = 0; i < bands; i++) {
                float k = i / (float)(bands - 1);
                Color c = Color.Lerp(SkyTop, SkyMid, MathHelper.SmoothStep(0f, 1f, k));
                //地平附近烘一层烬光，越靠近越暖
                float glow = MathF.Exp(-MathF.Abs(k * h - horizonY) / (h * 0.085f));
                c = Color.Lerp(c, Horizon, glow * 0.55f);
                sb.Draw(px, new Rectangle(0, i * (h / bands), w, bandH), PixelSrc, c);
            }

            //缓涌暗云：两层反向漂移的横带，压在地平之上
            float t = loadTime;
            for (int i = 0; i < 7; i++) {
                float phase = i * 1.37f;
                float dir = (i & 1) == 0 ? 1f : -1f;
                float y = horizonY - h * (0.06f + i * 0.055f);
                float drift = (t * 9f * dir + phase * 260f) % (w + 640f) - 320f;
                int cw = (int)(w * (0.32f + Hash(i, 3) * 0.4f));
                int ch = (int)(h * (0.012f + Hash(i, 11) * 0.016f));
                sb.Draw(px, new Rectangle((int)drift, (int)y, cw, ch), PixelSrc,
                    SkyTop * (0.42f + Hash(i, 7) * 0.24f));
            }
        }

        //一排村落：民居/望楼/枯树错落，三成人家亮窗火
        private static void DrawVillageRow(SpriteBatch sb, Texture2D px, int w, float baseY,
            float unit, int cellDiv, int seed, Color sil, float lightGate) {
            int cell = Math.Max(w / (cellDiv * 6), 18);
            int count = w / cell + 2;
            for (int i = -1; i < count; i++) {
                float h1 = Hash(i, seed);
                float h2 = Hash(i, seed + 7);
                float h3 = Hash(i, seed + 13);
                float roll = Hash(i, seed + 23);
                //地面起伏：房子平放在格心地面上，不跟着坡歪
                int gy = (int)(baseY + (Hash(i, seed + 31) - 0.5f) * unit * 0.42f);
                int cx = i * cell + (int)(h3 * cell * 0.5f);

                if (roll < 0.16f) {
                    continue;
                }
                if (roll < 0.32f) {
                    DrawDeadTree(sb, px, cx, gy, unit * (0.62f + h1 * 0.5f), seed + i, sil);
                }
                else if (roll < 0.44f) {
                    DrawHut(sb, px, cx, gy, (int)(unit * (0.30f + h2 * 0.14f)),
                        (int)(unit * (1.25f + h1 * 0.75f)), (int)(unit * 0.52f), sil,
                        lightGate * 0.9f, topWindow: true);
                }
                else {
                    DrawHut(sb, px, cx, gy, (int)(unit * (0.72f + h2 * 0.62f)),
                        (int)(unit * (0.52f + h1 * 0.44f)), (int)(unit * 0.32f), sil,
                        h1 > 0.66f ? lightGate : 0f, topWindow: false);
                }
            }
        }

        //民居/望楼：墙体 + 出檐坡脊（逐层收窄的横条堆出脊线）+ 窗火
        private static void DrawHut(SpriteBatch sb, Texture2D px, int cx, int groundY,
            int wid, int hgt, int roofH, Color sil, float lightGate, bool topWindow) {
            wid = Math.Max(wid, 3);
            hgt = Math.Max(hgt, 4);
            roofH = Math.Max(roofH, 2);
            int bodyTop = groundY - hgt;
            sb.Draw(px, new Rectangle(cx - wid / 2, bodyTop, wid, hgt), PixelSrc, sil);

            int eave = Math.Max(wid / 4, 2);
            for (int i = 0; i < roofH; i++) {
                float k = i / (float)roofH;
                //脊线 pow 曲线下垂：檐口外挑、脊头收细
                int rw = (int)MathHelper.Lerp(wid + eave * 2, wid * 0.22f, MathF.Pow(k, 0.72f));
                sb.Draw(px, new Rectangle(cx - rw / 2, bodyTop - i - 1, Math.Max(rw, 1), 1), PixelSrc, sil);
            }

            if (lightGate <= 0.01f) {
                return;
            }
            //窗火：忽明忽暗，是屋里还有人
            float flicker = 0.45f + 0.55f * (0.5f + 0.5f * MathF.Sin(loadTime * 2.3f + cx * 0.07f));
            int ww = Math.Max(wid / 5, 2);
            int wy = topWindow ? bodyTop + roofH / 2 : bodyTop + hgt / 3;
            sb.Draw(px, new Rectangle(cx - ww / 2, wy, ww, Math.Max(ww * 3 / 4, 2)), PixelSrc,
                Ember * (flicker * lightGate));
        }

        //枯树：细干 + 被啃过的双团冠（小矩形簇出毛边，别是个圆疙瘩）
        private static void DrawDeadTree(SpriteBatch sb, Texture2D px, int cx, int groundY,
            float size, int seed, Color sil) {
            int trunkH = (int)MathHelper.Max(size, 5f);
            sb.Draw(px, new Rectangle(cx - 1, groundY - trunkH, 2, trunkH), PixelSrc, sil);
            int crownY = groundY - trunkH;
            for (int i = 0; i < 9; i++) {
                float a = Hash(seed, i * 3 + 1) * MathHelper.TwoPi;
                float r = size * (0.14f + Hash(seed, i * 3 + 2) * 0.34f);
                int bw = (int)MathHelper.Max(size * (0.10f + Hash(seed, i * 3) * 0.16f), 2f);
                sb.Draw(px, new Rectangle(
                    cx + (int)(MathF.Cos(a) * r) - bw / 2,
                    crownY + (int)(MathF.Sin(a) * r * 0.72f) - bw / 2,
                    bw, bw), PixelSrc, sil);
            }
        }

        //远场烬点：稀疏红星缓缓上浮
        private static void DrawEmberMotes(SpriteBatch sb, Texture2D px, int w, float horizonY) {
            for (int i = 0; i < 34; i++) {
                float speed = 12f + Hash(i, 5) * 26f;
                float y = horizonY - (loadTime * speed + Hash(i, 9) * 900f) % (horizonY + 120f);
                float x = (Hash(i, 17) * w + MathF.Sin(loadTime * 0.5f + i) * 14f) % w;
                float fade = MathHelper.Clamp(y / horizonY, 0f, 1f);
                int s = Hash(i, 21) > 0.78f ? 3 : 2;
                sb.Draw(px, new Rectangle((int)x, (int)y, s, s), PixelSrc,
                    Ember * (0.30f * fade * (0.5f + 0.5f * MathF.Sin(loadTime * 3f + i))));
            }
        }

        //雾海：随进度涨上来，最后把村子淹到只剩屋顶。表面带行波，这才是"有水位的雾"
        private static void DrawFogSea(SpriteBatch sb, Texture2D px, int w, int h, float horizonY) {
            float rise = MathHelper.SmoothStep(0f, 1f, Progress / 0.95f);
            float surfaceY = MathHelper.Lerp(h * 1.04f, horizonY + h * 0.028f, rise);
            if (surfaceY >= h) {
                return;
            }

            //本体：越深越浓的横带
            const int bands = 26;
            float span = h - surfaceY;
            for (int i = 0; i < bands; i++) {
                float k = i / (float)(bands - 1);
                int y = (int)(surfaceY + span * k);
                int bh = (int)(span / bands) + 2;
                sb.Draw(px, new Rectangle(0, y, w, bh), PixelSrc,
                    FogBody * MathHelper.Lerp(0.55f, 0.97f, k));
            }

            //雾面：三道不同波长的行波叠出起伏，面上压一条亮边
            int step = 4;
            for (int x = 0; x < w; x += step) {
                float wave = MathF.Sin(x * 0.0132f + loadTime * 1.15f) * (h * 0.006f)
                    + MathF.Sin(x * 0.0041f - loadTime * 0.62f) * (h * 0.011f)
                    + MathF.Sin(x * 0.0009f + loadTime * 0.24f) * (h * 0.016f);
                int y = (int)(surfaceY + wave);
                //面下近表层：亮一档，让"这是一层液面"读得出来
                sb.Draw(px, new Rectangle(x, y, step, (int)(h * 0.024f)), PixelSrc, FogRim * 0.30f);
                sb.Draw(px, new Rectangle(x, y - 1, step, 2), PixelSrc, FogRim * 0.72f);
            }
        }

        //====== 字 ======

        private static void DrawMenu(SpriteBatch sb, float sw, float sh) {
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null || px.IsDisposed) {
                return;
            }
            float progress = Progress;
            DynamicSpriteFont titleFont = FontAssets.DeathText.Value;
            DynamicSpriteFont bodyFont = FontAssets.MouseText.Value;

            sb.DrawString(bodyFont, entering ? "// KIYUME  LAKESHORE DREAM" : "// KIYUME  WAKING",
                new Vector2(sw * 0.034f, sh * 0.088f), TextDim * 0.5f);

            string title = entering ? "INTO THE DREAM" : "WAKING";
            Vector2 titleSz = titleFont.MeasureString(title);
            Vector2 titlePos = new(sw * 0.5f - titleSz.X * 0.5f, sh * 0.155f);
            sb.DrawString(titleFont, title, titlePos + new Vector2(2f, 3f), Color.Black * 0.6f);
            sb.DrawString(titleFont, title, titlePos, entering ? Horizon : new Color(226, 208, 200));

            string sub = "THE LAKE REMEMBERS";
            Vector2 subSz = bodyFont.MeasureString(sub);
            Vector2 subPos = new(sw * 0.5f - subSz.X * 0.5f, titlePos.Y + titleSz.Y + 6f);
            sb.DrawString(bodyFont, sub, subPos, TextDim * 0.62f);

            int ulW = (int)(titleSz.X * 0.55f);
            sb.Draw(px, new Rectangle((int)(sw * 0.5f - ulW / 2f), (int)(subPos.Y + subSz.Y + 12f), ulW, 1),
                PixelSrc, Ember * 0.5f);

            //进度：涨水刻度式，数字压在雾面上方
            string num = ((int)(progress * 100)).ToString("D2");
            Vector2 numSz = titleFont.MeasureString(num);
            Vector2 numScale = new(0.9f);
            Vector2 numPos = new(sw * 0.5f - numSz.X * numScale.X * 0.5f, sh * 0.44f - numSz.Y * numScale.Y * 0.5f);
            sb.DrawString(titleFont, num, numPos + new Vector2(2f, 3f), Color.Black * 0.6f,
                0f, Vector2.Zero, numScale, SpriteEffects.None, 0f);
            sb.DrawString(titleFont, num, numPos, Ember * 0.92f,
                0f, Vector2.Zero, numScale, SpriteEffects.None, 0f);

            //优先读 gen pass 文案。SubLib 不写 Main.statusText，只读它会一直停在「正在清除地图数据」
            string status = WorldGenerator.CurrentGenerationProgress?.Message;
            if (string.IsNullOrEmpty(status)) {
                status = Main.statusText;
            }
            if (string.IsNullOrEmpty(status)) {
                status = entering ? "THE FOG IS COMING IN" : "THE LAKE LETS GO";
            }
            string full = status + new string('.', (int)(loadTime * 1.7f) % 4);
            Vector2 sz = bodyFont.MeasureString(full);
            Vector2 pos = new(sw * 0.5f - sz.X * 0.5f, sh * 0.845f);
            sb.DrawString(bodyFont, full, pos + new Vector2(1f, 1f), Color.Black * 0.6f);
            sb.DrawString(bodyFont, full, pos, TextDim * 0.88f);

            sb.DrawString(bodyFont, "LAKESIDE VILLAGE",
                new Vector2(sw * 0.034f, sh * 0.892f), TextDim * 0.52f);
        }

        //确定性散列：一次加载内村落布局不跳动
        private static float Hash(int a, int b) {
            float v = MathF.Sin(a * 127.1f + b * 311.7f) * 43758.5453f;
            return v - MathF.Floor(v);
        }
    }
}
