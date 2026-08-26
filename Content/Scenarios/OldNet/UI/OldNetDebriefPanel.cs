using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace CalamityOverhaul.Content.Scenarios.OldNet.UI
{
    /// <summary>弹出方式：决定战报屏的落款配色与损失呈现</summary>
    internal enum OldNetExitKind : byte
    {
        /// <summary>登出终端安全断链</summary>
        SafeLogout,
        /// <summary>RAM 耗尽烧断</summary>
        RamBurnout,
        /// <summary>构念崩解（死亡）</summary>
        Death,
    }

    /// <summary>
    /// 深潜战报数据。<b>静态缓存跨世界搬运</b>，ModPlayer 字段不跨世界切换存续，
    /// 弹出时写入，回主世界 OnEnterWorld 一次性消费即清（CybCourse._grantMewtwoOnExit 同款手法；
    /// MP 多玩家共用静态是已知反模式，MP 化时改 per-player 传递 TODO）
    /// </summary>
    internal sealed class OldNetDebriefReport
    {
        internal OldNetExitKind Kind;
        internal int MaxDepthCols;
        internal int HarvestCount;
        internal int SettledTotal;
        internal int HuntedCount;
        internal int DiveTicks;
        /// <summary>弹出时未铭刻而作废的碎片数（损失要被看见）</summary>
        internal int LostPending;
        //──── 深潜评级（2.1）────
        internal int Score;
        internal int GradeIndex;
        internal OldNetStyleFlags Styles;
        /// <summary>本潜刷新了历史最佳</summary>
        internal bool NewRecord;
        /// <summary>快照时点的历史最佳（含本潜刷新）</summary>
        internal int BestScore;
        internal int BestGradeIndex;
    }

    /// <summary>
    /// 深潜战报屏（2026-08 重做）：回主世界后的一屏总结。
    /// 底板走 OldNetHud.fx TechPanel 暗钢切角（域内同一张皮，缺编退实底）；
    /// 布局全量测算流式下排，面板高度随内容伸缩，杜绝写死偏移互撞；
    /// 内容按时间轴分段揭示：五行统计逐行解码 → 评分滚数 → 风格加成逐条入账 →
    /// 评级章砸落定妆 → 落款带与按键。揭示未完点击任意处直接快进到终态
    /// </summary>
    internal class OldNetDebriefPanel : ModSystem, ILocalizedModType
    {
        public string LocalizationCategory => "UI";

        public static LocalizedText DebriefSubtitle { get; private set; }
        public static LocalizedText DebriefDepth { get; private set; }
        public static LocalizedText DebriefHarvest { get; private set; }
        public static LocalizedText DebriefSettled { get; private set; }
        public static LocalizedText DebriefHunted { get; private set; }
        public static LocalizedText DebriefTime { get; private set; }
        public static LocalizedText DebriefExitSafe { get; private set; }
        public static LocalizedText DebriefExitBurnout { get; private set; }
        public static LocalizedText DebriefExitDeath { get; private set; }
        public static LocalizedText DebriefLost { get; private set; }
        public static LocalizedText DebriefContinue { get; private set; }
        public static LocalizedText DebriefMechHint { get; private set; }
        //深潜评级（2.1）
        public static LocalizedText DebriefScore { get; private set; }
        public static LocalizedText DebriefGrade { get; private set; }
        public static LocalizedText DebriefBest { get; private set; }
        public static LocalizedText DebriefNewRecord { get; private set; }
        public static LocalizedText StyleGhost { get; private set; }
        public static LocalizedText StyleHeat { get; private set; }
        public static LocalizedText StyleHotExtract { get; private set; }
        public static LocalizedText StyleDragnet { get; private set; }

        public override void SetStaticDefaults() {
            DebriefSubtitle = this.GetLocalization(nameof(DebriefSubtitle), () => "链路战报");
            DebriefDepth = this.GetLocalization(nameof(DebriefDepth), () => "最远离墙");
            DebriefHarvest = this.GetLocalization(nameof(DebriefHarvest), () => "采集节点");
            DebriefSettled = this.GetLocalization(nameof(DebriefSettled), () => "铭刻碎片");
            DebriefHunted = this.GetLocalization(nameof(DebriefHunted), () => "被追猎");
            DebriefTime = this.GetLocalization(nameof(DebriefTime), () => "深潜用时");
            DebriefExitSafe = this.GetLocalization(nameof(DebriefExitSafe), () => "安全断链");
            DebriefExitBurnout = this.GetLocalization(nameof(DebriefExitBurnout), () => "RAM 耗尽，链路烧断");
            DebriefExitDeath = this.GetLocalization(nameof(DebriefExitDeath), () => "构念崩解，链路烧断");
            DebriefLost = this.GetLocalization(nameof(DebriefLost), () => "{0} 枚未铭刻碎片已烧毁");
            DebriefContinue = this.GetLocalization(nameof(DebriefContinue), () => "继续");
            DebriefMechHint = this.GetLocalization(nameof(DebriefMechHint),
                () => "未铭刻的收获只活在链路里，经中继站或登出终端铭刻后才真正属于你");
            DebriefScore = this.GetLocalization(nameof(DebriefScore), () => "评分");
            DebriefGrade = this.GetLocalization(nameof(DebriefGrade), () => "评级");
            DebriefBest = this.GetLocalization(nameof(DebriefBest), () => "历史最佳 {0}");
            DebriefNewRecord = this.GetLocalization(nameof(DebriefNewRecord), () => "新纪录");
            StyleGhost = this.GetLocalization(nameof(StyleGhost), () => "幽灵潜行");
            StyleHeat = this.GetLocalization(nameof(StyleHeat), () => "高热生还");
            StyleHotExtract = this.GetLocalization(nameof(StyleHotExtract), () => "热断链");
            StyleDragnet = this.GetLocalization(nameof(StyleDragnet), () => "收网撤离");
        }

        //════════ 静态战报缓存 ════════

        private static OldNetDebriefReport pending;

        /// <summary>
        /// 弹出时写入（先于清账快照）。同一次深潜首个弹出原因为准，不被覆盖。
        /// 评级在此结算并写跨潜战绩（与快照同守卫=每潜恰记一次；快照先于离世，director 会话仍有效）
        /// </summary>
        internal static void CacheReport(OldNetPlayer session, OldNetExitKind kind) {
            if (pending != null) {
                return;
            }
            (int score, int gradeIndex, OldNetStyleFlags styles) = OldNetRating.Compute(session, kind);
            //跨潜战绩持久化（随玩家存档；session.Player 即归属端，per-player 语义天然正确）
            var record = session.Player.GetModPlayer<Narrative.Data.StoryPlayer>()
                .Get<Narrative.Data.Modules.OldNetRecordData>();
            record.TotalDives++;
            record.TotalSettledShards += session.SettledTotal;
            bool newRecord = score > record.BestScore;
            if (newRecord) {
                record.BestScore = score;
                record.BestGradeIndex = gradeIndex;
            }
            pending = new OldNetDebriefReport {
                Kind = kind,
                MaxDepthCols = session.MaxDepthCols,
                HarvestCount = session.HarvestCount,
                SettledTotal = session.SettledTotal,
                HuntedCount = session.HuntedCount,
                DiveTicks = session.DiveTicks,
                LostPending = session.PendingTotal,
                Score = score,
                GradeIndex = gradeIndex,
                Styles = styles,
                NewRecord = newRecord,
                BestScore = record.BestScore,
                BestGradeIndex = record.BestGradeIndex,
            };
        }

        /// <summary>回主世界的消费点（OldNetPlayer.OnEnterWorld 非旧网分支），一次性即清</summary>
        internal static void ConsumePending() {
            if (pending == null || Main.dedServ) {
                return;
            }
            Show(pending);
            pending = null;
        }

#if DEBUG
        /// <summary>视觉验收入口（/oldnet report [safe|burn|death]）：合成假战报直接弹屏</summary>
        internal static void ShowPreview(OldNetExitKind kind) {
            bool bad = kind != OldNetExitKind.SafeLogout;
            Show(new OldNetDebriefReport {
                Kind = kind,
                MaxDepthCols = 1926,
                HarvestCount = 13,
                SettledTotal = bad ? 0 : 21,
                HuntedCount = 2,
                DiveTicks = 527 * 60,
                LostPending = bad ? 5 : 0,
                Score = bad ? 435 : 1520,
                GradeIndex = bad ? OldNetRating.GradeC : OldNetRating.GradeA,
                Styles = bad ? OldNetStyleFlags.Ghost
                    : OldNetStyleFlags.Ghost | OldNetStyleFlags.HeatSurvivor,
                NewRecord = true,
                BestScore = bad ? 435 : 1520,
                BestGradeIndex = bad ? OldNetRating.GradeC : OldNetRating.GradeA,
            });
        }
#endif

        //════════ 相位机 ════════

        private enum Phase { Hidden, FadeIn, Idle, FadeOut }

        private const int PanelW = 508;
        /// <summary>内容左右内缩</summary>
        private const int Inset = 46;
        private const int ContentW = PanelW - Inset * 2;
        private const float RowH = 29f;
        private const int RowCount = 5;
        private const float StyleRowH = 26f;
        /// <summary>评级章框边长；左列宽 = 内容宽让出章框与间隙</summary>
        private const int StampSize = 96;
        private const int LeftColW = ContentW - StampSize - 26;
        private const float VerdictBandH = 36f;
        private const int BtnW = 172;
        private const int BtnH = 38;

        private const string TitleText = "LINK REPORT";
        private const float TitleScale = 0.62f;
        private const float ScoreCountDur = 0.55f;
        private const float StampDur = 0.28f;

        private static readonly Color ColdCyan = new(140, 200, 210);
        private static readonly Color EmberRed = new(235, 64, 44);
        private static readonly Color Amber = new(255, 170, 60);
        private static readonly Color TextDim = new(150, 160, 175);
        private static readonly Color PanelBg = new(8, 12, 16);

        private static Phase phase = Phase.Hidden;
        private static float alpha;
        private static float idleTimer;
        private static bool prevMouseLeft;
        private static bool prevHover;
        private static OldNetDebriefReport report;
        private static Rectangle continueRect = Rectangle.Empty;
        private static Rectangle panelRect = Rectangle.Empty;

        //──── 揭示时间轴（Show 时按内容预计算，秒；idleTimer 驱动）────
        private static readonly List<(LocalizedText name, int bonus)> styleEntries = [];
        private static float tScore, tScoreDone, tBest, tStamp, tFooter, tDone;
        //一次性揭示节点（音效去重）与落章白闪
        private static int seenRows, seenStyles;
        private static bool stampLanded, verdictCued;
        private static float stampFlash;

        public static bool Visible => phase != Phase.Hidden;

        private static float RowRevealAt(int i) => 0.08f + i * 0.10f;
        private static float StyleRevealAt(int i) => tScoreDone + 0.12f + i * 0.15f;

        internal static void Show(OldNetDebriefReport data) {
            report = data;
            phase = Phase.FadeIn;
            alpha = 0f;
            idleTimer = 0f;
            seenRows = 0;
            seenStyles = 0;
            stampLanded = false;
            verdictCued = false;
            stampFlash = 0f;
            //吞掉入场残留按压，跳过/按键都要求新的按下沿
            prevMouseLeft = true;
            prevHover = false;

            styleEntries.Clear();
            void Add(OldNetStyleFlags flag, LocalizedText name) {
                if ((data.Styles & flag) != 0) {
                    styleEntries.Add((name, OldNetRating.StyleBonus(flag)));
                }
            }
            Add(OldNetStyleFlags.Ghost, StyleGhost);
            Add(OldNetStyleFlags.HeatSurvivor, StyleHeat);
            Add(OldNetStyleFlags.HotExtract, StyleHotExtract);
            Add(OldNetStyleFlags.DragnetEscape, StyleDragnet);

            //时间轴：行揭示 → 滚分 → 风格入账 → 历史最佳 → 落章 → 落款
            tScore = RowRevealAt(RowCount) + 0.12f;
            tScoreDone = tScore + ScoreCountDur;
            float styleEnd = styleEntries.Count > 0
                ? StyleRevealAt(styleEntries.Count - 1) + 0.15f
                : tScoreDone;
            tBest = styleEnd + 0.16f;
            tStamp = tBest + 0.26f;
            tFooter = tStamp + StampDur + 0.26f;
            tDone = tFooter + 0.30f;

            SoundEngine.PlaySound(SoundID.MenuOpen with { Pitch = -0.3f });
        }

        internal static void Hide() {
            phase = Phase.Hidden;
            alpha = 0f;
            report = null;
            styleEntries.Clear();
            continueRect = Rectangle.Empty;
            panelRect = Rectangle.Empty;
        }

        public override void OnWorldUnload() => Hide();

        //UI 空间口径：布局与命中共用
        private static float UIScreenW => PlayerInput.RealScreenWidth / Main.UIScale;
        private static float UIScreenH => PlayerInput.RealScreenHeight / Main.UIScale;
        private static Point UIMouse => new((int)(PlayerInput.MouseX / Main.UIScale),
            (int)(PlayerInput.MouseY / Main.UIScale));

        public override void UpdateUI(GameTime gameTime) {
            if (Main.dedServ || Main.gameMenu || phase == Phase.Hidden) {
                return;
            }
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            switch (phase) {
                case Phase.FadeIn:
                    alpha = MathHelper.Lerp(alpha, 1f, 0.14f);
                    //揭示时间轴与淡入并行，不让玩家干等
                    idleTimer += dt;
                    TickRevealCues();
                    HandleClicks();
                    if (alpha > 0.985f) {
                        alpha = 1f;
                        phase = Phase.Idle;
                    }
                    break;
                case Phase.Idle:
                    idleTimer += dt;
                    TickRevealCues();
                    HandleClicks();
                    break;
                case Phase.FadeOut:
                    alpha = MathHelper.Lerp(alpha, 0f, 0.18f);
                    if (alpha < 0.02f) {
                        Hide();
                    }
                    break;
            }
            stampFlash = MathF.Max(0f, stampFlash - dt * 2.6f);

            if (phase != Phase.Hidden && panelRect != Rectangle.Empty) {
                Main.LocalPlayer.mouseInterface = true;
            }
        }

        //揭示节点的一次性声效：逐行电传 tick / 风格入账 tick / 落章锁定 / 烧断落款故障音
        private static void TickRevealCues() {
            int rowsNow = 0;
            for (int i = 0; i < RowCount; i++) {
                if (idleTimer >= RowRevealAt(i)) {
                    rowsNow++;
                }
            }
            if (rowsNow > seenRows) {
                seenRows = rowsNow;
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.3f, Pitch = 0.25f });
            }
            int stylesNow = 0;
            for (int i = 0; i < styleEntries.Count; i++) {
                if (idleTimer >= StyleRevealAt(i)) {
                    stylesNow++;
                }
            }
            if (stylesNow > seenStyles) {
                seenStyles = stylesNow;
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.35f, Pitch = 0.55f });
            }
            if (!stampLanded && idleTimer >= tStamp + StampDur) {
                stampLanded = true;
                stampFlash = 1f;
                SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.85f, Pitch = -0.45f });
            }
            if (!verdictCued && idleTimer >= tFooter) {
                verdictCued = true;
                if (report.Kind != OldNetExitKind.SafeLogout) {
                    SoundEngine.PlaySound(CWRSound.Fault with { Volume = 0.3f, Pitch = -0.2f });
                }
            }
        }

        private static void HandleClicks() {
            bool mouseDown = Main.mouseLeft;
            bool clicked = mouseDown && !prevMouseLeft;
            prevMouseLeft = mouseDown;

            //按键悬停沿 tick（与上帧比较，不逐帧重放）
            bool hover = idleTimer >= tDone && continueRect.Contains(UIMouse);
            if (hover && !prevHover) {
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.4f });
            }
            prevHover = hover;

            if (!clicked) {
                return;
            }
            //揭示未完：任意点击快进到终态（拆礼物可跳过），一次性声效静默补记
            if (idleTimer < tDone) {
                idleTimer = tDone;
                seenRows = RowCount;
                seenStyles = styleEntries.Count;
                stampLanded = true;
                verdictCued = true;
                stampFlash = 0.6f;
                Main.mouseLeft = false;
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.5f, Pitch = -0.1f });
                return;
            }
            if (continueRect.Contains(UIMouse)) {
                Main.mouseLeft = false;
                phase = Phase.FadeOut;
                SoundEngine.PlaySound(SoundID.MenuClose);
            }
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers) {
            if (phase == Phase.Hidden || alpha < 0.01f || report == null) {
                return;
            }
            int idx = layers.FindIndex(l => l.Name == "Vanilla: Mouse Text");
            if (idx == -1) {
                return;
            }
            layers.Insert(idx, new LegacyGameInterfaceLayer("CWRMod: OldNet Debrief Panel",
                delegate {
                    DrawOverlay(Main.spriteBatch);
                    return true;
                }, InterfaceScaleType.UI));
        }

        //════════ 布局（全量测算流式下排；偏移均相对面板顶，面板高随内容伸缩）════════

        private struct Layout
        {
            public int PanelH;
            public float TitleY, SubY;
            public float Div1Y, Div2Y, Div3Y;
            public float RowsY;
            public float ScoreY, StyleY, BestY, StampY;
            public float VerdictY, LostY, HintY, ButtonY;
            public float HintLineH;
            public string[] HintLines;
        }

        private static Layout ComputeLayout(DynamicSpriteFont title, DynamicSpriteFont body, bool burned) {
            Layout ly = new();
            float titleH = title.MeasureString(TitleText).Y * TitleScale;
            float bodyLineH = body.MeasureString(DebriefSubtitle.Value).Y;

            ly.TitleY = 22f;
            ly.SubY = ly.TitleY + titleH + 4f;
            ly.Div1Y = ly.SubY + bodyLineH * 0.85f + 12f;
            ly.RowsY = ly.Div1Y + 15f;
            ly.Div2Y = ly.RowsY + RowCount * RowH + 6f;

            //评级章分区：左列（评分/风格/最佳）与右侧章框各自量高，取大者
            ly.ScoreY = ly.Div2Y + 16f;
            ly.StyleY = ly.ScoreY + 38f;
            ly.BestY = ly.StyleY + styleEntries.Count * StyleRowH + 8f;
            float leftColBottom = ly.BestY + 26f;
            ly.StampY = ly.ScoreY + 2f;
            float stampBottom = ly.StampY + StampSize + 6f + bodyLineH * 0.7f;
            ly.Div3Y = MathF.Max(leftColBottom, stampBottom) + 12f;

            ly.VerdictY = ly.Div3Y + 16f;
            float footCursor = ly.VerdictY + VerdictBandH;
            ly.LostY = -1f;
            if (burned && report.LostPending > 0) {
                ly.LostY = footCursor + 8f;
                footCursor = ly.LostY + bodyLineH * 0.8f + 2f;
            }
            ly.HintLines = VaultUtils.WrapTextArray(DebriefMechHint.Value, body, ContentW, 0.7f);
            ly.HintLineH = bodyLineH * 0.7f + 3f;
            ly.HintY = footCursor + 12f;
            ly.ButtonY = ly.HintY + ly.HintLines.Length * ly.HintLineH + 14f;
            ly.PanelH = (int)(ly.ButtonY + BtnH + 22f);
            return ly;
        }

        //════════ 绘制 ════════

        private static void DrawOverlay(SpriteBatch sb) {
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null || px.IsDisposed || report == null) {
                return;
            }
            DynamicSpriteFont title = FontAssets.DeathText.Value;
            DynamicSpriteFont body = FontAssets.MouseText.Value;
            bool burned = report.Kind != OldNetExitKind.SafeLogout;
            Color accent = burned ? EmberRed : ColdCyan;
            Layout ly = ComputeLayout(title, body, burned);

            //全屏压暗（材料纯色底，合法矩形用途）
            sb.Draw(px, new Rectangle(0, 0, (int)UIScreenW, (int)UIScreenH),
                new Color(0, 4, 8) * (0.62f * alpha));

            float slideY = (1f - alpha) * 26f;
            int x0 = (int)((UIScreenW - PanelW) * 0.5f);
            int y0 = (int)((UIScreenH - ly.PanelH) * 0.5f + slideY);
            panelRect = new Rectangle(x0, y0, PanelW, ly.PanelH);

            DrawPlate(sb, px);
            DrawChrome(sb, px, accent, burned);
            DrawContent(sb, px, title, body, ly, accent, burned);
        }

        //底板：OldNetHud.fx TechPanel 暗钢切角（域内同一张皮），缺编 CPU 实底兜底
        private static void DrawPlate(SpriteBatch sb, Texture2D px) {
            Effect fx = EffectLoader.OldNetHud?.Value;
            if (fx == null) {
                sb.Draw(px, panelRect, PanelBg * (0.94f * alpha));
                return;
            }
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.UIScaleMatrix);
            //共享参数化 shader：每次调用全参数重设
            fx.CurrentTechnique = fx.Techniques["TechPanel"];
            fx.Parameters["uTime"]?.SetValue(idleTimer);
            fx.Parameters["uPanelSize"]?.SetValue(new Vector2(panelRect.Width, panelRect.Height));
            fx.Parameters["uFrac"]?.SetValue(0f);
            fx.Parameters["uTier"]?.SetValue(0f);
            fx.Parameters["uAlpha"]?.SetValue(alpha);
            fx.CurrentTechnique.Passes[0].Apply();
            sb.Draw(px, panelRect, Color.White);
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);
        }

        //外沿：低调 1px 框 + 顶缘受光线 + 面板级四角角标 + 扫描线 + 烧断撕裂残迹
        private static void DrawChrome(SpriteBatch sb, Texture2D px, Color accent, bool burned) {
            int x0 = panelRect.X;
            int y0 = panelRect.Y;
            int w = panelRect.Width;
            int h = panelRect.Height;

            DrawBorder(sb, px, panelRect, accent * (0.30f * alpha));
            sb.Draw(px, new Rectangle(x0 + 10, y0 + 1, w - 20, 1), accent * (0.8f * alpha));
            DrawCornerBrackets(sb, px, panelRect, 16, accent * (0.75f * alpha));

            //慢速横向扫描线：面板活着
            float scanPhase = idleTimer * 0.20f % 1f;
            int scanY = y0 + (int)(scanPhase * (h - 2)) + 1;
            sb.Draw(px, new Rectangle(x0 + 1, scanY, w - 2, 1), accent * (0.10f * alpha));

            //烧断档：约每 2.6s 一瞬横向撕裂残迹（EjectFlash 语汇的低强度余震）
            if (burned) {
                float cycle = idleTimer % 2.6f;
                if (cycle < 0.09f) {
                    int salt = (int)(idleTimer / 2.6f);
                    int ty = y0 + 14 + (int)(Hash01(salt * 3 + 1) * (h - 28));
                    sb.Draw(px, new Rectangle(x0 + 2, ty, w - 4, 2), Color.White * (0.16f * alpha));
                    sb.Draw(px, new Rectangle(x0 + 2, ty + 2, w - 4, 1), Color.Black * (0.35f * alpha));
                    int ty2 = y0 + 14 + (int)(Hash01(salt * 3 + 2) * (h - 28));
                    sb.Draw(px, new Rectangle(x0 + 2, ty2, w - 4, 1), EmberRed * (0.25f * alpha));
                }
            }
        }

        private static void DrawContent(SpriteBatch sb, Texture2D px, DynamicSpriteFont title,
            DynamicSpriteFont body, Layout ly, Color accent, bool burned) {
            int x0 = panelRect.X;
            int y0 = panelRect.Y;

            //左上机器铭牌（加载屏同语汇）
            Utils.DrawBorderString(sb, "// OLDNET LINK", new Vector2(x0 + 14f, y0 + 8f),
                TextDim * (0.45f * alpha), 0.7f);

            //标题：诊断字样走机器英文，副题走本地化
            Vector2 titleSz = title.MeasureString(TitleText) * TitleScale;
            Vector2 titlePos = new(x0 + (PanelW - titleSz.X) * 0.5f, y0 + ly.TitleY);
            sb.DrawString(title, TitleText, titlePos + new Vector2(2f, 2f), Color.Black * (0.6f * alpha),
                0f, Vector2.Zero, TitleScale, SpriteEffects.None, 0f);
            sb.DrawString(title, TitleText, titlePos, accent * alpha,
                0f, Vector2.Zero, TitleScale, SpriteEffects.None, 0f);

            string sub = DebriefSubtitle.Value;
            Vector2 subSz = body.MeasureString(sub) * 0.85f;
            Utils.DrawBorderString(sb, sub, new Vector2(x0 + (PanelW - subSz.X) * 0.5f, y0 + ly.SubY),
                TextDim * alpha, 0.85f);

            DrawDivider(sb, px, y0 + ly.Div1Y, accent);
            DrawStatRows(sb, px, body, ly, burned);
            DrawDivider(sb, px, y0 + ly.Div2Y, accent);
            DrawGradeBlock(sb, px, title, body, ly);
            DrawDivider(sb, px, y0 + ly.Div3Y, accent);
            DrawFooter(sb, px, body, ly, accent, burned);
        }

        //分隔线：细线 + 两端收口刻齿
        private static void DrawDivider(SpriteBatch sb, Texture2D px, float y, Color accent) {
            int x = panelRect.X + Inset - 12;
            int w = ContentW + 24;
            sb.Draw(px, new Rectangle(x, (int)y, w, 1), accent * (0.35f * alpha));
            sb.Draw(px, new Rectangle(x, (int)y - 1, 1, 3), accent * (0.55f * alpha));
            sb.Draw(px, new Rectangle(x + w - 1, (int)y - 1, 1, 3), accent * (0.55f * alpha));
        }

        //五行统计：逐行解码揭示（标签左滑入、数值右对齐，解码期数字打乱）
        private static void DrawStatRows(SpriteBatch sb, Texture2D px, DynamicSpriteFont body,
            Layout ly, bool burned) {
            int seconds = report.DiveTicks / 60;
            (string label, string value, bool voided)[] rows = [
                (DebriefDepth.Value, $"{report.MaxDepthCols}", false),
                (DebriefHarvest.Value, $"{report.HarvestCount}", false),
                //烧断且铭刻为 0：灰红划销，损失要被看见
                (DebriefSettled.Value, $"{report.SettledTotal}", burned && report.SettledTotal == 0),
                (DebriefHunted.Value, $"{report.HuntedCount}", false),
                (DebriefTime.Value, $"{seconds / 60:D2}:{seconds % 60:D2}", false),
            ];
            for (int i = 0; i < rows.Length; i++) {
                float t = Ease01((idleTimer - RowRevealAt(i)) / 0.16f);
                if (t <= 0f) {
                    continue;
                }
                float y = panelRect.Y + ly.RowsY + i * RowH;
                float a = alpha * t;
                float slide = (1f - t) * 14f;
                Utils.DrawBorderString(sb, rows[i].label,
                    new Vector2(panelRect.X + Inset - slide, y), TextDim * a, 0.85f);

                string val = t < 0.55f ? ScrambleDigits(rows[i].value, i * 31) : rows[i].value;
                Vector2 vsz = body.MeasureString(val) * 0.95f;
                Vector2 vpos = new(panelRect.Right - Inset - vsz.X + slide, y - 2f);
                Color vcol = rows[i].voided ? Color.Lerp(EmberRed, TextDim, 0.45f) : Color.White * 0.92f;
                Utils.DrawBorderString(sb, val, vpos, vcol * a, 0.95f);

                //作废斜划：过字面视觉中心，斜线对居中误差不敏感
                if (rows[i].voided && t >= 0.55f) {
                    Vector2 mid = vpos + vsz * new Vector2(0.5f, 0.42f);
                    float len = vsz.X + 14f;
                    sb.Draw(px, mid, null, Color.Lerp(EmberRed, TextDim, 0.25f) * (0.9f * a),
                        -0.38f, new Vector2(px.Width * 0.5f, px.Height * 0.5f),
                        new Vector2(len / px.Width, 2f / px.Height), SpriteEffects.None, 0f);
                }
            }
        }

        //评级章分区：评分滚数（左）+ 风格加成逐条入账 + 历史最佳，章框砸落（右）
        private static void DrawGradeBlock(SpriteBatch sb, Texture2D px, DynamicSpriteFont title,
            DynamicSpriteFont body, Layout ly) {
            int x0 = panelRect.X;
            int y0 = panelRect.Y;
            float xLeft = x0 + Inset;
            float colRight = xLeft + LeftColW;

            //评分行：标签实测排位（不写死偏移），数值 0→终值缓出滚数
            float scoreT = Ease01((idleTimer - tScore) / 0.18f);
            if (scoreT > 0f) {
                float a = alpha * scoreT;
                string scoreLabel = DebriefScore.Value;
                Utils.DrawBorderString(sb, scoreLabel, new Vector2(xLeft, y0 + ly.ScoreY + 6f),
                    TextDim * a, 0.85f);
                float labelW = body.MeasureString(scoreLabel).X * 0.85f;
                float countT = Ease01((idleTimer - tScore) / ScoreCountDur);
                int shown = (int)MathF.Round(report.Score * EaseOutCubic(countT));
                Color numCol = countT < 1f
                    ? Color.Lerp(Color.White, OldNetRating.GradeColor(report.GradeIndex), 0.35f)
                    : Color.White;
                Utils.DrawBorderString(sb, $"{shown}",
                    new Vector2(xLeft + labelW + 18f, y0 + ly.ScoreY), numCol * (0.95f * a), 1.2f);
            }

            //风格加成：名目左、+N 右对齐，一行一条（长串横贯与章框相撞的旧病根治）
            for (int i = 0; i < styleEntries.Count; i++) {
                float t = Ease01((idleTimer - StyleRevealAt(i)) / 0.15f);
                if (t <= 0f) {
                    continue;
                }
                float a = alpha * t;
                float y = y0 + ly.StyleY + i * StyleRowH;
                (LocalizedText name, int bonus) = styleEntries[i];
                Utils.DrawBorderString(sb, name.Value, new Vector2(xLeft + 12f, y), Amber * (0.9f * a), 0.8f);
                string bonusText = $"+{bonus}";
                Vector2 bsz = body.MeasureString(bonusText) * 0.85f;
                Utils.DrawBorderString(sb, bonusText,
                    new Vector2(colRight - bsz.X + (1f - t) * 10f, y - 1f), Amber * a, 0.85f);
            }

            //历史最佳 + 新纪录闪（新纪录与落章同拍揭晓）
            float bestT = Ease01((idleTimer - tBest) / 0.18f);
            if (bestT > 0f) {
                float a = alpha * bestT;
                string best = DebriefBest.Format(
                    $"{OldNetRating.Letter(report.BestGradeIndex)} {report.BestScore}");
                Utils.DrawBorderString(sb, best, new Vector2(xLeft, y0 + ly.BestY),
                    TextDim * (0.9f * a), 0.8f);
                if (report.NewRecord && stampLanded) {
                    float flash = 0.65f + 0.35f * MathF.Sin(idleTimer * 7f);
                    float bw = body.MeasureString(best).X * 0.8f;
                    Utils.DrawBorderString(sb, DebriefNewRecord.Value,
                        new Vector2(xLeft + bw + 12f, y0 + ly.BestY), EmberRed * (flash * a), 0.8f);
                }
            }

            DrawStamp(sb, px, title, body, ly);
        }

        //评级章：放大砸落定妆（角标框自外收拢 + 落章白闪 + 微倾手戳感 + SoftGlow 底光）
        private static void DrawStamp(SpriteBatch sb, Texture2D px, DynamicSpriteFont title,
            DynamicSpriteFont body, Layout ly) {
            float t = Ease01((idleTimer - tStamp) / StampDur);
            if (t <= 0f) {
                return;
            }
            var frame = new Rectangle(panelRect.X + PanelW - Inset - StampSize,
                (int)(panelRect.Y + ly.StampY), StampSize, StampSize);
            Color gradeCol = OldNetRating.GradeColor(report.GradeIndex);
            float a = alpha * (0.3f + 0.7f * t);

            int spread = (int)((1f - t) * 14f);
            var frameSpread = new Rectangle(frame.X - spread, frame.Y - spread,
                frame.Width + spread * 2, frame.Height + spread * 2);
            DrawCornerBrackets(sb, px, frameSpread, 18, gradeCol * (0.8f * a));

            //字后底光（SoftGlow A=0 加色，亮层合法路径）
            Vector2 center = frame.Center.ToVector2();
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null && !glow.IsDisposed) {
                Color gl = gradeCol * ((0.18f + stampFlash * 0.4f) * a);
                gl.A = 0;
                sb.Draw(glow, center, null, gl, 0f, glow.Size() * 0.5f,
                    StampSize * 1.5f / glow.Width, SpriteEffects.None, 0f);
            }

            string letter = OldNetRating.Letter(report.GradeIndex);
            Vector2 lsz = title.MeasureString(letter);
            float fit = (StampSize - 26f) / MathF.Max(lsz.Y, 1f);
            float punch = 1f + 1.5f * (1f - t) * (1f - t);
            float scale = fit * punch;
            float rot = (Hash01(report.Score * 7 + report.GradeIndex) - 0.5f) * 0.12f;

            sb.DrawString(title, letter, center + new Vector2(3f, 3f), Color.Black * (0.6f * a * t),
                rot, lsz * 0.5f, scale, SpriteEffects.None, 0f);
            sb.DrawString(title, letter, center, gradeCol * a,
                rot, lsz * 0.5f, scale, SpriteEffects.None, 0f);
            if (stampFlash > 0f) {
                sb.DrawString(title, letter, center, Color.White * (stampFlash * 0.8f * alpha),
                    rot, lsz * 0.5f, scale * (1f + stampFlash * 0.04f), SpriteEffects.None, 0f);
            }

            string gradeLabel = DebriefGrade.Value;
            Vector2 glSz = body.MeasureString(gradeLabel) * 0.7f;
            Utils.DrawBorderString(sb, gradeLabel,
                new Vector2(frame.X + (frame.Width - glSz.X) * 0.5f, frame.Bottom + 6f),
                TextDim * (0.75f * a), 0.7f);
        }

        //落款带（弹出方式）+ 损失行 + 机制注脚（换行）+ CONTINUE 键
        private static void DrawFooter(SpriteBatch sb, Texture2D px, DynamicSpriteFont body,
            Layout ly, Color accent, bool burned) {
            float t = Ease01((idleTimer - tFooter) / 0.22f);
            if (t <= 0f) {
                continueRect = Rectangle.Empty;
                return;
            }
            float a = alpha * t;
            int x0 = panelRect.X;
            int y0 = panelRect.Y;

            //落款带：实底 + 两缘竖条 + 居中判词（烧断档微闪 + 低频白残影）
            var band = new Rectangle(x0 + Inset, (int)(y0 + ly.VerdictY), ContentW, (int)VerdictBandH);
            float bandFlicker = burned ? 0.9f + 0.1f * MathF.Sin(idleTimer * 9.2f) : 1f;
            sb.Draw(px, band, accent * (0.10f * a * bandFlicker));
            sb.Draw(px, new Rectangle(band.X, band.Y, 3, band.Height), accent * (0.8f * a));
            sb.Draw(px, new Rectangle(band.Right - 3, band.Y, 3, band.Height), accent * (0.8f * a));

            string exitText = report.Kind switch {
                OldNetExitKind.RamBurnout => DebriefExitBurnout.Value,
                OldNetExitKind.Death => DebriefExitDeath.Value,
                _ => DebriefExitSafe.Value,
            };
            Vector2 esz = body.MeasureString(exitText) * 0.95f;
            Vector2 epos = new(x0 + (PanelW - esz.X) * 0.5f,
                band.Y + (VerdictBandH - esz.Y) * 0.5f + 2f);
            if (burned && Hash01((int)(idleTimer * 6f)) > 0.86f) {
                Utils.DrawBorderString(sb, exitText, epos + new Vector2(1.5f, 0f),
                    Color.White * (0.18f * a), 0.95f);
            }
            Utils.DrawBorderString(sb, exitText, epos, accent * a, 0.95f);

            if (ly.LostY >= 0f) {
                string lost = DebriefLost.Format(report.LostPending);
                Vector2 lsz = body.MeasureString(lost) * 0.8f;
                Utils.DrawBorderString(sb, lost,
                    new Vector2(x0 + (PanelW - lsz.X) * 0.5f, y0 + ly.LostY),
                    Color.Lerp(EmberRed, TextDim, 0.35f) * a, 0.8f);
            }

            //机制注脚：铭刻语义的常驻解释（首潜引导的收尾复读），换行居中
            for (int i = 0; i < ly.HintLines.Length; i++) {
                Vector2 hsz = body.MeasureString(ly.HintLines[i]) * 0.7f;
                Utils.DrawBorderString(sb, ly.HintLines[i],
                    new Vector2(x0 + (PanelW - hsz.X) * 0.5f, y0 + ly.HintY + i * ly.HintLineH),
                    TextDim * (0.75f * a), 0.7f);
            }

            //CONTINUE 键（揭示完成后才可点，悬停角标外扩）
            continueRect = new Rectangle(x0 + (PanelW - BtnW) / 2, (int)(y0 + ly.ButtonY), BtnW, BtnH);
            bool hover = idleTimer >= tDone && continueRect.Contains(UIMouse);
            Color btnAccent = hover ? accent : accent * 0.55f;
            sb.Draw(px, continueRect, PanelBg * (0.9f * a));
            DrawBorder(sb, px, continueRect, btnAccent * a);
            if (hover) {
                DrawCornerBrackets(sb, px, new Rectangle(continueRect.X - 4, continueRect.Y - 4,
                    continueRect.Width + 8, continueRect.Height + 8), 9, accent * a);
            }
            string btn = DebriefContinue.Value;
            Vector2 bsz = body.MeasureString(btn) * 0.9f;
            Utils.DrawBorderString(sb, btn,
                new Vector2(continueRect.X + (BtnW - bsz.X) * 0.5f,
                    continueRect.Y + (BtnH - bsz.Y) * 0.5f + 2f),
                (hover ? Color.White : TextDim) * a, 0.9f);
        }

        //════════ 小工具 ════════

        //解码期数字打乱（仅替换数字字符，分隔符原样；帧率化重掷）
        private static string ScrambleDigits(string final, int salt) {
            int frame = (int)(idleTimer * 28f);
            char[] buf = final.ToCharArray();
            for (int i = 0; i < buf.Length; i++) {
                if (char.IsDigit(buf[i])) {
                    buf[i] = (char)('0' + (int)(Hash01(salt + i * 17 + frame * 131) * 9.99f));
                }
            }
            return new string(buf);
        }

        //四角 L 形角标（placeholder2 直线拼绘）
        private static void DrawCornerBrackets(SpriteBatch sb, Texture2D px, Rectangle rect,
            int len, Color color) {
            sb.Draw(px, new Rectangle(rect.X, rect.Y, len, 1), color);
            sb.Draw(px, new Rectangle(rect.X, rect.Y, 1, len), color);
            sb.Draw(px, new Rectangle(rect.Right - len, rect.Y, len, 1), color);
            sb.Draw(px, new Rectangle(rect.Right - 1, rect.Y, 1, len), color);
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 1, len, 1), color);
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - len, 1, len), color);
            sb.Draw(px, new Rectangle(rect.Right - len, rect.Bottom - 1, len, 1), color);
            sb.Draw(px, new Rectangle(rect.Right - 1, rect.Bottom - len, 1, len), color);
        }

        private static void DrawBorder(SpriteBatch sb, Texture2D px, Rectangle rect, Color color) {
            sb.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 1), color);
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), color);
            sb.Draw(px, new Rectangle(rect.X, rect.Y, 1, rect.Height), color);
            sb.Draw(px, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), color);
        }

        private static float Ease01(float t) => t <= 0f ? 0f : t >= 1f ? 1f : t * t * (3f - 2f * t);

        private static float EaseOutCubic(float t) {
            t = MathHelper.Clamp(t, 0f, 1f);
            float u = 1f - t;
            return 1f - u * u * u;
        }

        private static float Hash01(int n) {
            float s = MathF.Sin(n * 12.9898f) * 43758.5453f;
            return s - MathF.Floor(s);
        }
    }
}
