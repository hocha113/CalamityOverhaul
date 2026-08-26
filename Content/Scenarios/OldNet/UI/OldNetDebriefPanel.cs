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
    /// 深潜战报屏：回主世界后的一屏总结（ModSystem + 相位机，形态照抄 CybCourseCompletePanel，
    /// 坐标改用 UI 空间口径修正先例纪律偏差）。标题 + 五行统计 + 弹出方式 + 单键 CONTINUE
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

        //════════ 相位机 ════════

        private enum Phase { Hidden, FadeIn, Idle, FadeOut }

        private const int PanelW = 470;
        //2.1 评级章改版：容纳评级徽章 + 评分/最佳两行 + 风格加成行
        private const int PanelH = 452;

        private static readonly Color ColdCyan = new(140, 200, 210);
        private static readonly Color EmberRed = new(235, 64, 44);
        private static readonly Color TextDim = new(150, 160, 175);
        private static readonly Color PanelBg = new(8, 12, 16);

        private static Phase phase = Phase.Hidden;
        private static float alpha;
        private static float idleTimer;
        private static bool prevMouseLeft;
        private static OldNetDebriefReport report;
        private static Rectangle continueRect = Rectangle.Empty;
        private static Rectangle panelRect = Rectangle.Empty;

        public static bool Visible => phase != Phase.Hidden;

        internal static void Show(OldNetDebriefReport data) {
            report = data;
            phase = Phase.FadeIn;
            alpha = 0f;
            idleTimer = 0f;
            SoundEngine.PlaySound(SoundID.MenuOpen with { Pitch = -0.3f });
        }

        internal static void Hide() {
            phase = Phase.Hidden;
            alpha = 0f;
            report = null;
            continueRect = Rectangle.Empty;
            panelRect = Rectangle.Empty;
        }

        public override void OnWorldUnload() => Hide();

        //UI 空间口径：布局与命中共用（修正 CybCourseCompletePanel 直读 screenWidth 的偏差）
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
                    if (alpha > 0.985f) {
                        alpha = 1f;
                        phase = Phase.Idle;
                    }
                    break;
                case Phase.Idle:
                    idleTimer += dt;
                    HandleClicks();
                    break;
                case Phase.FadeOut:
                    alpha = MathHelper.Lerp(alpha, 0f, 0.18f);
                    if (alpha < 0.02f) {
                        Hide();
                    }
                    break;
            }

            if (phase != Phase.Hidden && panelRect != Rectangle.Empty) {
                Main.LocalPlayer.mouseInterface = true;
            }
        }

        private static void HandleClicks() {
            bool mouseDown = Main.mouseLeft;
            bool clicked = mouseDown && !prevMouseLeft;
            prevMouseLeft = mouseDown;
            if (!clicked) {
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

        //════════ 绘制 ════════

        private static void DrawOverlay(SpriteBatch sb) {
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null || px.IsDisposed || report == null) {
                return;
            }

            //全屏压暗（材料纯色底，合法矩形用途）
            sb.Draw(px, new Rectangle(0, 0, (int)UIScreenW, (int)UIScreenH),
                new Color(0, 4, 8) * (0.62f * alpha));

            float slideY = (1f - alpha) * 26f;
            int x0 = (int)((UIScreenW - PanelW) * 0.5f);
            int y0 = (int)((UIScreenH - PanelH) * 0.5f + slideY);
            panelRect = new Rectangle(x0, y0, PanelW, PanelH);

            bool burned = report.Kind != OldNetExitKind.SafeLogout;
            Color accent = burned ? EmberRed : ColdCyan;

            //实底面板 + 1px 边框 + 顶缘受光线（被骇者 HUD 的底板语法，无暗羽化）
            sb.Draw(px, panelRect, PanelBg * (0.94f * alpha));
            DrawBorder(sb, px, panelRect, accent * (0.55f * alpha));
            sb.Draw(px, new Rectangle(x0 + 1, y0 + 1, PanelW - 2, 1), accent * (0.8f * alpha));

            //慢速横向扫描线：面板活着
            float scanPhase = idleTimer * 0.22f % 1f;
            int scanY = y0 + (int)(scanPhase * (PanelH - 2)) + 1;
            sb.Draw(px, new Rectangle(x0 + 1, scanY, PanelW - 2, 1), accent * (0.10f * alpha));

            DrawContent(sb, px, accent, burned);
        }

        private static void DrawContent(SpriteBatch sb, Texture2D px, Color accent, bool burned) {
            DynamicSpriteFont title = FontAssets.DeathText.Value;
            DynamicSpriteFont body = FontAssets.MouseText.Value;
            int x0 = panelRect.X;
            int y0 = panelRect.Y;

            //标题：诊断字样走机器英文（加载屏同语汇），副题走本地化
            string titleText = "LINK REPORT";
            Vector2 titleSz = title.MeasureString(titleText) * 0.62f;
            Vector2 titlePos = new(x0 + (PanelW - titleSz.X) * 0.5f, y0 + 18f);
            sb.DrawString(title, titleText, titlePos + new Vector2(2f, 2f), Color.Black * (0.6f * alpha),
                0f, Vector2.Zero, 0.62f, SpriteEffects.None, 0f);
            sb.DrawString(title, titleText, titlePos, accent * alpha,
                0f, Vector2.Zero, 0.62f, SpriteEffects.None, 0f);

            string sub = DebriefSubtitle.Value;
            Vector2 subSz = body.MeasureString(sub) * 0.8f;
            Utils.DrawBorderString(sb, sub, new Vector2(x0 + (PanelW - subSz.X) * 0.5f, titlePos.Y + titleSz.Y + 6f),
                TextDim * alpha, 0.8f);

            //分隔线
            int divY = (int)(titlePos.Y + titleSz.Y + 34f);
            sb.Draw(px, new Rectangle(x0 + 34, divY, PanelW - 68, 1), accent * (0.4f * alpha));

            //五行统计：标签左对齐、数值右对齐
            float rowY = divY + 14f;
            float rowH = 27f;
            DrawStatRow(sb, body, rowY, DebriefDepth.Value, $"{report.MaxDepthCols}", false);
            DrawStatRow(sb, body, rowY + rowH, DebriefHarvest.Value, $"{report.HarvestCount}", false);
            //烧断且铭刻为 0：灰红删除线，损失要被看见
            bool strikeSettled = burned && report.SettledTotal == 0;
            DrawStatRow(sb, body, rowY + rowH * 2, DebriefSettled.Value, $"{report.SettledTotal}", strikeSettled);
            DrawStatRow(sb, body, rowY + rowH * 3, DebriefHunted.Value, $"{report.HuntedCount}", false);
            int seconds = report.DiveTicks / 60;
            DrawStatRow(sb, body, rowY + rowH * 4, DebriefTime.Value, $"{seconds / 60:D2}:{seconds % 60:D2}", false);

            //评级章分区（2.1）：分隔线 + 徽章 + 评分/最佳 + 风格加成行
            int divY2 = (int)(rowY + rowH * 5 + 6f);
            sb.Draw(px, new Rectangle(x0 + 34, divY2, PanelW - 68, 1), accent * (0.4f * alpha));
            DrawGradeSection(sb, px, body, divY2);

            //弹出方式行 + 损失行
            float exitY = divY2 + 98f;
            string exitText = report.Kind switch {
                OldNetExitKind.RamBurnout => DebriefExitBurnout.Value,
                OldNetExitKind.Death => DebriefExitDeath.Value,
                _ => DebriefExitSafe.Value,
            };
            Vector2 exitSz = body.MeasureString(exitText) * 0.85f;
            Utils.DrawBorderString(sb, exitText, new Vector2(x0 + (PanelW - exitSz.X) * 0.5f, exitY),
                accent * alpha, 0.85f);

            if (burned && report.LostPending > 0) {
                string lost = DebriefLost.Format(report.LostPending);
                Vector2 lostSz = body.MeasureString(lost) * 0.62f;
                Utils.DrawBorderString(sb, lost, new Vector2(x0 + (PanelW - lostSz.X) * 0.5f, exitY + 22f),
                    Color.Lerp(EmberRed, TextDim, 0.45f) * alpha, 0.62f);
            }

            //机制注脚：铭刻语义的一行常驻解释（首潜引导的收尾复读）
            string hint = DebriefMechHint.Value;
            Vector2 hintSz = body.MeasureString(hint) * 0.56f;
            Utils.DrawBorderString(sb, hint,
                new Vector2(x0 + (PanelW - hintSz.X) * 0.5f, panelRect.Bottom - 70f),
                TextDim * (0.7f * alpha), 0.56f);

            //CONTINUE 键
            const int btnW = 150;
            const int btnH = 34;
            continueRect = new Rectangle(x0 + (PanelW - btnW) / 2, panelRect.Bottom - 50, btnW, btnH);
            bool hover = continueRect.Contains(UIMouse);
            Color btnCol = hover ? accent : accent * 0.55f;
            sb.Draw(px, continueRect, PanelBg * (0.9f * alpha));
            DrawBorder(sb, px, continueRect, btnCol * alpha);
            string btn = DebriefContinue.Value;
            Vector2 btnSz = body.MeasureString(btn) * 0.85f;
            Utils.DrawBorderString(sb, btn,
                new Vector2(continueRect.X + (btnW - btnSz.X) * 0.5f, continueRect.Y + (btnH - btnSz.Y) * 0.5f + 2f),
                (hover ? Color.White : TextDim) * alpha, 0.85f);
        }

        //评级章分区（2.1）：徽章（右）+ 评分与历史最佳（左）+ 风格加成行（下），零新贴图
        private static void DrawGradeSection(SpriteBatch sb, Texture2D px, DynamicSpriteFont body, int top) {
            DynamicSpriteFont big = FontAssets.DeathText.Value;
            int x0 = panelRect.X;
            float xLeft = x0 + 46f;
            Color gradeCol = OldNetRating.GradeColor(report.GradeIndex);

            //评分行：本区主角，数值放大
            Utils.DrawBorderString(sb, DebriefScore.Value, new Vector2(xLeft, top + 16f),
                TextDim * alpha, 0.78f);
            Utils.DrawBorderString(sb, $"{report.Score}", new Vector2(xLeft + 84f, top + 10f),
                Color.White * (0.95f * alpha), 1.05f);

            //历史最佳行 + 新纪录闪
            string best = DebriefBest.Format(
                $"{OldNetRating.Letter(report.BestGradeIndex)} {report.BestScore}");
            Utils.DrawBorderString(sb, best, new Vector2(xLeft, top + 46f),
                TextDim * (0.9f * alpha), 0.62f);
            if (report.NewRecord) {
                float flash = 0.6f + 0.4f * MathF.Sin(idleTimer * 7f);
                Vector2 bestSz = body.MeasureString(best) * 0.62f;
                Utils.DrawBorderString(sb, DebriefNewRecord.Value,
                    new Vector2(xLeft + bestSz.X + 10f, top + 46f), EmberRed * (flash * alpha), 0.62f);
            }

            //评级徽章：DeathText 大字母 + 评级色 + 细框角标
            var box = new Rectangle(x0 + PanelW - 46 - 72, top + 8, 72, 56);
            string letter = OldNetRating.Letter(report.GradeIndex);
            Vector2 lsz = big.MeasureString(letter) * 0.9f;
            Vector2 lpos = new(box.X + (box.Width - lsz.X) * 0.5f, box.Y + (box.Height - lsz.Y) * 0.5f);
            sb.DrawString(big, letter, lpos + new Vector2(2f, 2f), Color.Black * (0.6f * alpha),
                0f, Vector2.Zero, 0.9f, SpriteEffects.None, 0f);
            sb.DrawString(big, letter, lpos, gradeCol * alpha,
                0f, Vector2.Zero, 0.9f, SpriteEffects.None, 0f);
            DrawCornerBrackets(sb, px, box, gradeCol * (0.7f * alpha));
            string gradeLabel = DebriefGrade.Value;
            Vector2 glSz = body.MeasureString(gradeLabel) * 0.5f;
            Utils.DrawBorderString(sb, gradeLabel,
                new Vector2(box.X + (box.Width - glSz.X) * 0.5f, box.Bottom + 2f),
                TextDim * (0.7f * alpha), 0.5f);

            //风格加成行：刻意藏到战报屏才揭晓（拆礼物节拍）；无风格整行留白
            string styleLine = BuildStyleLine();
            if (styleLine != null) {
                Vector2 ssz = body.MeasureString(styleLine) * 0.58f;
                Utils.DrawBorderString(sb, styleLine,
                    new Vector2(x0 + (PanelW - ssz.X) * 0.5f, top + 78f),
                    new Color(255, 170, 60) * (0.92f * alpha), 0.58f);
            }
        }

        //已达成风格的展示串："幽灵潜行 +300 // 高热生还 +200"；无风格给 null
        private static string BuildStyleLine() {
            if (report.Styles == OldNetStyleFlags.None) {
                return null;
            }
            List<string> parts = [];
            void Add(OldNetStyleFlags flag, LocalizedText name) {
                if ((report.Styles & flag) != 0) {
                    parts.Add($"{name.Value} +{OldNetRating.StyleBonus(flag)}");
                }
            }
            Add(OldNetStyleFlags.Ghost, StyleGhost);
            Add(OldNetStyleFlags.HeatSurvivor, StyleHeat);
            Add(OldNetStyleFlags.HotExtract, StyleHotExtract);
            Add(OldNetStyleFlags.DragnetEscape, StyleDragnet);
            return string.Join(" // ", parts);
        }

        //四角 L 形细框角标（placeholder2 直线拼绘）
        private static void DrawCornerBrackets(SpriteBatch sb, Texture2D px, Rectangle rect, Color color) {
            const int len = 10;
            sb.Draw(px, new Rectangle(rect.X, rect.Y, len, 1), color);
            sb.Draw(px, new Rectangle(rect.X, rect.Y, 1, len), color);
            sb.Draw(px, new Rectangle(rect.Right - len, rect.Y, len, 1), color);
            sb.Draw(px, new Rectangle(rect.Right - 1, rect.Y, 1, len), color);
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 1, len, 1), color);
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - len, 1, len), color);
            sb.Draw(px, new Rectangle(rect.Right - len, rect.Bottom - 1, len, 1), color);
            sb.Draw(px, new Rectangle(rect.Right - 1, rect.Bottom - len, 1, len), color);
        }

        private static void DrawStatRow(SpriteBatch sb, DynamicSpriteFont font, float y,
            string label, string value, bool strike) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float xLeft = panelRect.X + 46f;
            float xRight = panelRect.Right - 46f;
            Utils.DrawBorderString(sb, label, new Vector2(xLeft, y), TextDim * alpha, 0.78f);

            Color valueCol = strike ? Color.Lerp(EmberRed, TextDim, 0.5f) : Color.White * 0.92f;
            Vector2 valueSz = font.MeasureString(value) * 0.85f;
            Vector2 valuePos = new(xRight - valueSz.X, y - 1f);
            Utils.DrawBorderString(sb, value, valuePos, valueCol * alpha, 0.85f);
            if (strike) {
                //灰红删除线压在数值上
                sb.Draw(px, new Rectangle((int)(valuePos.X - 4f), (int)(y + valueSz.Y * 0.42f),
                    (int)(valueSz.X + 8f), 2), Color.Lerp(EmberRed, TextDim, 0.3f) * (0.9f * alpha));
            }
        }

        private static void DrawBorder(SpriteBatch sb, Texture2D px, Rectangle rect, Color color) {
            sb.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 1), color);
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), color);
            sb.Draw(px, new Rectangle(rect.X, rect.Y, 1, rect.Height), color);
            sb.Draw(px, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), color);
        }
    }
}
