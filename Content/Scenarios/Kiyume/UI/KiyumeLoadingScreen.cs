using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Scenarios.Kiyume.UI
{
    /// <summary>
    /// 鬼梦加载屏：红黑穹顶下的湖畔村剪影，雾海随进度涨上来把村子淹掉。<br/>
    /// 语汇是怪谈志怪：竖排题字与落款印、木桩水尺当进度、窗火在雾里晕开、灯影落在雾面上——
    /// 没有任何 HUD 科技元素。<br/>
    /// 纯 CPU 绘制零 shader 依赖（加载期 shader 资产未必就绪）；
    /// 接线走 A 路薄转发（KiyumeWorld 内各一行）
    /// </summary>
    internal class KiyumeLoadingScreen : ModSystem, ILocalizedModType
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
            TitleEnter = this.GetLocalization(nameof(TitleEnter), () => "鬼梦");
            TitleExit = this.GetLocalization(nameof(TitleExit), () => "梦醒");
            VerseEnter = this.GetLocalization(nameof(VerseEnter), () => "雾涨过窗台，屋里的灯还亮着。");
            VerseExit = this.GetLocalization(nameof(VerseExit), () => "湖松手了，别回头。");
            StatusEnter = this.GetLocalization(nameof(StatusEnter), () => "雾从湖上漫过来");
            StatusExit = this.GetLocalization(nameof(StatusExit), () => "村子沉回雾里");
        }

        //====== 状态 ======

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
        //题字：比地平烬光亮半档的暗朱
        private static readonly Color TitleInk = new(196, 62, 36);

        private static float loadTime;
        private static bool entering = true;
        //估时钉住 95%，实际完成由 SubLib 切场景
        private const float EstDuration = 6f;

        //本帧雾海基准面（DrawScene 开头算好，窗火光学层与水尺共用）
        private static float fogSurfaceY;
        //窗火收集：雾海画完后统一画光学层（雾中晕 / 雾面倒影）
        private struct EmberLight
        {
            internal float X;
            internal float Y;
            internal int W;
            internal float Glow;
        }
        private static readonly List<EmberLight> emberLights = new(48);

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

        //====== 景 ======

        private static void DrawScene(SpriteBatch sb, int w, int h) {
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null || px.IsDisposed) {
                return;
            }
            float horizonY = h * 0.615f;
            //雾海基准面先算好：村排画窗火、水尺画亮痕都要对着它做光学判断
            float rise = MathHelper.SmoothStep(0f, 1f, Progress / 0.95f);
            fogSurfaceY = MathHelper.Lerp(h * 1.04f, horizonY + h * 0.028f, rise);
            emberLights.Clear();

            DrawDome(sb, px, w, h, horizonY);
            //远排：小而密，雾里只剩个大概；近排：大而疏，黑得实
            DrawVillageRow(sb, px, w, horizonY + h * 0.012f, h * 0.052f, 3, seed: 37, SilFar, 0.62f, nearRow: false);
            DrawVillageRow(sb, px, w, horizonY + h * 0.062f, h * 0.092f, 7, seed: 911, SilNear, 1f, nearRow: true);
            DrawEmberMotes(sb, px, w, horizonY);
            DrawWaterGauge(sb, px, w, h, horizonY, afterFog: false);
            DrawFogSea(sb, px, w, h);
            //雾后光学层：雾中晕开的窗火、落在雾面上的灯影、水尺水线亮痕
            DrawEmberOptics(sb, px, h);
            DrawWaterGauge(sb, px, w, h, horizonY, afterFog: true);
        }

        //穹顶：红黑竖向层次 + 地平一线烬光；逐带 hash 抖动破色带
        private static void DrawDome(SpriteBatch sb, Texture2D px, int w, int h, float horizonY) {
            const int bands = 96;
            int bandH = h / bands + 1;
            for (int i = 0; i < bands; i++) {
                float k = i / (float)(bands - 1);
                Color c = Color.Lerp(SkyTop, SkyMid, MathHelper.SmoothStep(0f, 1f, k));
                //地平附近烘一层烬光，越靠近越暖
                float glow = MathF.Exp(-MathF.Abs(k * h - horizonY) / (h * 0.085f));
                c = Color.Lerp(c, Horizon, glow * 0.55f);
                //±2/255 的确定性微扰：相邻带的色阶断口被打散
                float dith = (Hash(i, 91) - 0.5f) * (4f / 255f);
                c = new Color(
                    MathHelper.Clamp(c.R / 255f + dith, 0f, 1f),
                    MathHelper.Clamp(c.G / 255f + dith, 0f, 1f),
                    MathHelper.Clamp(c.B / 255f + dith, 0f, 1f));
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

        //一排村落：民居/望楼/枯树错落，近排另掺牌坊与电线杆（乡村怪核的两件标志物）
        private static void DrawVillageRow(SpriteBatch sb, Texture2D px, int w, float baseY,
            float unit, int cellDiv, int seed, Color sil, float lightGate, bool nearRow) {
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

                if (roll < 0.14f) {
                    continue;
                }
                if (roll < 0.30f) {
                    DrawDeadTree(sb, px, cx, gy, unit * (0.62f + h1 * 0.5f), seed + i, sil);
                }
                else if (roll < 0.42f) {
                    DrawHut(sb, px, cx, gy, (int)(unit * (0.30f + h2 * 0.14f)),
                        (int)(unit * (1.25f + h1 * 0.75f)), (int)(unit * 0.52f), sil,
                        lightGate * 0.9f, topWindow: true, lantern: false);
                }
                else if (nearRow && roll < 0.47f) {
                    DrawGateArch(sb, px, cx, gy, unit, sil);
                }
                else if (nearRow && roll < 0.52f) {
                    DrawUtilityPole(sb, px, cx, gy, unit, cell, sil, seed + i);
                }
                else {
                    DrawHut(sb, px, cx, gy, (int)(unit * (0.72f + h2 * 0.62f)),
                        (int)(unit * (0.52f + h1 * 0.44f)), (int)(unit * 0.32f), sil,
                        h1 > 0.66f ? lightGate : 0f, topWindow: false,
                        lantern: nearRow && h2 > 0.72f);
                }
            }
        }

        //民居/望楼：墙体 + 出檐坡脊（逐层收窄的横条堆出脊线）+ 翘角 + 窗火 + 檐下灯笼
        private static void DrawHut(SpriteBatch sb, Texture2D px, int cx, int groundY,
            int wid, int hgt, int roofH, Color sil, float lightGate, bool topWindow, bool lantern) {
            wid = Math.Max(wid, 3);
            hgt = Math.Max(hgt, 4);
            roofH = Math.Max(roofH, 2);
            int bodyTop = groundY - hgt;
            sb.Draw(px, new Rectangle(cx - wid / 2, bodyTop, wid, hgt), PixelSrc, sil);

            int eave = Math.Max(wid / 4, 2);
            int eaveW = wid + eave * 2;
            for (int i = 0; i < roofH; i++) {
                float k = i / (float)roofH;
                //脊线 pow 曲线下垂：檐口外挑、脊头收细
                int rw = (int)MathHelper.Lerp(eaveW, wid * 0.22f, MathF.Pow(k, 0.72f));
                sb.Draw(px, new Rectangle(cx - rw / 2, bodyTop - i - 1, Math.Max(rw, 1), 1), PixelSrc, sil);
            }
            //檐口两端各上挑一笔：瓦檐翘角
            sb.Draw(px, new Rectangle(cx - eaveW / 2, bodyTop - 3, 2, 3), PixelSrc, sil);
            sb.Draw(px, new Rectangle(cx + eaveW / 2 - 2, bodyTop - 3, 2, 3), PixelSrc, sil);

            //檐下灯笼：挂绳一线 + 暖色小灯身，长明——有没有人是另一回事
            if (lantern) {
                int lx = cx + eaveW / 2 - 3;
                int ly = bodyTop + 1;
                sb.Draw(px, new Rectangle(lx, ly, 1, 2), PixelSrc, sil);
                float breath = 0.72f + 0.28f * MathF.Sin(loadTime * 1.7f + cx * 0.11f);
                Color body = Ember * (0.85f * breath);
                sb.Draw(px, new Rectangle(lx - 1, ly + 2, 3, 4), PixelSrc, body);
                //收进光学层：雾里晕开或在雾面上留影
                emberLights.Add(new EmberLight { X = lx + 0.5f, Y = ly + 4f, W = 3, Glow = 0.8f * breath });
            }

            if (lightGate <= 0.01f) {
                return;
            }
            //窗火：忽明忽暗，是屋里还有人
            float flicker = 0.45f + 0.55f * (0.5f + 0.5f * MathF.Sin(loadTime * 2.3f + cx * 0.07f));
            int ww = Math.Max(wid / 5, 2);
            int wy = topWindow ? bodyTop + roofH / 2 : bodyTop + hgt / 3;
            float glow = flicker * lightGate;
            bool submerged = wy > fogSurfaceY;
            if (!submerged) {
                //雾面之上：锐利窗口 + 一层贴脸微晕
                sb.Draw(px, new Rectangle(cx - ww / 2, wy, ww, Math.Max(ww * 3 / 4, 2)), PixelSrc,
                    Ember * glow);
                sb.Draw(px, new Rectangle(cx - ww, wy - ww / 2, ww * 2, ww * 2), PixelSrc,
                    Ember * (glow * 0.10f));
            }
            //雾面之下的锐利窗火不画——被雾吞了，光学层会补一团糊晕
            emberLights.Add(new EmberLight { X = cx, Y = wy + ww * 0.4f, W = ww, Glow = glow });
        }

        //牌坊：两柱一梁，梁上一段小顶——村口那种，谁立的没人记得
        private static void DrawGateArch(SpriteBatch sb, Texture2D px, int cx, int groundY,
            float unit, Color sil) {
            int hgt = (int)(unit * 1.15f);
            int half = (int)(unit * 0.34f);
            int top = groundY - hgt;
            sb.Draw(px, new Rectangle(cx - half, top + 2, 2, hgt - 2), PixelSrc, sil);
            sb.Draw(px, new Rectangle(cx + half - 2, top + 2, 2, hgt - 2), PixelSrc, sil);
            //主梁探出柱外，双层
            sb.Draw(px, new Rectangle(cx - half - 3, top + 1, half * 2 + 6, 2), PixelSrc, sil);
            sb.Draw(px, new Rectangle(cx - half - 1, top + 4, half * 2 + 2, 1), PixelSrc, sil);
            //梁心一段小顶
            sb.Draw(px, new Rectangle(cx - 3, top - 2, 6, 2), PixelSrc, sil);
        }

        //电线杆：高杆 + 双层横臂 + 向两侧垂下的线——村里唯一一件新东西，也早就不响了
        private static void DrawUtilityPole(SpriteBatch sb, Texture2D px, int cx, int groundY,
            float unit, int cell, Color sil, int seed) {
            int hgt = (int)(unit * 1.65f);
            int top = groundY - hgt;
            sb.Draw(px, new Rectangle(cx - 1, top, 2, hgt), PixelSrc, sil);
            sb.Draw(px, new Rectangle(cx - 6, top + 2, 12, 1), PixelSrc, sil);
            sb.Draw(px, new Rectangle(cx - 4, top + 5, 8, 1), PixelSrc, sil);

            //垂线：抛物线近似，向两侧远端渐淡渐没——线的那头没有下一根杆
            int span = (int)(cell * 1.35f);
            float sag = unit * 0.34f * (0.8f + Hash(seed, 3) * 0.4f);
            for (int dir = -1; dir <= 1; dir += 2) {
                for (int d = 4; d < span; d += 4) {
                    float k = d / (float)span;
                    int lx = cx + dir * d;
                    int ly = top + 3 + (int)(sag * k * k * 4f);
                    float fade = 1f - k;
                    sb.Draw(px, new Rectangle(lx, ly, 2, 1), PixelSrc, sil * (0.85f * fade));
                }
            }
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

        //雾海：随进度涨上来，最后把村子淹到只剩屋顶。近表半透让屋顶隐约可见，表面带行波
        private static void DrawFogSea(SpriteBatch sb, Texture2D px, int w, int h) {
            float surfaceY = fogSurfaceY;
            if (surfaceY >= h) {
                return;
            }

            //本体：越深越浓的横带；近表压到三成透明度，淹掉的屋顶还剩个影子
            const int bands = 26;
            float span = h - surfaceY;
            for (int i = 0; i < bands; i++) {
                float k = i / (float)(bands - 1);
                int y = (int)(surfaceY + span * k);
                int bh = (int)(span / bands) + 2;
                float alpha = MathHelper.Lerp(0.30f, 0.97f, MathF.Pow(k, 0.8f));
                //本体带同样打散色阶
                float dith = (Hash(i, 57) - 0.5f) * (3f / 255f);
                Color c = new(
                    MathHelper.Clamp(FogBody.R / 255f + dith, 0f, 1f),
                    MathHelper.Clamp(FogBody.G / 255f + dith, 0f, 1f),
                    MathHelper.Clamp(FogBody.B / 255f + dith, 0f, 1f));
                sb.Draw(px, new Rectangle(0, y, w, bh), PixelSrc, c * alpha);
            }

            //雾面：四道不同波长的行波叠出起伏（含一重碎浪），面上压一条亮边
            int step = 4;
            for (int x = 0; x < w; x += step) {
                float wave = MathF.Sin(x * 0.0132f + loadTime * 1.15f) * (h * 0.006f)
                    + MathF.Sin(x * 0.0041f - loadTime * 0.62f) * (h * 0.011f)
                    + MathF.Sin(x * 0.0009f + loadTime * 0.24f) * (h * 0.016f)
                    + MathF.Sin(x * 0.0310f + loadTime * 2.2f) * (h * 0.0028f);
                int y = (int)(surfaceY + wave);
                //面下近表层：亮一档，让"这是一层液面"读得出来
                sb.Draw(px, new Rectangle(x, y, step, (int)(h * 0.024f)), PixelSrc, FogRim * 0.30f);
                sb.Draw(px, new Rectangle(x, y - 1, step, 2), PixelSrc, FogRim * 0.72f);
            }
        }

        //窗火光学层（雾海之后画）：淹进雾里的窗火晕成一团暖光；雾面上方的窗火在面上留一道拉长的灯影
        private static void DrawEmberOptics(SpriteBatch sb, Texture2D px, int h) {
            float surfaceY = fogSurfaceY;
            foreach (EmberLight light in emberLights) {
                float depth = light.Y - surfaceY;
                if (depth > 0f) {
                    //雾中晕：淹得越深越糊越暗，深过 15% 屏高就吞干净了
                    float fade = 1f - MathHelper.Clamp(depth / (h * 0.15f), 0f, 1f);
                    if (fade <= 0.02f) {
                        continue;
                    }
                    float halo = light.Glow * fade;
                    int r1 = light.W * 3;
                    int r2 = light.W * 5;
                    sb.Draw(px, CenterRect(light.X, light.Y, r1, r1), PixelSrc, Ember * (halo * 0.22f));
                    sb.Draw(px, CenterRect(light.X, light.Y, r2, (int)(r2 * 0.7f)), PixelSrc, Ember * (halo * 0.10f));
                }
                else if (surfaceY < h && -depth < h * 0.22f) {
                    //灯影倒映：竖向拉长的一道暖痕，随行波轻轻摆
                    float above = -depth;
                    float fade = 1f - above / (h * 0.22f);
                    float sway = MathF.Sin(loadTime * 1.3f + light.X * 0.05f) * 1.5f;
                    int len = (int)MathHelper.Clamp(above * 0.5f, 4f, h * 0.08f);
                    sb.Draw(px, new Rectangle((int)(light.X + sway - light.W * 0.5f), (int)(surfaceY + 2f),
                        light.W, len), PixelSrc, Ember * (0.16f * fade * light.Glow));
                }
            }
        }

        //木桩水尺：进度即水位。桩体与刻度画在雾前（会被淹掉），水线亮痕画在雾后（浮在雾面上）
        private static void DrawWaterGauge(SpriteBatch sb, Texture2D px, int w, int h,
            float horizonY, bool afterFog) {
            int gx = (int)(w * 0.115f);
            int topY = (int)(horizonY + h * 0.02f);
            int footY = (int)(h * 0.93f);
            if (!afterFog) {
                //桩体 + 顶牌
                sb.Draw(px, new Rectangle(gx - 1, topY, 3, footY - topY), PixelSrc, SilNear);
                sb.Draw(px, new Rectangle(gx - 5, topY - 6, 11, 6), PixelSrc, SilNear);
                //八段刻度：往上数的是水，不是日子
                const int marks = 8;
                for (int i = 0; i <= marks; i++) {
                    int my = topY + (footY - topY) * i / marks;
                    int mw = i % 4 == 0 ? 6 : 4;
                    sb.Draw(px, new Rectangle(gx + 2, my, mw, 1), PixelSrc, FogRim * 0.45f);
                }
                return;
            }
            //水线亮痕：雾面正淹到的那一格，烬色一横
            if (fogSurfaceY > topY - 4 && fogSurfaceY < footY + 4) {
                int ly = (int)fogSurfaceY;
                sb.Draw(px, new Rectangle(gx - 4, ly - 1, 9, 2), PixelSrc, Ember * 0.78f);
                sb.Draw(px, new Rectangle(gx - 6, ly, 13, 1), PixelSrc, Ember * 0.30f);
            }
        }

        private static Rectangle CenterRect(float cx, float cy, int rw, int rh) =>
            new((int)(cx - rw * 0.5f), (int)(cy - rh * 0.5f), rw, rh);

        //====== 字 ======

        private static void DrawMenu(SpriteBatch sb, float sw, float sh) {
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null || px.IsDisposed) {
                return;
            }
            DynamicSpriteFont titleFont = FontAssets.DeathText.Value;
            DynamicSpriteFont bodyFont = FontAssets.MouseText.Value;

            //竖排题字 + 落款印：右上，逐字下行
            string title = (entering ? TitleEnter : TitleExit)?.Value ?? (entering ? "鬼梦" : "梦醒");
            DrawVerticalTitle(sb, px, titleFont, title, new Vector2(sw * 0.84f, sh * 0.13f), 1.05f);

            //引子：一句怪谈，浮在雾海上方，进度过一成半才慢慢显出来
            float verseFade = MathHelper.Clamp((Progress - 0.15f) / 0.25f, 0f, 1f);
            if (verseFade > 0.01f) {
                string verse = (entering ? VerseEnter : VerseExit)?.Value ?? string.Empty;
                Vector2 vSz = bodyFont.MeasureString(verse);
                Vector2 vPos = new(sw * 0.5f - vSz.X * 0.5f, sh * 0.775f);
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

        //竖排题字：逐字下行，记忆颤动（低频摆 + 偶发暗闪），末字下方落一枚暗红方印
        private static void DrawVerticalTitle(SpriteBatch sb, Texture2D px, DynamicSpriteFont font,
            string title, Vector2 topCenter, float scale) {
            float y = topCenter.Y;
            float maxW = 0f;
            for (int i = 0; i < title.Length; i++) {
                string s = title[i].ToString();
                Vector2 sz = font.MeasureString(s) * scale;
                maxW = MathHelper.Max(maxW, sz.X);
                float wobX = MathF.Sin(loadTime * 0.9f + i * 2.1f) * 1.3f;
                //偶发暗闪：影像不稳，字迹隔一阵暗一下
                float flick = 1f - 0.18f * MathHelper.Clamp(
                    (MathF.Sin(loadTime * 5.3f + i * 4.7f) - 0.86f) / 0.14f, 0f, 1f);
                Vector2 pos = new(topCenter.X - sz.X * 0.5f + wobX, y);
                sb.DrawString(font, s, pos + new Vector2(2f, 3f), Color.Black * 0.65f,
                    0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                sb.DrawString(font, s, pos, TitleInk * flick,
                    0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                y += sz.Y * 0.88f;
            }
            //落款印：一枚暗红小方，心里挖空一点
            int seal = (int)MathHelper.Max(maxW * 0.22f, 8f);
            int sealX = (int)(topCenter.X - seal * 0.5f);
            int sealY = (int)(y + 6f);
            sb.Draw(px, new Rectangle(sealX, sealY, seal, seal), PixelSrc, new Color(122, 24, 18) * 0.9f);
            sb.Draw(px, new Rectangle(sealX + seal / 4, sealY + seal / 4,
                seal - seal / 2, seal - seal / 2), PixelSrc, new Color(58, 8, 8) * 0.9f);
        }

        //确定性散列：一次加载内村落布局不跳动
        private static float Hash(int a, int b) {
            float v = MathF.Sin(a * 127.1f + b * 311.7f) * 43758.5453f;
            return v - MathF.Floor(v);
        }
    }
}
