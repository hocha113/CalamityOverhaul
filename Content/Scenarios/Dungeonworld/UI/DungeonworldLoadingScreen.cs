using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Scenarios.Dungeonworld.Backgrounds;
using CalamityOverhaul.Content.Scenarios.Dungeonworld.Fog;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using SubworldLibrary;
using System;
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

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.UI
{
    /// <summary>
    /// 地牢子世界加载屏中枢（「下降即加载」：加载屏即吊笼降井本身）<br/>
    /// 进入与退出共用同一口井：石壁滚动方向、顶光、钟声音高随方向反转<br/>
    /// <br/>
    /// <b>A 路接线方式（Dungeonworld 主类内各加一行转发即可）：</b>
    /// <code>
    /// public override void DrawSetup(GameTime gameTime) => DungeonworldLoadingScreen.DrawSetup(gameTime);
    /// public override bool ChangeAudio() => DungeonworldLoadingScreen.ChangeAudio();
    /// public override void OnExit() => DungeonworldLoadingScreen.Exit();//状态兜底复位
    /// </code>
    /// 触发进入的一方（传送物品等）建议在 SubworldSystem.Enter&lt;Dungeonworld&gt;() 之前
    /// 先调 <see cref="Enter"/>，退出侧在 SubworldSystem.Exit() 之前先调 <see cref="Exit"/>——
    /// 主线程先复位，规避首帧竞态<br/>
    /// 过渡链路修复：自愈复位改为「一次过渡只复位一次」（世界内帧撤防、过渡首帧布防），
    /// 旧的 1s 帧戳阈值会在头段长帧（12M tile 分配/GC 卡顿）下逐帧误判新过渡，
    /// 把入场包络反复清零钉在纯黑——这正是「进入先黑屏十几秒」的根因。<br/>
    /// 时间源：加载期 Main.gameMenu=true，DoUpdate 在 UpdateMenu 后对客户端直接 return，
    /// ModSystem.Update/PostUpdateEverything/Subworld.Update 全部不到。动画只许在 DrawSetup 推进。
    /// 本机 SLib IL_Main.DoDraw 传 Ldarg_0(Main this) 当 GameTime（GitHub 已改 Ldarg_1），
    /// CybCourse 因此用 +0.02f/帧而不是 ElapsedGameTime。本屏优先合法 GameTime，否则墙钟。<br/>
    /// Present 修复：SLib 在 HoverItem 后调 DrawSetup 并 Ret，跳过原版 EndCapture。
    /// 若当前 RT 仍是 screenTarget，绘制进 RT、Present 交出压黑后的后台缓冲——
    /// 不操作全程黑屏，QQ 截图等外部刷新才看见已在跑的加载屏。DrawSetup 必须先钉回后台缓冲
    /// </summary>
    internal class DungeonworldLoadingScreen : ModSystem, ILocalizedModType
    {
        public string LocalizationCategory => "UI";

        #region 本地化
        /// <summary>七层铭牌层名（I..VII）</summary>
        public static LocalizedText[] BandNames { get; private set; }
        /// <summary>七条过层播报「第X响 · 层名」</summary>
        public static LocalizedText[] Tolls { get; private set; }
        /// <summary>轮换箴言（铭文体）</summary>
        public static LocalizedText[] Tips { get; private set; }
        public static LocalizedText StatusDescend { get; private set; }
        public static LocalizedText StatusAscend { get; private set; }
        public static LocalizedText DepthLabel { get; private set; }
        /// <summary>揭示层黑幕等待期的状态行（首帧闩锁期间显示）</summary>
        public static LocalizedText RevealHold { get; private set; }

        public override void SetStaticDefaults() {
            string[] bandZh = ["教堂区", "牢狱层", "大档案馆", "水牢", "万骨窖", "铸造机关层", "倒吊教堂"];
            string[] tollZh = [
                "第一响 · 教堂区", "第二响 · 牢狱层", "第三响 · 大档案馆", "第四响 · 水牢",
                "第五响 · 万骨窖", "第六响 · 铸造机关层", "第七响 · 倒吊教堂",
            ];
            string[] tipZh = [
                "越往下，砖越老，祷词越短。",
                "唱诗声还在，只是隔了七层楼板。",
                "上面敲一下钟，井底回两下。",
                "蜡烛越往下越矮，烛泪越往下越厚。",
                "档案馆收每个人的名字，包括还没来的。",
                "万骨窖的砖不用灰浆，骨头咬得比灰浆紧。",
                "齿轮没停过，上油的人换了七代。",
                "最底下那座教堂，祭坛钉在天花板上。",
            ];
            BandNames = new LocalizedText[DungeonworldLoadTheme.BandCount];
            Tolls = new LocalizedText[DungeonworldLoadTheme.BandCount];
            for (int i = 0; i < DungeonworldLoadTheme.BandCount; i++) {
                int n = i;
                BandNames[i] = this.GetLocalization($"BandName{i}", () => bandZh[n]);
                Tolls[i] = this.GetLocalization($"Toll{i}", () => tollZh[n]);
            }
            Tips = new LocalizedText[tipZh.Length];
            for (int i = 0; i < tipZh.Length; i++) {
                int n = i;
                Tips[i] = this.GetLocalization($"Tip{i}", () => tipZh[n]);
            }
            StatusDescend = this.GetLocalization(nameof(StatusDescend), () => "吊笼下行中");
            StatusAscend = this.GetLocalization(nameof(StatusAscend), () => "吊笼上行中");
            DepthLabel = this.GetLocalization(nameof(DepthLabel), () => "深度 {0}%");
            RevealHold = this.GetLocalization(nameof(RevealHold), () => "正在点亮烛火");
        }
        #endregion

        #region 状态
        //方向标志：true=进入（下行），false=退出（上行）
        private static bool descending = true;
        //真实秒计时（DrawSetup 墙钟累计。加载期 gameMenu 早退,ModSystem/Subworld.Update 都不跑）
        private static float realSeconds;
        //DrawSetup 调用次数（诊断 tick,与墙钟无关）
        private static int drawTick;
        //上一帧 Advance 的墙钟戳;Reset 归零,首帧按 1/60 起步
        private static long lastAdvanceStamp;
        //本帧实际采用的 dt,以及探针读到的 GameTime.Elapsed（非法=-1）
        private static float lastUsedDt;
        private static float lastGtElapsed;
        //石壁累计滚动量（屏高单位，含方向）
        private static float scrollY;
        //过渡行程 0..1（单调递增，进度降级链的滤波输出）
        private static float travel;
        //石壁速度增益（真实进度速率驱动，±30%）
        private static float speedGain = 1f;
        //深度 0..7（进入=travel*7，退出=(1-travel)*7）
        private static float depth;
        //当前层 1..7
        private static int layerNow = 1;
        //已播报的层（0=尚未第一响）
        private static int lastAnnounced;
        private static bool firstBellDone;
        //铭牌过层闪计时
        private static readonly float[] plaqueFlash = new float[DungeonworldLoadTheme.BandCount];
        //文案轮换
        private static int tipIndex;
        private static float tipTimer;
        //绘制侧缓存（Advance 算好供 Draw 消费）
        private static float topLight;
        private static float candleFlicker = 1f;
        //最后一次 DrawSetup 的墙钟帧戳（长帧诊断 + 揭示层测「加载末帧→世界首帧」间隔）
        private static long lastDrawStamp;
        //本次过渡是否已布防（复位过）：世界内帧撤防,过渡首帧自愈布防,加载期长帧不再误触发复位
        private static bool armed;
        //本次过渡的 DrawSetup 首帧是否已记录时间线日志
        private static bool firstDrawLogged;
        //每秒诊断累计（真实秒，跟 Advance 同一 dt）
        private static float diagSeconds;
        //本帧实际绘制路径：shader / cpu / black
        private static string drawnPath = "black";

        /// <summary>最后一次 DrawSetup 的墙钟帧戳，供揭示层测量硬切间隔</summary>
        internal static long LastDrawStamp => lastDrawStamp;
        #endregion

        #region 公开静态 API（A 路转发目标）
        /// <summary>
        /// 进入方向的状态复位入口（主线程调用，先于 SubworldSystem.Enter）<br/>
        /// A 路的进入触发点调用；忘记接线时过渡首帧自愈复位
        /// </summary>
        public static void Enter() {
            armed = true;
            Reset(true);
        }

        /// <summary>
        /// 退出方向的状态复位入口（主线程调用，先于 SubworldSystem.Exit）<br/>
        /// 同时作为 Subworld.OnExit 的兜底转发目标（重复调用无害）
        /// </summary>
        public static void Exit() {
            armed = true;
            Reset(false);
        }

        /// <summary>
        /// 加载屏总入口，镜像 SubworldLibrary 的 Subworld.DrawSetup(GameTime) 原型<br/>
        /// 进入与退出共用；方向由 SubworldSystem.Current 判定（null=退出路径）
        /// </summary>
        public static void DrawSetup(GameTime gameTime) {
            SelfArm();
            //压黑门使命到此结束:DrawSetup 已接管,禁止再盖全屏黑矩形
            DungeonworldTransitionGate.HandOffToDrawSetup();
            //加载期唯一推进点:SLib 只保证调 DrawSetup,不调 Update。时间源见 ResolveDrawDt
            float dt = ResolveDrawDt(gameTime);
            Advance(dt);
            drawTick++;
            if (descending) {
                //逐帧刷新揭示层军备时戳:世界侧凭"时戳新鲜"判定本次进入需要落底演出
                DungeonworldEntryReveal.ArmFromLoading();
            }

            PlayerInput.SetZoom_UI();
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            //SLib 在 HoverItem 赋值后调用本方法并直接 Ret(SubworldLibrary.cs IL_Main.DoDraw),
            //跳过了原版随后的 Clear / Filters.BeginCapture / EndCapture / DrawMenu。
            //若 OnPreDraw 或上一帧 EndCaptureDraw 把 RT 留在 screenTarget 上,
            //Clear+绘制会进 RT,FNA EndDraw.Present 却交出未被重画的后台缓冲——
            //屏幕停在压黑门的纯黑,直到外部事件(QQ 截图/失焦)迫使交换链刷新。
            string rtBefore = ProbeCurrentTarget(gd);
            BindBackbuffer(gd);
            SilenceWorldFilters();
            if (gd != null && !gd.IsDisposed) {
                //清成石壁中间调,避免 shader 未盖满时露出井心 Abyss 当黑幕
                gd.Clear(DungeonworldLoadTheme.Stone);
            }

            DrawBackground();

            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, Main.Rasterizer,
                null, Main.UIScaleMatrix);
            DrawMenu(gameTime);
            Main.DrawCursor(Main.DrawThickCursor());
            Main.spriteBatch.End();

            TickDrawDiag(dt, rtBefore);
        }

        /// <summary>
        /// CPU 前景层（尘埃/深度计/播报/箴言/状态行），由本类 DrawSetup 内部调用<br/>
        /// 镜像 Subworld.DrawMenu(GameTime) 原型，A 路无需单独转发
        /// </summary>
        public static void DrawMenu(GameTime gameTime) {
            int sw = Main.screenWidth;
            int sh = Main.screenHeight;
            float uiFade = UiFade;
            Texture2D px = VaultAsset.placeholder2?.Value;
            DynamicSpriteFont body = FontAssets.MouseText.Value;

            if (px != null && !px.IsDisposed) {
                DrawDust(px, sw, sh, uiFade);
                DrawDepthGauge(px, body, sw, sh, uiFade);
            }
            DrawAnnounce(body, sw, sh, uiFade);
            DrawTip(body, sw, sh, uiFade);
            DrawStatus(body, sw, sh);
        }

        /// <summary>
        /// 音频接管，镜像 Subworld.ChangeAudio() 原型：<br/>
        /// 加载菜单期间静音（钟声与风底独占声场），世界内交还原版选曲（ZoneDungeon 音乐免费到位）
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
            diagSeconds = 0f;
            drawnPath = "black";
            realSeconds = 0f;
            drawTick = 0;
            lastAdvanceStamp = 0;
            lastUsedDt = 0f;
            lastGtElapsed = 0f;
            scrollY = 0f;
            travel = 0f;
            speedGain = 1f;
            depth = enterDirection ? 0f : 7f;
            layerNow = enterDirection ? 1 : 7;
            lastAnnounced = 0;
            firstBellDone = false;
            tipIndex = 0;
            tipTimer = 0f;
            Array.Clear(plaqueFlash, 0, plaqueFlash.Length);
        }

        //自愈布防：未经 Enter()/Exit() 接线时在过渡首帧复位一次;
        //布防后加载期任意长帧都不再触发复位(旧 1s 阈值会把入场包络反复清零钉黑,已废弃)
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
                DungeonworldTransitionLog.Mark(
                    $"DrawSetup 首帧(方向={(descending ? "下行" : "上行")}, 距上帧绘制 {gap}ms)");
            }
            else if (gap > 1000) {
                //长帧诊断:主线程被冻结了多久,一条一行,用户 QA 直接抄日志
                DungeonworldTransitionLog.Mark($"加载期长帧 {gap}ms(状态保持,不复位)");
            }
        }

        //世界内每帧撤防:下次过渡的首帧重新布防;提交过渡当帧 gameMenu 已翻 true,不会误撤刚布的防
        //加载期(gameMenu)走不到这里:Main.DoUpdate 在 UpdateMenu 后对非服务器直接 return
        public override void PostUpdateEverything() {
            if (!Main.dedServ && !Main.gameMenu) {
                armed = false;
            }
        }

        //SLib IL_Main.DoDraw 本机源码传 Ldarg_0(=Main this),GitHub 已改 Ldarg_1(=GameTime)
        //参数可能根本不是 GameTime,或 Elapsed 恒 0。CybCourse 因此写死 +0.02f/帧
        //合法 GameTime 优先(用户要求);否则墙钟,保证井壁/深度计/钟声在绘制帧前进
        private static float ResolveDrawDt(GameTime gameTime) {
            lastGtElapsed = ProbeGtElapsed(gameTime);

            long now = Environment.TickCount64;
            float wallDt = lastAdvanceStamp == 0
                ? 1f / 60f
                : (now - lastAdvanceStamp) / 1000f;
            lastAdvanceStamp = now;
            wallDt = MathHelper.Clamp(wallDt, 0f, 0.1f);

            if (lastGtElapsed > 0.00005f && lastGtElapsed <= 0.1f) {
                lastUsedDt = lastGtElapsed;
                return lastUsedDt;
            }
            if (wallDt < 0.00005f) {
                wallDt = 1f / 60f;
            }
            lastUsedDt = wallDt;
            return lastUsedDt;
        }

        //形参必须是 object:SLib 可把 Main 塞进来;静态类型 GameTime 会让编译器把 is 优化成非空检查
        private static float ProbeGtElapsed(object maybeTime) {
            if (maybeTime is GameTime gt) {
                return (float)gt.ElapsedGameTime.TotalSeconds;
            }
            return -1f;
        }

        //入场包络：压黑已由 TransitionGate 在提交前演完,DrawSetup 接管后从可见亮度起算,
        //不再重复设计稿 0~0.18s 纯黑保持(那会让「接管后必须看见加载内容」失败)
        private static float IntroFade => MathHelper.SmoothStep(0.4f, 1f, MathHelper.Clamp(
            realSeconds / Math.Max(0.01f, DungeonworldLoadTheme.IntroFadeEnd - DungeonworldLoadTheme.BlackHoldEnd),
            0f, 1f));

        //前景淡入：与背景同时起步,首帧文案/深度计已可见
        private static float UiFade => MathHelper.SmoothStep(0.2f, 1f, MathHelper.Clamp(
            realSeconds / Math.Max(0.01f, DungeonworldLoadTheme.ScrollRampEnd - DungeonworldLoadTheme.IntroFadeEnd),
            0f, 1f));

        private static void Advance(float dt) {
            realSeconds += dt;

            //进度降级链：真实生成进度 → 时间估计（钉95%）→ 单调滤波（深度计只许沿行进方向走）
            float estDur = descending ? DungeonworldLoadTheme.EnterEstSeconds : DungeonworldLoadTheme.ExitEstSeconds;
            float target = MathHelper.SmoothStep(0f, 1f,
                MathHelper.Clamp(realSeconds / estDur, 0f, DungeonworldLoadTheme.EstPin));
            if (descending) {
                //仅生成窗口非 null；读档/退出/联机客户端全程 null，自动落到时间估计
                double? real = WorldGenerator.CurrentGenerationProgress?.TotalProgress;
                if (real.HasValue) {
                    target = MathHelper.Clamp((float)real.Value, 0f, 1f);
                }
            }
            float prev = travel;
            travel = Math.Max(travel, MathHelper.Lerp(travel, target, 0.08f));

            //石壁巡航速度：进度涨得快=降得快（±30%），无进度时恒速
            float rate = dt > 0f ? (travel - prev) / dt : 0f;
            float gainTarget = MathHelper.Clamp(rate * estDur,
                DungeonworldLoadTheme.ScrollGainMin, DungeonworldLoadTheme.ScrollGainMax);
            speedGain = MathHelper.Lerp(speedGain, gainTarget, 0.06f);
            float ramp = MathHelper.SmoothStep(0f, 1f, MathHelper.Clamp(
                (realSeconds - DungeonworldLoadTheme.IntroFadeEnd)
                / (DungeonworldLoadTheme.ScrollRampEnd - DungeonworldLoadTheme.IntroFadeEnd), 0f, 1f));
            scrollY += dt * DungeonworldLoadTheme.BaseScrollSpeed * speedGain * ramp * (descending ? 1f : -1f);

            depth = descending ? travel * 7f : (1f - travel) * 7f;
            layerNow = (int)MathHelper.Clamp(depth, 0f, 6.999f) + 1;

            //顶光=（1-深度/7）×呼吸×入场包络；退出时深度回升，顶光自然渐强
            float breath = 0.93f + 0.07f * (float)Math.Sin(realSeconds * 0.7f);
            topLight = IntroFade * Math.Max(0.12f, 1f - depth / 7f) * breath;

            //烛光 flicker：双频 sin + 逐帧 hash，幅度 ≤0.28
            float hash = DungeonworldLoadTheme.Hash01((float)Math.Floor(realSeconds * 60f) * 0.618f);
            candleFlicker = 0.86f + 0.12f * (float)Math.Sin(realSeconds * 2.3f) + (hash - 0.5f) * 0.12f;

            //跨层沿节拍：一层只响一次；帧内跨多层时只敲抵达层，不连响
            if (!firstBellDone) {
                if (realSeconds >= DungeonworldLoadTheme.FirstBellAt) {
                    firstBellDone = true;
                    lastAnnounced = layerNow;
                    Toll(layerNow, true);
                    plaqueFlash[layerNow - 1] = DungeonworldLoadTheme.PlaqueFlashTime;
                }
            }
            else if (layerNow != lastAnnounced) {
                lastAnnounced = layerNow;
                Toll(layerNow, false);
                plaqueFlash[layerNow - 1] = DungeonworldLoadTheme.PlaqueFlashTime;
            }
            for (int i = 0; i < plaqueFlash.Length; i++) {
                plaqueFlash[i] = Math.Max(0f, plaqueFlash[i] - dt);
            }

            //箴言轮换
            tipTimer += dt;
            if (tipTimer >= DungeonworldLoadTheme.TipPeriod) {
                tipTimer -= DungeonworldLoadTheme.TipPeriod;
                if (Tips != null && Tips.Length > 0) {
                    tipIndex = (tipIndex + 1) % Tips.Length;
                }
            }
        }

        //梵钟配方（OniMeiBellWave 已上线验证）：Item52 主钟体 + DD2_BetsyWindAttack 风底
        //音高按层沉降：第一层 -0.55，每层再沉 0.058，第七层 ≈ -0.9；退出方向层号回升=音高自然上行
        private static void Toll(int layer, bool first) {
            float pitch = -0.55f - 0.058f * (layer - 1);
            SoundEngine.PlaySound(SoundID.Item52 with { Pitch = pitch, Volume = first ? 0.6f : 0.78f });
            if (!first) {
                SoundEngine.PlaySound(SoundID.DD2_BetsyWindAttack with { Pitch = -0.9f, Volume = 0.35f });
            }
        }
        #endregion

        #region 背景（shader 或 CPU 回退）
        private static void DrawBackground() {
            int w = Main.screenWidth;
            int h = Main.screenHeight;
            var shader = EffectLoader.DungeonworldLoading?.Value;
            if (shader == null || VaultAsset.placeholder2 == null || VaultAsset.placeholder2.IsDisposed) {
                //FNA3D 静默毁 shader 时的完整回退：渐变井底 + 顶光；深度计/文字/钟声照常，绝不裸黑屏
                DrawBackgroundFallback(w, h);
                drawnPath = VaultAsset.placeholder2?.Value != null && !VaultAsset.placeholder2.IsDisposed
                    ? "cpu" : "black";
                return;
            }

            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, Main.Rasterizer,
                null, Main.UIScaleMatrix);

            //uniform 残值硬规则：每次调用全参数重设
            shader.Parameters["uTime"]?.SetValue(realSeconds);
            shader.Parameters["uScrollY"]?.SetValue(scrollY);
            shader.Parameters["uDepth"]?.SetValue(depth);
            shader.Parameters["uAspectRatio"]?.SetValue((float)w / h);
            shader.Parameters["uIntro"]?.SetValue(IntroFade);
            shader.Parameters["uTopLight"]?.SetValue(topLight);
            shader.Parameters["uCandle"]?.SetValue(candleFlicker);
            for (int i = 0; i < DungeonworldLoadTheme.BandCount; i++) {
                shader.Parameters["uBand" + i]?.SetValue(DungeonworldLoadTheme.Vec3(DungeonworldLoadTheme.BandAccents[i]));
            }
            shader.CurrentTechnique.Passes[0].Apply();

            Main.spriteBatch.Draw(VaultAsset.placeholder2.Value, new Rectangle(0, 0, w, h), Color.White);
            Main.spriteBatch.End();
            drawnPath = "shader";
        }

        //CPU 回退：纵向渐变井壁基调 + 中央顶光柱，只有石壁砌砖材质缺席
        private static void DrawBackgroundFallback(int w, int h) {
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null || px.IsDisposed) {
                return;
            }
            float fade = IntroFade;
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, Main.Rasterizer,
                null, Main.UIScaleMatrix);

            const int bands = 24;
            int bandH = h / bands + 1;
            for (int i = 0; i < bands; i++) {
                float t = i / (float)(bands - 1);
                //全幅石壁中间调,底部只略沉,不沉到 Abyss 当黑纸
                Color c = Color.Lerp(DungeonworldLoadTheme.Stone, DungeonworldLoadTheme.StoneDeep, t * 0.42f);
                c = Color.Lerp(c, DungeonworldLoadTheme.StoneLit, fade * 0.08f);
                c.A = 255;
                Main.spriteBatch.Draw(px, new Rectangle(0, i * (h / bands), w, bandH), c);
            }
            //中轴井缝:半透明压暗,中间仍是石头色不是黑矩形
            int wellW = (int)(w * 0.18f);
            Main.spriteBatch.Draw(px,
                new Rectangle(w / 2 - wellW / 2, 0, wellW, h),
                DungeonworldLoadTheme.StoneDeep * 0.28f);

            //顶光柱：三层嵌套亮带（亮色半透明可以叠，magic-pixel 禁令只针对暗部假羽化）
            float[] widths = [0.30f, 0.16f, 0.07f];
            float[] alphas = [0.045f, 0.075f, 0.11f];
            for (int i = 0; i < 3; i++) {
                int bw = (int)(w * widths[i]);
                Main.spriteBatch.Draw(px,
                    new Rectangle(w / 2 - bw / 2, 0, bw, (int)(h * 0.52f)),
                    DungeonworldLoadTheme.CandleHi * (alphas[i] * topLight));
            }
            Main.spriteBatch.End();
        }

        //加载期卸掉世界滤镜/原版压暗,防止菜单帧被 FilterMiniTower 或雾套一层暗幕
        private static void SilenceWorldFilters() {
            DeactivateFilter(DungeonworldSky.Name);
            DeactivateFilter(DungeonworldFogSystem.FilterName);
            ScreenDarkness.screenObstruction = 0f;
            ScreenObstruction.screenObstruction = 0f;
        }

        private static void DeactivateFilter(string name) {
            Filter filter = Filters.Scene[name];
            if (filter != null && filter.IsActive()) {
                Filters.Scene.Deactivate(name);
            }
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

        //诊断用:当前 OM 绑定的是后台缓冲还是某块 RT
        private static string ProbeCurrentTarget(GraphicsDevice gd) {
            if (gd == null || gd.IsDisposed) {
                return "none";
            }
            RenderTargetBinding[] bindings = gd.GetRenderTargets();
            if (bindings == null || bindings.Length == 0) {
                return "backbuffer";
            }
            Texture rt = bindings[0].RenderTarget;
            if (rt == Main.screenTarget) {
                return "screenTarget";
            }
            if (rt is RenderTarget2D sized) {
                return $"rt:{sized.Width}x{sized.Height}";
            }
            return "rt";
        }

        //每秒一行 [DungeonworldTransition] 诊断;首帧额外带上绑定时的 RT 探针
        private static void TickDrawDiag(float dt, string rtBefore) {
            bool first = diagSeconds <= 0f;
            diagSeconds += Math.Max(dt, 0.0001f);
            if (!first && diagSeconds < 1f) {
                return;
            }
            if (diagSeconds >= 1f) {
                diagSeconds -= 1f;
            }
            string extra = first ? $" rt={rtBefore}" : string.Empty;
            DungeonworldTransitionLog.Mark(
                $"drawn={drawnPath} tick={drawTick} elapsed={realSeconds:F2} progress={travel:F3}"
                + $" gt={lastGtElapsed:F4} dt={lastUsedDt:F4}"
                + $" gameMenu={Main.gameMenu} IsActive={Main.instance?.IsActive}{extra}");
        }
        #endregion

        #region CPU 前景
        //阴影+正文两笔
        private static void DrawText(DynamicSpriteFont font, string text, Vector2 pos, Color color, float scale) {
            Main.spriteBatch.DrawString(font, text, pos + Vector2.One, Color.Black * (color.A / 255f * 0.55f),
                0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            Main.spriteBatch.DrawString(font, text, pos, color,
                0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        //上飘尘埃与烛烬：确定性 hash 伪粒子，相对下降=向上飘
        private static void DrawDust(Texture2D px, int sw, int sh, float uiFade) {
            if (uiFade <= 0.01f) {
                return;
            }
            for (int i = 0; i < DungeonworldLoadTheme.DustCount; i++) {
                float h1 = DungeonworldLoadTheme.Hash01(i * 1.618f + 0.31f);
                float h2 = DungeonworldLoadTheme.Hash01(i * 2.71f + 7.7f);
                float speed = 0.05f + 0.11f * h2;
                float cyc = h1 + realSeconds * speed;
                float yFrac = 1f - (cyc - (float)Math.Floor(cyc));
                float xBase = 0.06f + 0.88f * DungeonworldLoadTheme.Hash01(i * 3.33f + 1.1f);
                float wander = (float)Math.Sin(realSeconds * (0.4f + h1 * 0.5f) + i * 2.399f) * 0.012f;
                float xFrac = xBase + wander;
                //穿过顶光柱时略亮
                float boost = topLight * (float)Math.Exp(-Math.Abs(xFrac - 0.5f) * 7f) * (float)Math.Exp(-yFrac * 2.5f);
                float a = MathHelper.Clamp((0.10f + 0.22f * h1) * (1f + boost * 2f), 0f, 0.8f) * uiFade;
                int size = h2 > 0.72f ? 3 : 2;
                Main.spriteBatch.Draw(px,
                    new Rectangle((int)(xFrac * sw), (int)(yFrac * sh), size, size),
                    DungeonworldLoadTheme.Candle * a);
            }
        }

        //右缘深度计：鎏金竖轨 + 七格铭牌 + 吊坠随深度走
        private static void DrawDepthGauge(Texture2D px, DynamicSpriteFont body, int sw, int sh, float uiFade) {
            if (uiFade <= 0.01f || BandNames == null) {
                return;
            }
            float railX = sw * DungeonworldLoadTheme.RailX;
            float top = sh * DungeonworldLoadTheme.RailTop;
            float bot = sh * DungeonworldLoadTheme.RailBottom;
            float railH = bot - top;
            Color rail = DungeonworldLoadTheme.Gilt * (0.75f * uiFade);

            Main.spriteBatch.Draw(px, new Rectangle((int)railX, (int)top, 1, (int)railH), rail);
            Main.spriteBatch.Draw(px, new Rectangle((int)railX - 3, (int)top, 7, 1), rail);
            Main.spriteBatch.Draw(px, new Rectangle((int)railX - 3, (int)bot, 7, 1), rail);

            for (int i = 0; i < DungeonworldLoadTheme.BandCount; i++) {
                float cellTop = top + railH * i / 7f;
                float cellMid = cellTop + railH / 14f;
                if (i > 0) {
                    Main.spriteBatch.Draw(px, new Rectangle((int)railX - 3, (int)cellTop, 4, 1), rail * 0.8f);
                }
                bool lit = i + 1 <= layerNow;
                float flash = plaqueFlash[i] > 0f ? plaqueFlash[i] / DungeonworldLoadTheme.PlaqueFlashTime : 0f;
                Color nameCol = lit
                    ? Color.Lerp(DungeonworldLoadTheme.Gilt, DungeonworldLoadTheme.GiltHi, flash)
                    : DungeonworldLoadTheme.InkFaint * 0.85f;
                string name = BandNames[i].Value;
                const float scale = 0.8f;
                Vector2 size = body.MeasureString(name) * scale;
                var pos = new Vector2(railX - 12f - size.X, cellMid - size.Y * 0.5f);
                DrawText(body, name, pos, nameCol * uiFade, scale);
                if (flash > 0f) {
                    Main.spriteBatch.Draw(px, new Rectangle((int)railX - 8, (int)cellMid, 5, 1),
                        DungeonworldLoadTheme.GiltHi * (flash * uiFade));
                }
            }

            //吊坠标记（小菱形）
            float py = top + MathHelper.Clamp(depth / 7f, 0f, 1f) * railH;
            Color pend = DungeonworldLoadTheme.GiltHi * uiFade;
            Main.spriteBatch.Draw(px, new Rectangle((int)railX, (int)py - 2, 1, 1), pend * 0.8f);
            Main.spriteBatch.Draw(px, new Rectangle((int)railX - 1, (int)py - 1, 3, 1), pend);
            Main.spriteBatch.Draw(px, new Rectangle((int)railX - 2, (int)py, 5, 1), pend);
            Main.spriteBatch.Draw(px, new Rectangle((int)railX - 1, (int)py + 1, 3, 1), pend);
            Main.spriteBatch.Draw(px, new Rectangle((int)railX, (int)py + 2, 1, 1), pend * 0.8f);

            //弱化的百分比小字挂吊坠旁
            if (DepthLabel != null) {
                string pct = DepthLabel.Format((int)(MathHelper.Clamp(depth / 7f, 0f, 1f) * 100f));
                DrawText(body, pct, new Vector2(railX + 9f, py - 9f),
                    DungeonworldLoadTheme.Parchment * (0.62f * uiFade), 0.78f);
            }
        }

        //播报行：当前层「第X响 · 层名」
        private static void DrawAnnounce(DynamicSpriteFont body, int sw, int sh, float uiFade) {
            if (uiFade <= 0.01f || Tolls == null) {
                return;
            }
            int idx = Math.Clamp(layerNow - 1, 0, DungeonworldLoadTheme.BandCount - 1);
            float flash = plaqueFlash[idx] > 0f ? plaqueFlash[idx] / DungeonworldLoadTheme.PlaqueFlashTime : 0f;
            string line = Tolls[idx].Value;
            Vector2 size = body.MeasureString(line);
            var pos = new Vector2(sw * 0.5f - size.X * 0.5f, sh * 0.735f);
            Color col = Color.Lerp(DungeonworldLoadTheme.Parchment, DungeonworldLoadTheme.GiltHi, flash * 0.8f);
            DrawText(body, line, pos, col * uiFade, 1f);
        }

        //轮换箴言：0.45s 淡入 / 3.8s 驻留 / 0.55s 淡出
        private static void DrawTip(DynamicSpriteFont body, int sw, int sh, float uiFade) {
            if (uiFade <= 0.01f || Tips == null || Tips.Length == 0) {
                return;
            }
            float t = tipTimer;
            float alpha;
            if (t < DungeonworldLoadTheme.TipFadeIn) {
                alpha = t / DungeonworldLoadTheme.TipFadeIn;
            }
            else if (t < DungeonworldLoadTheme.TipFadeIn + DungeonworldLoadTheme.TipHold) {
                alpha = 1f;
            }
            else {
                alpha = 1f - (t - DungeonworldLoadTheme.TipFadeIn - DungeonworldLoadTheme.TipHold)
                    / DungeonworldLoadTheme.TipFadeOut;
            }
            string tip = Tips[tipIndex].Value;
            const float scale = 0.95f;
            Vector2 size = body.MeasureString(tip) * scale;
            var pos = new Vector2(sw * 0.5f - size.X * 0.5f, sh * 0.255f);
            DrawText(body, tip, pos, DungeonworldLoadTheme.Parchment * (alpha * 0.85f * uiFade), scale);
        }

        //底部状态行：生成 Message → Main.statusText → 方向垫底文案，带省略号动画
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
            DrawText(body, full, pos, DungeonworldLoadTheme.Parchment * (0.75f * fade), scale);
        }
        #endregion
    }
}
