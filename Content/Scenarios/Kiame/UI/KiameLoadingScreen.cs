using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Scenarios.Kiame.UI
{
    /// <summary>
    /// 鬼雨加载屏：湿墨夜穹下的废村剪影，斜雨不停，黑水随进度自屏底涨上来。<br/>
    /// 一把立伞站在村口，水涨到哪，倒影就跟到哪；木桩水尺当进度。<br/>
    /// 语汇是怪谈志怪，色板全程冷灰青禁红禁暖；
    /// 纯 CPU 绘制零 shader 依赖（加载期 shader 资产未必就绪）；
    /// 接线走 A 路薄转发（KiameWorld 内各一行）
    /// </summary>
    internal class KiameLoadingScreen : ModSystem, ILocalizedModType
    {
        public string LocalizationCategory => "UI";

        //====== 本地化 ======

        internal static LocalizedText TitleEnter { get; private set; }
        internal static LocalizedText TitleExit { get; private set; }
        internal static LocalizedText VerseEnter { get; private set; }
        internal static LocalizedText VerseExit { get; private set; }
        internal static LocalizedText StatusEnter { get; private set; }
        internal static LocalizedText StatusExit { get; private set; }

        public override void SetStaticDefaults() {
            TitleEnter = this.GetLocalization(nameof(TitleEnter), () => "鬼雨");
            TitleExit = this.GetLocalization(nameof(TitleExit), () => "雨歇");
            VerseEnter = this.GetLocalization(nameof(VerseEnter), () => "洼里的水涨了一夜，没有人来收伞。");
            VerseExit = this.GetLocalization(nameof(VerseExit), () => "伞收了，雨还在下。");
            StatusEnter = this.GetLocalization(nameof(StatusEnter), () => "雨漫过洼地");
            StatusExit = this.GetLocalization(nameof(StatusExit), () => "雨声退远");
        }

        //====== 状态 ======

        private static readonly Rectangle PixelSrc = new(0, 0, 1, 1);

        //湿墨色板：冷灰青/尸斑青/灰白，禁红禁暖（与鬼湖夜雨菜单同律）
        private static readonly Color SkyTop = new(7, 9, 12);
        private static readonly Color SkyMid = new(26, 34, 39);
        private static readonly Color HorizonGlow = new(74, 96, 100);
        private static readonly Color SilFar = new(16, 20, 23);
        private static readonly Color SilNear = new(5, 7, 9);
        private static readonly Color RainPale = new(150, 168, 172);
        private static readonly Color WaterBlack = new(10, 14, 17);
        private static readonly Color WaterRim = new(120, 150, 146);
        private static readonly Color FlashPale = new(143, 161, 166);
        private static readonly Color TextDim = new(150, 166, 168);
        //题字：比雾光亮半档的淡青墨
        private static readonly Color TitleInk = new(172, 192, 194);

        private static float loadTime;
        private static bool entering = true;
        //估时钉住 95%，实际完成由 SubLib 切场景
        private const float EstDuration = 6f;

        //本帧水面基准行（DrawScene 开头算好，倒影/涟漪/水尺共用）
        private static float waterSurfaceY;
        //立伞锚点（DrawScene 里回填，倒影层复用）
        private static float umbrellaX;
        private static float umbrellaFootY;

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

        //雷闪包络：每隔几秒一记速衰的惨白，光在云底先亮
        private static float FlashEnv {
            get {
                float cycle = loadTime % 6.7f;
                return MathF.Exp(-cycle * 5.5f) + 0.45f * MathF.Exp(-((loadTime + 2.9f) % 9.3f) * 6f);
            }
        }

        //====== 景 ======

        private static void DrawScene(SpriteBatch sb, int w, int h) {
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null || px.IsDisposed) {
                return;
            }
            float horizonY = h * 0.60f;
            //水面基准先算好：黑水随进度自屏底涨上来
            float rise = MathHelper.SmoothStep(0f, 1f, Progress / 0.95f);
            waterSurfaceY = MathHelper.Lerp(h * 1.04f, h * 0.74f, rise);

            DrawDome(sb, px, w, h, horizonY);
            //远排：小而密，雨里只剩个大概；近排：大而疏，黑得实。
            //每排先铺贴地剪影带再立结构，村子长在地上而不是浮在天上
            DrawRuinRow(sb, px, w, h, horizonY + h * 0.010f, h * 0.050f, 3, seed: 41, SilFar);
            DrawRuinRow(sb, px, w, h, horizonY + h * 0.058f, h * 0.090f, 7, seed: 733, SilNear);
            DrawUmbrella(sb, px, w, h, horizonY);
            DrawWaterGauge(sb, px, w, h, horizonY, afterWater: false);
            DrawRain(sb, px, w, h);
            DrawWaterRise(sb, px, w, h);
            DrawWaterOptics(sb, px, w, h);
            DrawWaterGauge(sb, px, w, h, horizonY, afterWater: true);
        }

        /// <summary>行地形线：两组慢正弦叠一点定势哈希，结构脚点与地面填充共用同一条线</summary>
        private static float GroundTopAt(float baseY, float unit, int x, int seed) {
            return baseY
                + MathF.Sin(x * 0.0043f + seed * 1.7f) * unit * 0.16f
                + MathF.Sin(x * 0.0131f + seed * 3.1f) * unit * 0.07f;
        }

        //穹顶：倒置明度，头顶最黑、地平尸青雾光反亮；雷闪从云底透出来
        private static void DrawDome(SpriteBatch sb, Texture2D px, int w, int h, float horizonY) {
            const int bands = 96;
            int bandH = h / bands + 1;
            float flash = FlashEnv;
            for (int i = 0; i < bands; i++) {
                float k = i / (float)(bands - 1);
                Color c = Color.Lerp(SkyTop, SkyMid, MathHelper.SmoothStep(0f, 1f, k));
                //地平附近雾光，越靠近越亮，是这画面里唯一的"亮"
                float glow = MathF.Exp(-MathF.Abs(k * h - horizonY) / (h * 0.09f));
                c = Color.Lerp(c, HorizonGlow, glow * 0.45f);
                //雷闪：上半天穹整体透白一瞬
                float flashGrad = 1f - MathHelper.Clamp(k * 1.6f, 0f, 1f);
                c = Color.Lerp(c, FlashPale, flash * flashGrad * 0.5f);
                //±2/255 的确定性微扰：相邻带的色阶断口被打散
                float dith = (Hash(i, 91) - 0.5f) * (4f / 255f);
                c = new Color(
                    MathHelper.Clamp(c.R / 255f + dith, 0f, 1f),
                    MathHelper.Clamp(c.G / 255f + dith, 0f, 1f),
                    MathHelper.Clamp(c.B / 255f + dith, 0f, 1f));
                sb.Draw(px, new Rectangle(0, i * (h / bands), w, bandH), PixelSrc, c);
            }

            //缓涌沉云：两层反向漂移的软边横带（中心实、上下缘与两端各让半档，
            //硬边矩形读作贴片，云不能有直角）
            float t = loadTime;
            for (int i = 0; i < 7; i++) {
                float phase = i * 1.37f;
                float dir = (i & 1) == 0 ? 1f : -1f;
                float y = horizonY - h * (0.07f + i * 0.055f);
                float drift = (t * 11f * dir + phase * 260f) % (w + 640f) - 320f;
                int cw = (int)(w * (0.32f + Hash(i, 3) * 0.4f));
                int ch = (int)(h * (0.014f + Hash(i, 11) * 0.018f));
                DrawSoftBand(sb, px, (int)drift, (int)y, cw, ch,
                    SkyTop, 0.5f + Hash(i, 7) * 0.26f);
            }

            //远景雨幡：云底垂向地平的倾斜暗柱，两层缓移
            for (int i = 0; i < 5; i++) {
                float sx = ((Hash(i, 29) * w * 1.6f) + t * (6f + i * 2f)) % (w + 300f) - 150f;
                float top = horizonY - h * 0.30f;
                float shaftH = h * 0.30f;
                int sw = (int)(w * (0.05f + Hash(i, 31) * 0.06f));
                //斜切：逐段右移画出斜幡
                const int segs = 7;
                for (int s = 0; s < segs; s++) {
                    float sk = s / (float)(segs - 1);
                    sb.Draw(px, new Rectangle((int)(sx + sk * sw * 0.5f), (int)(top + sk * shaftH),
                        sw, (int)(shaftH / segs) + 1), PixelSrc,
                        new Color(38, 48, 52) * (0.16f + 0.08f * Hash(i, 13)));
                }
            }
        }

        //一排废村：先铺贴地剪影带（含缓起伏地形线），再把残屋/断墙/枯树/歪杆
        //的脚点钉在同一条地形线上——没有一扇窗亮着，也没有一样东西悬空
        private static void DrawRuinRow(SpriteBatch sb, Texture2D px, int w, int h, float baseY,
            float unit, int cellDiv, int seed, Color sil) {
            //地面剪影带：逐列填到屏底，顶缘随地形线起伏；上涨的黑水吞的就是这片地
            const int colStep = 4;
            for (int x = 0; x < w; x += colStep) {
                int top = (int)GroundTopAt(baseY, unit, x, seed);
                sb.Draw(px, new Rectangle(x, top, colStep, Math.Max(h - top, 0)), PixelSrc, sil);
            }

            int cell = Math.Max(w / (cellDiv * 6), 18);
            int count = w / cell + 2;
            for (int i = -1; i < count; i++) {
                float h1 = Hash(i, seed);
                float h2 = Hash(i, seed + 7);
                float h3 = Hash(i, seed + 13);
                float roll = Hash(i, seed + 23);
                int cx = i * cell + (int)(h3 * cell * 0.5f);
                //脚点钉在地形线上（略沉半格，接缝被地面吃掉）
                int gy = (int)GroundTopAt(baseY, unit, cx, seed) + 1;

                if (roll < 0.16f) {
                    continue;
                }
                if (roll < 0.34f) {
                    DrawDeadTree(sb, px, cx, gy, unit * (0.62f + h1 * 0.5f), seed + i, sil);
                }
                else if (roll < 0.44f) {
                    //歪电杆：村里唯一一件新东西，也早就不响了
                    DrawLeaningPole(sb, px, cx, gy, unit, cell, sil, seed + i);
                }
                else {
                    DrawRuinHut(sb, px, cx, gy, (int)(unit * (0.72f + h2 * 0.62f)),
                        (int)(unit * (0.50f + h1 * 0.42f)), (int)(unit * 0.30f), sil, seed + i);
                }
            }
        }

        //软边横带：中心实心，上下缘与两端各退一档透明度——云和雾不许有直角
        private static void DrawSoftBand(SpriteBatch sb, Texture2D px, int x, int y,
            int bw, int bh, Color color, float alpha) {
            if (bw <= 4 || bh <= 0) {
                return;
            }
            int edge = Math.Max(bh / 3, 1);
            int inset = Math.Max(bw / 12, 3);
            //中心体
            sb.Draw(px, new Rectangle(x + inset, y, bw - inset * 2, bh), PixelSrc, color * alpha);
            //上下软缘
            sb.Draw(px, new Rectangle(x + inset * 2, y - edge, bw - inset * 4, edge), PixelSrc, color * (alpha * 0.45f));
            sb.Draw(px, new Rectangle(x + inset * 2, y + bh, bw - inset * 4, edge), PixelSrc, color * (alpha * 0.45f));
            //两端软头
            sb.Draw(px, new Rectangle(x, y + bh / 4, inset, bh / 2), PixelSrc, color * (alpha * 0.5f));
            sb.Draw(px, new Rectangle(x + bw - inset, y + bh / 4, inset, bh / 2), PixelSrc, color * (alpha * 0.5f));
        }

        //残屋：墙体 + 残破坡脊（随机蛀掉的横条）+ 塌陷侧；窗是黑的
        private static void DrawRuinHut(SpriteBatch sb, Texture2D px, int cx, int groundY,
            int wid, int hgt, int roofH, Color sil, int seed) {
            wid = Math.Max(wid, 4);
            hgt = Math.Max(hgt, 4);
            roofH = Math.Max(roofH, 2);
            //塌陷户：墙压矮，脊线只剩半边
            bool collapsed = Hash(seed, 3) < 0.35f;
            if (collapsed) {
                hgt = Math.Max(hgt * 2 / 3, 3);
            }
            int bodyTop = groundY - hgt;
            sb.Draw(px, new Rectangle(cx - wid / 2, bodyTop, wid, hgt), PixelSrc, sil);

            int eave = Math.Max(wid / 4, 2);
            int eaveW = wid + eave * 2;
            int collapseDir = Hash(seed, 5) > 0.5f ? 1 : -1;
            for (int i = 0; i < roofH; i++) {
                float k = i / (float)roofH;
                int rw = (int)MathHelper.Lerp(eaveW, wid * 0.22f, MathF.Pow(k, 0.72f));
                if (collapsed) {
                    //塌侧只剩半边脊
                    int half = rw / 2;
                    int rx = collapseDir > 0 ? cx - half : cx;
                    //蛀洞
                    if (Hash(seed + i, 17) < 0.25f) {
                        continue;
                    }
                    sb.Draw(px, new Rectangle(rx, bodyTop - i - 1, Math.Max(half, 1), 1), PixelSrc, sil);
                }
                else {
                    if (Hash(seed + i, 17) < 0.14f) {
                        continue;
                    }
                    sb.Draw(px, new Rectangle(cx - rw / 2, bodyTop - i - 1, Math.Max(rw, 1), 1), PixelSrc, sil);
                }
            }
            //完好户檐口两端各上挑一笔
            if (!collapsed) {
                sb.Draw(px, new Rectangle(cx - eaveW / 2, bodyTop - 3, 2, 3), PixelSrc, sil);
                sb.Draw(px, new Rectangle(cx + eaveW / 2 - 2, bodyTop - 3, 2, 3), PixelSrc, sil);
            }
            //黑窗：比墙体更黑一格的洞
            int ww = Math.Max(wid / 5, 2);
            sb.Draw(px, new Rectangle(cx - ww / 2, bodyTop + hgt / 3, ww, Math.Max(ww * 3 / 4, 2)),
                PixelSrc, Color.Black * 0.55f);
        }

        //歪电杆：斜杆 + 单横臂 + 垂断的线
        private static void DrawLeaningPole(SpriteBatch sb, Texture2D px, int cx, int groundY,
            float unit, int cell, Color sil, int seed) {
            int hgt = (int)(unit * 1.6f);
            float lean = (Hash(seed, 9) - 0.5f) * 0.5f;
            //逐段画斜杆
            const int segs = 8;
            for (int s = 0; s < segs; s++) {
                float k = s / (float)segs;
                sb.Draw(px, new Rectangle((int)(cx + lean * hgt * k), groundY - (int)(hgt * k) - hgt / segs,
                    2, hgt / segs + 1), PixelSrc, sil);
            }
            int topX = (int)(cx + lean * hgt);
            int topY = groundY - hgt;
            sb.Draw(px, new Rectangle(topX - 5, topY + 2, 10, 1), PixelSrc, sil);
            //断线往下荡
            int drop = (int)(unit * 0.5f);
            for (int d = 0; d < drop; d += 2) {
                sb.Draw(px, new Rectangle(topX + 4 + d / 3, topY + 3 + d, 1, 2), PixelSrc, sil * 0.8f);
            }
        }

        //枯树：细干 + 三四条上挑秃枝，梢头各一小团——枝是长在干上的，
        //不是一圈悬空方块（悬空簇在剪影距离上读作坏像素）
        private static void DrawDeadTree(SpriteBatch sb, Texture2D px, int cx, int groundY,
            float size, int seed, Color sil) {
            int trunkH = (int)MathHelper.Max(size, 6f);
            sb.Draw(px, new Rectangle(cx - 1, groundY - trunkH, 2, trunkH), PixelSrc, sil);

            int branches = 3 + (Hash(seed, 40) > 0.55f ? 1 : 0);
            for (int i = 0; i < branches; i++) {
                //枝根落在干的上半段，向外向上逐段爬
                float rootK = 0.45f + Hash(seed, i * 5 + 1) * 0.5f;
                int bx = cx;
                int by = groundY - (int)(trunkH * rootK);
                int dir = Hash(seed, i * 5 + 2) > 0.5f ? 1 : -1;
                int segs = 3 + (int)(Hash(seed, i * 5 + 3) * 3f);
                for (int s = 0; s < segs; s++) {
                    bx += dir * (1 + (int)(Hash(seed, i * 7 + s) * 2f));
                    by -= 1 + (int)(Hash(seed, i * 9 + s) * 2f);
                    sb.Draw(px, new Rectangle(bx, by, 2, 2), PixelSrc, sil);
                }
                //梢头小团：残叶或者别的什么，攥在枝尖上
                if (Hash(seed, i * 5 + 4) > 0.35f) {
                    int bw = (int)MathHelper.Max(size * 0.12f, 2f);
                    sb.Draw(px, new Rectangle(bx - bw / 2, by - bw / 2, bw, bw), PixelSrc, sil);
                }
            }
        }

        //立伞：村口那把入口伞的剪影——伞柄拄地，伞盖层层收窄，尾钩一点
        private static void DrawUmbrella(SpriteBatch sb, Texture2D px, int w, int h, float horizonY) {
            umbrellaX = w * 0.62f;
            umbrellaFootY = horizonY + h * 0.115f;
            float unit = h * 0.11f;
            int poleH = (int)(unit * 1.35f);
            int footY = (int)umbrellaFootY;
            int cx = (int)umbrellaX;

            //微晃：雨里立着的东西不会纹丝不动
            float sway = MathF.Sin(loadTime * 0.8f) * unit * 0.02f;
            //伞柄
            sb.Draw(px, new Rectangle((int)(cx + sway * 0.4f) - 1, footY - poleH, 2, poleH), PixelSrc, SilNear);
            //尾钩
            sb.Draw(px, new Rectangle(cx - 3, footY - 2, 3, 2), PixelSrc, SilNear);
            //伞盖：逐行收窄的穹盖
            int canopyH = (int)(unit * 0.5f);
            int canopyW = (int)(unit * 1.15f);
            int canopyTop = footY - poleH - canopyH;
            for (int i = 0; i < canopyH; i++) {
                float k = i / (float)canopyH;
                int rw = (int)(canopyW * MathF.Sqrt(1f - (1f - k) * (1f - k)));
                sb.Draw(px, new Rectangle((int)(cx + sway) - rw / 2, canopyTop + i, Math.Max(rw, 2), 1),
                    PixelSrc, SilNear);
            }
            //盖沿一线湿光
            sb.Draw(px, new Rectangle((int)(cx + sway) - canopyW / 2, canopyTop + canopyH - 1, canopyW, 1),
                PixelSrc, WaterRim * 0.22f);
            //伞脚一点湿光：柄插在积水里
            sb.Draw(px, new Rectangle(cx - 10, footY - 1, 20, 1), PixelSrc, WaterRim * 0.14f);
        }

        //斜雨丝：三层视差，近层粗快、远层细慢；雷闪时整幕透亮。
        //每丝拆三段头亮尾隐——均匀亮条读作拉伸像素，雨要有来处和去处
        private static void DrawRain(SpriteBatch sb, Texture2D px, int w, int h) {
            float flash = FlashEnv;
            for (int layer = 0; layer < 3; layer++) {
                int count = layer == 0 ? 26 : layer == 1 ? 38 : 52;
                float speed = layer == 0 ? 900f : layer == 1 ? 640f : 430f;
                float len = layer == 0 ? h * 0.040f : layer == 1 ? h * 0.028f : h * 0.019f;
                float slant = 0.16f - layer * 0.03f;
                float alpha = (layer == 0 ? 0.30f : layer == 1 ? 0.21f : 0.13f) * (1f + flash * 0.9f);
                for (int i = 0; i < count; i++) {
                    float hx = Hash(i, 101 + layer * 37);
                    float hy = Hash(i, 211 + layer * 37);
                    float y = (hy * (h + 200f) + loadTime * speed) % (h + 200f) - 100f;
                    float x = hx * w + y * slant;
                    //落进水面以下的不画：雨止于水
                    if (y > waterSurfaceY + 6f) {
                        continue;
                    }
                    //三段渐隐：落点端最亮，来向端几乎没了
                    float segLen = len / 3f;
                    Vector2 dir = new(MathF.Sin(slant), MathF.Cos(slant));
                    Vector2 head = new(x % (w + 40f), y);
                    for (int s = 0; s < 3; s++) {
                        float fade = 1f - s * 0.34f;
                        sb.Draw(px, head - dir * (segLen * (s + 1)), PixelSrc,
                            RainPale * (alpha * fade), slant, Vector2.Zero,
                            new Vector2(1.1f, segLen + 1f), SpriteEffects.None, 0f);
                    }
                }
            }
        }

        //黑水上涨：越深越沉的横带 + 面上一线尸斑青亮沿（带行波）
        private static void DrawWaterRise(SpriteBatch sb, Texture2D px, int w, int h) {
            float surfaceY = waterSurfaceY;
            if (surfaceY >= h) {
                return;
            }
            const int bands = 22;
            float span = h - surfaceY;
            for (int i = 0; i < bands; i++) {
                float k = i / (float)(bands - 1);
                int y = (int)(surfaceY + span * k);
                int bh = (int)(span / bands) + 2;
                float alpha = MathHelper.Lerp(0.62f, 0.98f, MathF.Pow(k, 0.8f));
                float dith = (Hash(i, 57) - 0.5f) * (3f / 255f);
                Color c = new(
                    MathHelper.Clamp(WaterBlack.R / 255f + dith, 0f, 1f),
                    MathHelper.Clamp(WaterBlack.G / 255f + dith, 0f, 1f),
                    MathHelper.Clamp(WaterBlack.B / 255f + dith, 0f, 1f));
                sb.Draw(px, new Rectangle(0, y, w, bh), PixelSrc, c * alpha);
            }

            //水面：三道行波叠出起伏，面上压一线亮沿
            int step = 4;
            for (int x = 0; x < w; x += step) {
                float wave = MathF.Sin(x * 0.0132f + loadTime * 1.15f) * (h * 0.004f)
                    + MathF.Sin(x * 0.0041f - loadTime * 0.62f) * (h * 0.007f)
                    + MathF.Sin(x * 0.0310f + loadTime * 2.2f) * (h * 0.002f);
                int y = (int)(surfaceY + wave);
                sb.Draw(px, new Rectangle(x, y, step, (int)(h * 0.02f)), PixelSrc, WaterRim * 0.10f);
                sb.Draw(px, new Rectangle(x, y - 1, step, 2), PixelSrc, WaterRim * 0.45f);
            }
        }

        //水面光学层：立伞倒影 + 雨砸涟漪 + 碎闪
        private static void DrawWaterOptics(SpriteBatch sb, Texture2D px, int w, int h) {
            float surfaceY = waterSurfaceY;
            if (surfaceY >= h) {
                return;
            }

            //立伞倒影：水面涨近伞脚时，竖向拉长的一道暗影带一点湿光
            float above = umbrellaFootY < surfaceY ? surfaceY - umbrellaFootY : 0f;
            if (above < h * 0.30f) {
                float fade = 1f - above / (h * 0.30f);
                float sway = MathF.Sin(loadTime * 1.3f) * 2f;
                int len = (int)MathHelper.Clamp((h - surfaceY) * 0.6f, 8f, h * 0.16f);
                sb.Draw(px, new Rectangle((int)(umbrellaX + sway - 2f), (int)(surfaceY + 2f),
                    4, len), PixelSrc, SilNear * (0.55f * fade));
                sb.Draw(px, new Rectangle((int)(umbrellaX + sway - 6f), (int)(surfaceY + 2f),
                    12, Math.Max(len / 4, 2)), PixelSrc, SilNear * (0.30f * fade));
                sb.Draw(px, new Rectangle((int)(umbrellaX + sway - 5f), (int)(surfaceY + 1f),
                    10, 1), PixelSrc, WaterRim * (0.25f * fade));
            }

            //雨砸涟漪：水面上扩张的扁环，逐拍换落点
            for (int i = 0; i < 14; i++) {
                float cyc = 0.7f + Hash(i, 61) * 0.8f;
                float t = (loadTime / cyc + Hash(i, 67)) % 1f;
                float beat = MathF.Floor(loadTime / cyc + Hash(i, 67));
                float x = Hash(i + (int)beat, 71) * w;
                float halfW = (2f + t * 15f);
                float alpha = (1f - t) * (1f - t) * 0.5f;
                float wob = MathF.Sin(x * 0.0132f + loadTime * 1.15f) * (h * 0.004f);
                int y = (int)(surfaceY + wob);
                sb.Draw(px, new Rectangle((int)(x - halfW), y, (int)(halfW * 2f), 1), PixelSrc,
                    WaterRim * alpha);
            }

            //细密碎闪打底：高频阈值 + 逐拍重播种
            for (int i = 0; i < 22; i++) {
                float beat = MathF.Floor(loadTime * 7f);
                if (Hash(i, (int)beat % 511) < 0.62f) {
                    continue;
                }
                float x = Hash(i + (int)beat, 83) * w;
                float dy = Hash(i, 89) * (h - surfaceY) * 0.5f;
                sb.Draw(px, new Rectangle((int)x, (int)(surfaceY + 3f + dy), 2, 1), PixelSrc,
                    RainPale * 0.14f);
            }
        }

        //木桩水尺：进度即水位。桩体与刻度画在水前（会被淹掉），水线亮痕画在水后（浮在面上）。
        //桩要种进土里：下粗上细带底堆，一根裸线悬在半空读作坏像素
        private static void DrawWaterGauge(SpriteBatch sb, Texture2D px, int w, int h,
            float horizonY, bool afterWater) {
            int gx = (int)(w * 0.115f);
            int topY = (int)(horizonY + h * 0.02f);
            int footY = (int)(h * 0.93f);
            if (!afterWater) {
                //桩体：下半粗、上半细，微斜一笔
                int midY = (topY + footY) / 2;
                sb.Draw(px, new Rectangle(gx - 2, midY, 5, footY - midY), PixelSrc, SilNear);
                sb.Draw(px, new Rectangle(gx - 1, topY, 3, midY - topY), PixelSrc, SilNear);
                //顶牌
                sb.Draw(px, new Rectangle(gx - 5, topY - 6, 11, 6), PixelSrc, SilNear);
                //底堆：桩脚一小撮土石，桩是打进去的
                sb.Draw(px, new Rectangle(gx - 5, footY - 3, 11, 3), PixelSrc, SilNear);
                sb.Draw(px, new Rectangle(gx - 8, footY - 1, 17, 2), PixelSrc, SilNear * 0.8f);
                //八段刻度：往上数的是水，不是日子
                const int marks = 8;
                for (int i = 0; i <= marks; i++) {
                    int my = topY + (footY - topY) * i / marks;
                    int mw = i % 4 == 0 ? 6 : 4;
                    sb.Draw(px, new Rectangle(gx + 2, my, mw, 1), PixelSrc, WaterRim * 0.40f);
                }
                return;
            }
            //水线亮痕：黑水正淹到的那一格，尸斑青一横
            if (waterSurfaceY > topY - 4 && waterSurfaceY < footY + 4) {
                int ly = (int)waterSurfaceY;
                sb.Draw(px, new Rectangle(gx - 4, ly - 1, 9, 2), PixelSrc, WaterRim * 0.78f);
                sb.Draw(px, new Rectangle(gx - 6, ly, 13, 1), PixelSrc, WaterRim * 0.30f);
            }
        }

        //====== 字 ======

        private static void DrawMenu(SpriteBatch sb, float sw, float sh) {
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null || px.IsDisposed) {
                return;
            }
            DynamicSpriteFont titleFont = FontAssets.DeathText.Value;
            DynamicSpriteFont bodyFont = FontAssets.MouseText.Value;

            //竖排题字 + 落款印：右上，逐字下行
            string title = (entering ? TitleEnter : TitleExit)?.Value ?? (entering ? "鬼雨" : "雨歇");
            DrawVerticalTitle(sb, px, titleFont, title, new Vector2(sw * 0.84f, sh * 0.13f), 1.05f);

            //引子：一句怪谈，浮在水面上方，进度过一成半才慢慢显出来
            float verseFade = MathHelper.Clamp((Progress - 0.15f) / 0.25f, 0f, 1f);
            if (verseFade > 0.01f) {
                string verse = (entering ? VerseEnter : VerseExit)?.Value ?? string.Empty;
                Vector2 vSz = bodyFont.MeasureString(verse);
                Vector2 vPos = new(sw * 0.5f - vSz.X * 0.5f, sh * 0.66f);
                sb.DrawString(bodyFont, verse, vPos + new Vector2(1f, 1f), Color.Black * (0.6f * verseFade));
                sb.DrawString(bodyFont, verse, vPos, TextDim * (0.80f * verseFade));
            }

            //状态行：优先读 gen pass 文案（SubLib 不写 Main.statusText，只读它会一直停在「正在清除地图数据」）
            string status = WorldGenerator.CurrentGenerationProgress?.Message;
            if (string.IsNullOrEmpty(status)) {
                status = Main.statusText;
            }
            if (string.IsNullOrEmpty(status)) {
                status = ((entering ? StatusEnter : StatusExit)?.Value) ?? string.Empty;
            }
            string full = status + new string('…', (int)(loadTime * 1.2f) % 4);
            Vector2 sz = bodyFont.MeasureString(full);
            Vector2 pos = new(sw * 0.5f - sz.X * 0.5f, sh * 0.872f);
            sb.DrawString(bodyFont, full, pos + new Vector2(1f, 1f), Color.Black * 0.55f);
            sb.DrawString(bodyFont, full, pos, TextDim * 0.62f);
        }

        //竖排题字：逐字下行，雨里字迹微晃，末字下方落一枚青黑方印
        private static void DrawVerticalTitle(SpriteBatch sb, Texture2D px, DynamicSpriteFont font,
            string title, Vector2 topCenter, float scale) {
            float y = topCenter.Y;
            float maxW = 0f;
            for (int i = 0; i < title.Length; i++) {
                string s = title[i].ToString();
                Vector2 sz = font.MeasureString(s) * scale;
                maxW = MathHelper.Max(maxW, sz.X);
                float wobX = MathF.Sin(loadTime * 0.9f + i * 2.1f) * 1.3f;
                //偶发暗闪：雨幕里的字迹隔一阵暗一下
                float flick = 1f - 0.18f * MathHelper.Clamp(
                    (MathF.Sin(loadTime * 5.3f + i * 4.7f) - 0.86f) / 0.14f, 0f, 1f);
                Vector2 pos = new(topCenter.X - sz.X * 0.5f + wobX, y);
                sb.DrawString(font, s, pos + new Vector2(2f, 3f), Color.Black * 0.65f,
                    0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                sb.DrawString(font, s, pos, TitleInk * flick,
                    0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                y += sz.Y * 0.88f;
            }
            //落款印：一枚青黑小方，心里挖空一点
            int seal = (int)MathHelper.Max(maxW * 0.22f, 8f);
            int sealX = (int)(topCenter.X - seal * 0.5f);
            int sealY = (int)(y + 6f);
            sb.Draw(px, new Rectangle(sealX, sealY, seal, seal), PixelSrc, new Color(46, 72, 70) * 0.9f);
            sb.Draw(px, new Rectangle(sealX + seal / 4, sealY + seal / 4,
                seal - seal / 2, seal - seal / 2), PixelSrc, new Color(16, 28, 27) * 0.9f);
        }

        //确定性散列：一次加载内村落布局不跳动
        private static float Hash(int a, int b) {
            float v = MathF.Sin(a * 127.1f + b * 311.7f) * 43758.5453f;
            return v - MathF.Floor(v);
        }
    }
}
