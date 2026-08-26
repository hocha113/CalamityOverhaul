using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using SubworldLibrary;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.Events;
using Terraria.GameInput;
using Terraria.Graphics.Effects;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Scenarios.Hadalworld.UI
{
    /// <summary>
    /// 深渊海沟加载屏中枢(「下潜即加载」:镜像 DungeonworldLoadingScreen 的第一期简洁版)<br/>
    /// 进入与退出共用同一片水体:渐变方向、深度计走向、水涌音高随方向反转<br/>
    /// <br/>
    /// <b>接线方式(Hadalworld 主类内各加一行转发):</b>
    /// <code>
    /// public override void DrawSetup(GameTime gameTime) => HadalworldLoadingScreen.DrawSetup(gameTime);
    /// public override bool ChangeAudio() => HadalworldLoadingScreen.ChangeAudio();
    /// </code>
    /// 进入侧在 SubworldSystem.Enter 之前先调 <see cref="Enter"/>,退出侧先调 <see cref="Exit"/>,
    /// 忘记接线时过渡首帧自愈复位<br/>
    /// 时间源只走 DrawSetup 墙钟(加载期 gameMenu 早退,Update 钩不到;
    /// 本机 SLib 还会把 Main 当 GameTime 传入,Elapsed 恒 0,详见 UI.md "SLib 加载屏时间源")<br/>
    /// Present:SLib 跳过 EndCapture,DrawSetup 须先把 RT 钉回后台缓冲<br/>
    /// 第一期纯 CPU 绘制(渐变水体+气泡+深度计),零 shader 零新贴图
    /// </summary>
    internal class HadalworldLoadingScreen : ModSystem, ILocalizedModType
    {
        public string LocalizationCategory => "UI";

        #region 本地化
        /// <summary>五带铭牌名(日光带..超深渊带)</summary>
        public static LocalizedText[] BandNames { get; private set; }
        /// <summary>轮换短句(下潜意象)</summary>
        public static LocalizedText[] Tips { get; private set; }
        public static LocalizedText StatusDescend { get; private set; }
        public static LocalizedText StatusAscend { get; private set; }
        public static LocalizedText DepthLabel { get; private set; }

        public override void SetStaticDefaults() {
            string[] bandZh = ["日光带", "暮光带", "午夜带", "深渊带", "超深渊带"];
            string[] tipZh = [
                "光留在上面了。",
                "越深，水越安静。",
                "气泡只往一个方向走。",
                "水压不敲门。",
                "下面有东西亮着，那不是灯。",
            ];
            BandNames = new LocalizedText[HadalworldLoadTheme.BandCount];
            for (int i = 0; i < HadalworldLoadTheme.BandCount; i++) {
                int n = i;
                BandNames[i] = this.GetLocalization($"BandName{i}", () => bandZh[n]);
            }
            Tips = new LocalizedText[tipZh.Length];
            for (int i = 0; i < tipZh.Length; i++) {
                int n = i;
                Tips[i] = this.GetLocalization($"Tip{i}", () => tipZh[n]);
            }
            StatusDescend = this.GetLocalization(nameof(StatusDescend), () => "正在下潜");
            StatusAscend = this.GetLocalization(nameof(StatusAscend), () => "正在上浮");
            DepthLabel = this.GetLocalization(nameof(DepthLabel), () => "深度 {0} 米");
        }
        #endregion

        #region 状态
        //方向标志:true=进入(下潜),false=退出(上浮)
        private static bool descending = true;
        //真实秒计时(DrawSetup 墙钟累计)
        private static float realSeconds;
        //上一帧 Advance 的墙钟戳;Reset 归零,首帧按 1/60 起步
        private static long lastAdvanceStamp;
        //过渡行程 0..1(单调递增,进度降级链的滤波输出)
        private static float travel;
        //归一化深度 0..1(进入=travel,退出=1-travel)
        private static float depth;
        //当前带 0..4
        private static int bandNow;
        //已播报的带(-1=尚未首声)
        private static int lastAnnounced = -1;
        private static bool firstPlungeDone;
        //铭牌过带闪计时
        private static readonly float[] plaqueFlash = new float[HadalworldLoadTheme.BandCount];
        //文案轮换
        private static int tipIndex;
        private static float tipTimer;
        //顶部天光强度(Advance 算好供 Draw 消费)
        private static float skyLight;
        //最后一次 DrawSetup 的墙钟帧戳
        private static long lastDrawStamp;
        //本次过渡是否已布防(复位过):世界内帧撤防,过渡首帧自愈布防
        private static bool armed;
        //本次过渡的 DrawSetup 首帧是否已记时间线
        private static bool firstDrawLogged;

        /// <summary>
        /// C 路滤镜注册点:加载期需要静默的 Filters.Scene 滤镜名,
        /// C 路在自己的加载路径里 Add(加载屏逐帧 Deactivate,防菜单帧被世界滤镜套暗幕)
        /// </summary>
        internal static readonly List<string> SilencedFilters = [];
        #endregion

        #region 公开静态 API(骨架转发目标)
        /// <summary>进入方向的状态复位入口(主线程调用,先于 SubworldSystem.Enter)</summary>
        public static void Enter() {
            armed = true;
            Reset(true);
        }

        /// <summary>
        /// 退出方向的状态复位入口(主线程调用,先于 SubworldSystem.Exit)<br/>
        /// 同时作为 Subworld.OnExit 的兜底转发目标(重复调用无害)
        /// </summary>
        public static void Exit() {
            armed = true;
            Reset(false);
        }

        /// <summary>
        /// 加载屏总入口,镜像 SubworldLibrary 的 Subworld.DrawSetup(GameTime) 原型<br/>
        /// 进入与退出共用;方向由 SubworldSystem.Current 判定(null=退出路径)
        /// </summary>
        public static void DrawSetup(GameTime gameTime) {
            SelfArm();
            //压黑门使命到此结束:DrawSetup 已接管,禁止再盖全屏黑矩形
            HadalworldTransitionGate.HandOffToDrawSetup();
            Advance(ResolveDrawDt(gameTime));

            PlayerInput.SetZoom_UI();
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            //SLib 在 HoverItem 后调本方法并 Ret,跳过原版 EndCapture;RT 若停在 screenTarget,
            //绘制进 RT、Present 交出压黑后的后台缓冲,屏幕会一直黑。先钉回后台缓冲
            BindBackbuffer(gd);
            SilenceWorldFilters();
            if (gd != null && !gd.IsDisposed) {
                gd.Clear(HadalworldLoadTheme.HadalBlack);
            }

            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, Main.Rasterizer,
                null, Main.UIScaleMatrix);
            DrawWater();
            DrawMenu();
            Main.DrawCursor(Main.DrawThickCursor());
            Main.spriteBatch.End();
        }

        /// <summary>
        /// 音频接管,镜像 Subworld.ChangeAudio() 原型:<br/>
        /// 加载菜单期间静音(水声独占声场),世界内交还原版选曲
        /// </summary>
        public static bool ChangeAudio() {
            if (Main.gameMenu) {
                Main.newMusic = 0;
                return true;
            }
            return false;
        }
        #endregion

        #region 状态推进
        //计时/深度/节拍全量复位
        private static void Reset(bool enterDirection) {
            descending = enterDirection;
            firstDrawLogged = false;
            realSeconds = 0f;
            lastAdvanceStamp = 0;
            travel = 0f;
            depth = enterDirection ? 0f : 1f;
            bandNow = enterDirection ? 0 : HadalworldLoadTheme.BandCount - 1;
            lastAnnounced = -1;
            firstPlungeDone = false;
            tipIndex = 0;
            tipTimer = 0f;
            Array.Clear(plaqueFlash, 0, plaqueFlash.Length);
        }

        //自愈布防:未经 Enter()/Exit() 接线时在过渡首帧复位一次;
        //布防后加载期任意长帧都不再触发复位(帧戳阈值复位会把入场包络反复清零钉黑)
        private static void SelfArm() {
            long now = Environment.TickCount64;
            long gap = now - lastDrawStamp;
            lastDrawStamp = now;
            if (!armed) {
                armed = true;
                Reset(SubworldSystem.Current != null);
            }
            if (!firstDrawLogged) {
                firstDrawLogged = true;
                HadalworldTransitionLog.Mark(
                    $"DrawSetup 首帧(方向={(descending ? "下潜" : "上浮")}, 距上帧绘制 {gap}ms)");
            }
            else if (gap > 1000) {
                HadalworldTransitionLog.Mark($"加载期长帧 {gap}ms(状态保持,不复位)");
            }
        }

        //世界内每帧撤防:下次过渡的首帧重新布防;提交过渡当帧 gameMenu 已翻 true,不会误撤刚布的防
        //加载期(gameMenu)走不到这里:Main.DoUpdate 在 UpdateMenu 后对非服务器直接 return
        public override void PostUpdateEverything() {
            if (!Main.dedServ && !Main.gameMenu) {
                armed = false;
            }
        }

        //加载期唯一推进点。合法 GameTime 偶发可用时用它,否则墙钟(本机 SLib 常把 Main 当 GameTime,Elapsed=0)
        private static float ResolveDrawDt(GameTime gameTime) {
            long now = Environment.TickCount64;
            float wallDt = lastAdvanceStamp == 0
                ? 1f / 60f
                : (now - lastAdvanceStamp) / 1000f;
            lastAdvanceStamp = now;
            wallDt = MathHelper.Clamp(wallDt, 0f, 0.1f);
            if (wallDt < 0.00005f) {
                wallDt = 1f / 60f;
            }

            float gtElapsed = ReadGtElapsed(gameTime);
            if (gtElapsed > 0.00005f && gtElapsed <= 0.1f) {
                return gtElapsed;
            }
            return wallDt;
        }

        //形参必须是 object:静态类型 GameTime 会让 is 被优化成非空检查,本机传入的其实可能是 Main
        private static float ReadGtElapsed(object maybeTime) {
            return maybeTime is GameTime gt ? (float)gt.ElapsedGameTime.TotalSeconds : 0f;
        }

        //入场包络:压黑已由 TransitionGate 在提交前演完,DrawSetup 接管后从可见亮度起算
        private static float IntroFade => MathHelper.SmoothStep(0.4f, 1f, MathHelper.Clamp(
            realSeconds / HadalworldLoadTheme.IntroFadeEnd, 0f, 1f));

        //前景淡入:略滞后于背景,首帧文案/深度计已可见
        private static float UiFade => MathHelper.SmoothStep(0.2f, 1f, MathHelper.Clamp(
            realSeconds / HadalworldLoadTheme.UiRampEnd, 0f, 1f));

        private static void Advance(float dt) {
            realSeconds += dt;

            //进度降级链:真实生成进度→时间估计(钉95%)→单调滤波(深度计只许沿行进方向走)
            float estDur = descending ? HadalworldLoadTheme.EnterEstSeconds : HadalworldLoadTheme.ExitEstSeconds;
            float target = MathHelper.SmoothStep(0f, 1f,
                MathHelper.Clamp(realSeconds / estDur, 0f, HadalworldLoadTheme.EstPin));
            if (descending) {
                //仅生成窗口非 null;读档/退出/联机客户端全程 null,自动落到时间估计
                double? real = WorldGenerator.CurrentGenerationProgress?.TotalProgress;
                if (real.HasValue) {
                    target = MathHelper.Clamp((float)real.Value, 0f, 1f);
                }
            }
            travel = Math.Max(travel, MathHelper.Lerp(travel, target, 0.08f));

            depth = descending ? travel : 1f - travel;
            bandNow = HadalworldLoadTheme.BandIndex(depth);

            //天光=(1-深度)^2×呼吸×入场包络:日光带明亮,午夜以下近乎无光
            float breath = 0.93f + 0.07f * (float)Math.Sin(realSeconds * 0.6f);
            float surface = 1f - depth;
            skyLight = IntroFade * Math.Max(0.03f, surface * surface) * breath;

            //过带沿节拍:一带只响一次;帧内跨多带时只报抵达带
            if (!firstPlungeDone) {
                if (realSeconds >= HadalworldLoadTheme.FirstPlungeAt) {
                    firstPlungeDone = true;
                    lastAnnounced = bandNow;
                    Plunge(bandNow);
                    plaqueFlash[bandNow] = HadalworldLoadTheme.PlaqueFlashTime;
                }
            }
            else if (bandNow != lastAnnounced) {
                lastAnnounced = bandNow;
                Plunge(bandNow);
                plaqueFlash[bandNow] = HadalworldLoadTheme.PlaqueFlashTime;
            }
            for (int i = 0; i < plaqueFlash.Length; i++) {
                plaqueFlash[i] = Math.Max(0f, plaqueFlash[i] - dt);
            }

            //短句轮换
            tipTimer += dt;
            if (tipTimer >= HadalworldLoadTheme.TipPeriod) {
                tipTimer -= HadalworldLoadTheme.TipPeriod;
                if (Tips != null && Tips.Length > 0) {
                    tipIndex = (tipIndex + 1) % Tips.Length;
                }
            }
        }

        //过带水涌:音高随带沉降,退出方向带序回升=音高自然上行
        private static void Plunge(int band) {
            float pitch = -0.35f - 0.12f * band;
            SoundEngine.PlaySound(SoundID.Splash with { Pitch = pitch, Volume = 0.3f });
            SoundEngine.PlaySound(SoundID.SplashWeak with { Pitch = pitch - 0.2f, Volume = 0.25f });
        }
        #endregion

        #region 绘制
        //纵向海水渐变(屏幕窗口随 depth 下移)+顶部天光柱,纯 CPU 分带条
        private static void DrawWater() {
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null || px.IsDisposed) {
                return;
            }
            int w = Main.screenWidth;
            int h = Main.screenHeight;
            float fade = IntroFade;

            //屏幕纵向映射到深度窗口[depth-0.05, depth+0.08],下潜时整窗随深度下移
            const int bands = 28;
            int bandH = h / bands + 1;
            for (int i = 0; i < bands; i++) {
                float t = i / (float)(bands - 1);
                float frac = MathHelper.Clamp(depth + (t - 0.38f) * 0.13f, 0f, 1f);
                Color c = HadalworldLoadTheme.WaterAt(frac);
                //入场包络只压亮度,不改色相
                c = Color.Lerp(HadalworldLoadTheme.HadalBlack, c, fade);
                c.A = 255;
                Main.spriteBatch.Draw(px, new Rectangle(0, i * (h / bands), w, bandH), c);
            }

            //顶部天光柱:三层嵌套亮带,深度越大越微弱(亮色半透明可叠,暗部禁 magic-pixel 假羽化)
            float[] widths = [0.42f, 0.22f, 0.09f];
            float[] alphas = [0.035f, 0.06f, 0.09f];
            for (int i = 0; i < 3; i++) {
                int bw = (int)(w * widths[i]);
                Main.spriteBatch.Draw(px,
                    new Rectangle(w / 2 - bw / 2, 0, bw, (int)(h * 0.55f)),
                    HadalworldLoadTheme.SkyShaft * (alphas[i] * skyLight));
            }
        }

        //CPU 前景层(气泡/深度计/播报/短句/状态行)
        private static void DrawMenu() {
            int sw = Main.screenWidth;
            int sh = Main.screenHeight;
            float uiFade = UiFade;
            Texture2D px = VaultAsset.placeholder2?.Value;
            DynamicSpriteFont body = FontAssets.MouseText.Value;

            if (px != null && !px.IsDisposed) {
                DrawBubbles(px, sw, sh, uiFade);
                DrawDepthGauge(px, body, sw, sh, uiFade);
            }
            DrawAnnounce(body, sw, sh, uiFade);
            DrawTip(body, sw, sh, uiFade);
            DrawStatus(body, sw, sh);
        }

        //阴影+正文两笔
        private static void DrawText(DynamicSpriteFont font, string text, Vector2 pos, Color color, float scale) {
            Main.spriteBatch.DrawString(font, text, pos + Vector2.One, Color.Black * (color.A / 255f * 0.55f),
                0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            Main.spriteBatch.DrawString(font, text, pos, color,
                0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        //上飘气泡:确定性 hash 伪粒子(下潜=气泡相对上升;上浮方向反转),穿过天光柱时略亮
        private static void DrawBubbles(Texture2D px, int sw, int sh, float uiFade) {
            if (uiFade <= 0.01f) {
                return;
            }
            float dir = descending ? 1f : -0.6f;
            for (int i = 0; i < HadalworldLoadTheme.BubbleCount; i++) {
                float h1 = HadalworldLoadTheme.Hash01(i * 1.618f + 0.31f);
                float h2 = HadalworldLoadTheme.Hash01(i * 2.71f + 7.7f);
                float speed = (0.06f + 0.13f * h2) * dir;
                float cyc = h1 + realSeconds * speed;
                float yFrac = 1f - (cyc - (float)Math.Floor(cyc));
                float xBase = 0.05f + 0.9f * HadalworldLoadTheme.Hash01(i * 3.33f + 1.1f);
                float wobble = (float)Math.Sin(realSeconds * (0.8f + h1 * 0.7f) + i * 2.399f) * 0.014f;
                float xFrac = xBase + wobble;
                float boost = skyLight * (float)Math.Exp(-Math.Abs(xFrac - 0.5f) * 6f) * (float)Math.Exp(-yFrac * 2.5f);
                //深处气泡更稀更暗
                float a = MathHelper.Clamp((0.08f + 0.2f * h1) * (1f + boost * 2f), 0f, 0.7f)
                    * uiFade * MathHelper.Lerp(1f, 0.45f, depth);
                int size = h2 > 0.72f ? 3 : 2;
                Main.spriteBatch.Draw(px,
                    new Rectangle((int)(xFrac * sw), (int)(yFrac * sh), size, size),
                    HadalworldLoadTheme.Bubble * a);
            }
        }

        //右缘深度计:蚀青竖轨+五格铭牌+吊坠随深度走+米数读数
        private static void DrawDepthGauge(Texture2D px, DynamicSpriteFont body, int sw, int sh, float uiFade) {
            if (uiFade <= 0.01f || BandNames == null) {
                return;
            }
            float railX = sw * HadalworldLoadTheme.RailX;
            float top = sh * HadalworldLoadTheme.RailTop;
            float bot = sh * HadalworldLoadTheme.RailBottom;
            float railH = bot - top;
            Color rail = HadalworldLoadTheme.GaugeTeal * (0.7f * uiFade);

            Main.spriteBatch.Draw(px, new Rectangle((int)railX, (int)top, 1, (int)railH), rail);
            Main.spriteBatch.Draw(px, new Rectangle((int)railX - 3, (int)top, 7, 1), rail);
            Main.spriteBatch.Draw(px, new Rectangle((int)railX - 3, (int)bot, 7, 1), rail);

            //格线按真实带底断点排布(非均分),铭牌居中于各带区间
            float prevFrac = 0f;
            for (int i = 0; i < HadalworldLoadTheme.BandCount; i++) {
                float botFrac = HadalworldLoadTheme.BandBottomFracs[i];
                float cellTop = top + railH * prevFrac;
                float cellMid = top + railH * (prevFrac + botFrac) * 0.5f;
                if (i > 0) {
                    Main.spriteBatch.Draw(px, new Rectangle((int)railX - 3, (int)cellTop, 4, 1), rail * 0.8f);
                }
                bool lit = descending ? i <= bandNow : i >= bandNow;
                float flash = plaqueFlash[i] > 0f ? plaqueFlash[i] / HadalworldLoadTheme.PlaqueFlashTime : 0f;
                Color nameCol = lit
                    ? Color.Lerp(HadalworldLoadTheme.BandAccents[i], HadalworldLoadTheme.GaugeHi, flash)
                    : HadalworldLoadTheme.FoamDim * 0.8f;
                string name = BandNames[i].Value;
                const float scale = 0.8f;
                Vector2 size = body.MeasureString(name) * scale;
                var pos = new Vector2(railX - 12f - size.X, cellMid - size.Y * 0.5f);
                DrawText(body, name, pos, nameCol * uiFade, scale);
                if (flash > 0f) {
                    Main.spriteBatch.Draw(px, new Rectangle((int)railX - 8, (int)cellMid, 5, 1),
                        HadalworldLoadTheme.GaugeHi * (flash * uiFade));
                }
                prevFrac = botFrac;
            }

            //吊坠标记(小菱形)
            float py = top + MathHelper.Clamp(depth, 0f, 1f) * railH;
            Color pend = HadalworldLoadTheme.GaugeHi * uiFade;
            Main.spriteBatch.Draw(px, new Rectangle((int)railX, (int)py - 2, 1, 1), pend * 0.8f);
            Main.spriteBatch.Draw(px, new Rectangle((int)railX - 1, (int)py - 1, 3, 1), pend);
            Main.spriteBatch.Draw(px, new Rectangle((int)railX - 2, (int)py, 5, 1), pend);
            Main.spriteBatch.Draw(px, new Rectangle((int)railX - 1, (int)py + 1, 3, 1), pend);
            Main.spriteBatch.Draw(px, new Rectangle((int)railX, (int)py + 2, 1, 1), pend * 0.8f);

            //米数读数挂吊坠旁
            if (DepthLabel != null) {
                string meters = DepthLabel.Format((int)(depth * HadalworldLoadTheme.TrenchDepthMeters));
                DrawText(body, meters, new Vector2(railX + 9f, py - 9f),
                    HadalworldLoadTheme.SeaFoam * (0.62f * uiFade), 0.78f);
            }
        }

        //播报行:当前带名
        private static void DrawAnnounce(DynamicSpriteFont body, int sw, int sh, float uiFade) {
            if (uiFade <= 0.01f || BandNames == null) {
                return;
            }
            int idx = Math.Clamp(bandNow, 0, HadalworldLoadTheme.BandCount - 1);
            float flash = plaqueFlash[idx] > 0f ? plaqueFlash[idx] / HadalworldLoadTheme.PlaqueFlashTime : 0f;
            string line = BandNames[idx].Value;
            Vector2 size = body.MeasureString(line);
            var pos = new Vector2(sw * 0.5f - size.X * 0.5f, sh * 0.735f);
            Color col = Color.Lerp(HadalworldLoadTheme.SeaFoam, HadalworldLoadTheme.GaugeHi, flash * 0.8f);
            DrawText(body, line, pos, col * uiFade, 1f);
        }

        //轮换短句:0.45s 淡入 / 3.8s 驻留 / 0.55s 淡出
        private static void DrawTip(DynamicSpriteFont body, int sw, int sh, float uiFade) {
            if (uiFade <= 0.01f || Tips == null || Tips.Length == 0) {
                return;
            }
            float t = tipTimer;
            float alpha;
            if (t < HadalworldLoadTheme.TipFadeIn) {
                alpha = t / HadalworldLoadTheme.TipFadeIn;
            }
            else if (t < HadalworldLoadTheme.TipFadeIn + HadalworldLoadTheme.TipHold) {
                alpha = 1f;
            }
            else {
                alpha = 1f - (t - HadalworldLoadTheme.TipFadeIn - HadalworldLoadTheme.TipHold)
                    / HadalworldLoadTheme.TipFadeOut;
            }
            string tip = Tips[tipIndex].Value;
            const float scale = 0.95f;
            Vector2 size = body.MeasureString(tip) * scale;
            var pos = new Vector2(sw * 0.5f - size.X * 0.5f, sh * 0.255f);
            DrawText(body, tip, pos, HadalworldLoadTheme.SeaFoam * (alpha * 0.85f * uiFade), scale);
        }

        //底部状态行:生成 Message → Main.statusText → 方向垫底文案,带省略号动画
        private static void DrawStatus(DynamicSpriteFont body, int sw, int sh) {
            float fade = IntroFade;
            if (fade <= 0.01f) {
                return;
            }
            string status = null;
            if (descending) {
                status = WorldGenerator.CurrentGenerationProgress?.Message;
            }
            if (string.IsNullOrEmpty(status)) {
                status = Main.statusText;
            }
            if (string.IsNullOrEmpty(status)) {
                status = (descending ? StatusDescend : StatusAscend)?.Value ?? string.Empty;
            }
            int dotN = (int)(realSeconds * 1.7f) % 4;
            string full = status + new string('.', dotN);
            const float scale = 0.9f;
            Vector2 size = body.MeasureString(full) * scale;
            var pos = new Vector2(sw * 0.5f - size.X * 0.5f, sh * 0.79f);
            DrawText(body, full, pos, HadalworldLoadTheme.SeaFoam * (0.75f * fade), scale);
        }
        #endregion

        #region 渲染管线维护
        //加载期卸掉世界滤镜/原版压暗,防止菜单帧被 C 路滤镜或雾套一层暗幕
        private static void SilenceWorldFilters() {
            foreach (string name in SilencedFilters) {
                Filter filter = Filters.Scene[name];
                if (filter != null && filter.IsActive()) {
                    Filters.Scene.Deactivate(name);
                }
            }
            ScreenDarkness.screenObstruction = 0f;
            ScreenObstruction.screenObstruction = 0f;
        }

        //把绘制目标钉回后台缓冲并复位视口。SLib 早退跳过了原版 EndCapture 的 SetRenderTarget(null)
        private static void BindBackbuffer(GraphicsDevice gd) {
            if (gd == null || gd.IsDisposed) {
                return;
            }
            gd.SetRenderTarget(null);
            PresentationParameters pp = gd.PresentationParameters;
            if (pp != null && pp.BackBufferWidth > 0 && pp.BackBufferHeight > 0) {
                gd.Viewport = new Viewport(0, 0, pp.BackBufferWidth, pp.BackBufferHeight);
            }
        }
        #endregion
    }
}
