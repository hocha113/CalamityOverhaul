using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.UIs.EntryDecisions
{
    /// <summary>
    /// 入世决策通知 UI，两段式：右缘滑入的紧凑通知条(pill)，点击展开为操作卡
    /// <br/>取代旧的全屏确认弹窗；不画遮罩、不占屏幕中轴，忽略零代价
    /// <br/>通知条闲置一段时间后收起为屏幕边缘的窄条(peek)，悬停恢复
    /// </summary>
    internal class EntryDecisionUI : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "UI";
        public static EntryDecisionUI Instance => UIHandleLoader.GetUIHandleOfType<EntryDecisionUI>();

        //本地化文本：共享
        public static LocalizedText CollapseHint { get; private set; }
        //本地化文本：传奇武器升级决策
        public static LocalizedText LegendPill { get; private set; }
        public static LocalizedText LegendTitle { get; private set; }
        public static LocalizedText LegendDesc { get; private set; }
        public static LocalizedText LegendConfirm { get; private set; }
        public static LocalizedText LegendSkip { get; private set; }
        public static LocalizedText LegendTrust { get; private set; }
        public static LocalizedText LegendSuccess { get; private set; }
        public static LocalizedText LegendTrustSuccess { get; private set; }
        public static LocalizedText LegendQueue { get; private set; }
        //本地化文本：任务检测决策
        public static LocalizedText QuestPill { get; private set; }
        public static LocalizedText QuestTitle { get; private set; }
        public static LocalizedText QuestDesc { get; private set; }
        public static LocalizedText QuestConfirm { get; private set; }
        public static LocalizedText QuestSkip { get; private set; }
        public static LocalizedText QuestTrust { get; private set; }
        public static LocalizedText QuestEnabled { get; private set; }
        public static LocalizedText QuestDisabled { get; private set; }
        public static LocalizedText QuestTrusted { get; private set; }

        //布局常量(UI空间)
        private const float PillW = 264f;
        private const float PillH = 54f;
        private const float CardW = 396f;
        private const float PeekVisible = 34f;
        private const float StackStartYRatio = 0.4f;
        private const float SlotGap = 8f;
        private const float Chamfer = 7f;
        private const int IdleFramesBeforePeek = 900;
        private const int SwapDelayFrames = 12;
        private const float ButtonH = 30f;

        /// <summary>UI空间屏幕宽(任意调用语境一致)</summary>
        private static float UIScreenW => PlayerInput.RealScreenWidth / Main.UIScale;
        /// <summary>UI空间屏幕高</summary>
        private static float UIScreenH => PlayerInput.RealScreenHeight / Main.UIScale;
        /// <summary>UI空间鼠标位置</summary>
        private static Vector2 UIMouse => new Vector2(PlayerInput.MouseX, PlayerInput.MouseY) / Main.UIScale;

        private static Texture2D Pixel => VaultAsset.placeholder2.Value;
        private static readonly Rectangle PixelSrc = new(0, 0, 1, 1);

        /// <summary>通知条/操作卡的运行时槽位，一个决策对应一个</summary>
        private sealed class Slot
        {
            public EntryDecision Decision;
            public float SlideIn;          //0→1 入场
            public float Peek;             //0→1 收边为窄条
            public float Hover;
            public float Expand;           //0→1 展开为操作卡
            public float Y = float.MinValue; //平滑堆叠Y，MinValue=未初始化(首帧吸附)
            public int IdleFrames;
            public bool Dying;
            public bool PlayedEnterSound;
            public float ContentSwap;      //卡内容切换(队列推进)时的刷新闪烁
            public Rectangle PanelRect;    //本帧面板矩形(pill与card间插值)
            public readonly Rectangle[] ButtonRects = new Rectangle[3];
            public readonly float[] ButtonHover = new float[3];
            //描述排版缓存，desc变化时重算
            public string CachedDescRaw;
            public string[] CachedDescLines = [];
            public float CardH = 180f;
        }

        private readonly List<Slot> slots = [];
        private Slot expanded;
        private int swapDelay;

        public override bool Active
            => (EntryDecisionManager.HasAny && EntryDecisionManager.GraceElapsed) || slots.Count > 0;

        public override void SetStaticDefaults() {
            CollapseHint = this.GetLocalization(nameof(CollapseHint), () => "右键收起");

            LegendPill = this.GetLocalization(nameof(LegendPill), () => "传奇武器等级同步");
            LegendTitle = this.GetLocalization(nameof(LegendTitle), () => "传奇武器升级确认");
            LegendDesc = this.GetLocalization(nameof(LegendDesc), () => "当前世界等级高于武器等级\n是否将{0}升级到等级 {1}？");
            LegendConfirm = this.GetLocalization(nameof(LegendConfirm), () => "确认升级");
            LegendSkip = this.GetLocalization(nameof(LegendSkip), () => "本次跳过");
            LegendTrust = this.GetLocalization(nameof(LegendTrust), () => "信任此世界");
            LegendSuccess = this.GetLocalization(nameof(LegendSuccess), () => "{0}已经升级到 {1} 级");
            LegendTrustSuccess = this.GetLocalization(nameof(LegendTrustSuccess), () => "{0}已信任本世界，今后将自动同步");
            LegendQueue = this.GetLocalization(nameof(LegendQueue), () => "还有 {0} 个传奇武器待确认");

            QuestPill = this.GetLocalization(nameof(QuestPill), () => "任务检测待确认");
            QuestTitle = this.GetLocalization(nameof(QuestTitle), () => "任务检测确认");
            QuestDesc = this.GetLocalization(nameof(QuestDesc), () => "检测到您从其他世界进入当前世界\n是否在当前世界中检测任务进度？");
            QuestConfirm = this.GetLocalization(nameof(QuestConfirm), () => "检测任务");
            QuestSkip = this.GetLocalization(nameof(QuestSkip), () => "跳过");
            QuestTrust = this.GetLocalization(nameof(QuestTrust), () => "信任此世界");
            QuestEnabled = this.GetLocalization(nameof(QuestEnabled), () => "已启用任务检测");
            QuestDisabled = this.GetLocalization(nameof(QuestDisabled), () => "已跳过任务检测");
            QuestTrusted = this.GetLocalization(nameof(QuestTrusted), () => "已信任此世界，今后自动启用任务检测");
        }

        public override void OnEnterWorld() {
            slots.Clear();
            expanded = null;
            swapDelay = 0;
        }

        public override void Update() {
            EntryDecisionManager.TickValidate();
            SyncSlots();

            if (swapDelay > 0) {
                swapDelay--;
            }

            bool anyHover = false;
            float stackY = UIScreenH * StackStartYRatio;

            foreach (Slot slot in slots) {
                UpdateSlotAnimation(slot);
                LayoutSlot(slot, ref stackY);

                bool hover = slot.PanelRect.Contains(UIMouse.ToPoint()) && slot.SlideIn > 0.4f;
                slot.Hover += ((hover ? 1f : 0f) - slot.Hover) * 0.2f;
                anyHover |= hover;

                //闲置计时：完全展示、未悬停、未展开时推进
                if (!hover && expanded != slot && !slot.Dying && slot.SlideIn > 0.95f) {
                    slot.IdleFrames++;
                }

                HandleSlotInput(slot, hover);
            }

            //退场动画播完的槽位出列，全部清空后 Active 归零
            slots.RemoveAll(static s => s.Dying && s.SlideIn < 0.02f);

            hoverInMainPage = anyHover;
            if (anyHover) {
                player.mouseInterface = true;
            }

            //展开状态下点击卡外或右键收起
            if (expanded != null) {
                if (keyRightPressState == KeyPressState.Pressed
                    || (keyLeftPressState == KeyPressState.Pressed && !anyHover)) {
                    Collapse();
                }
            }

            //框架命中盒信息(取第一个槽位的面板)
            if (slots.Count > 0) {
                DrawPosition = slots[0].PanelRect.TopLeft();
                Size = slots[0].PanelRect.Size();
                UIHitBox = slots[0].PanelRect;
            }
        }

        /// <summary>槽位列表与管理器决策同步：新增建槽、被移除的标记退场</summary>
        private void SyncSlots() {
            IReadOnlyList<EntryDecision> decisions = EntryDecisionManager.Decisions;

            foreach (Slot slot in slots) {
                bool alive = false;
                for (int i = 0; i < decisions.Count; i++) {
                    if (ReferenceEquals(decisions[i], slot.Decision)) {
                        alive = true;
                        break;
                    }
                }
                if (!alive && !slot.Dying) {
                    slot.Dying = true;
                    if (expanded == slot) {
                        expanded = null;
                    }
                }
            }

            if (!EntryDecisionManager.GraceElapsed) {
                return;
            }

            for (int i = 0; i < decisions.Count; i++) {
                bool known = false;
                foreach (Slot slot in slots) {
                    if (ReferenceEquals(slot.Decision, decisions[i])) {
                        known = true;
                        break;
                    }
                }
                if (!known) {
                    slots.Add(new Slot { Decision = decisions[i] });
                }
            }
        }

        private void UpdateSlotAnimation(Slot slot) {
            //入退场
            float slideTarget = slot.Dying ? 0f : 1f;
            slot.SlideIn += (slideTarget - slot.SlideIn) * (slot.Dying ? 0.16f : 0.1f);

            if (!slot.PlayedEnterSound && slot.SlideIn > 0.05f) {
                slot.PlayedEnterSound = true;
                SoundEngine.PlaySound(SoundID.MenuOpen with { Volume = 0.4f, Pitch = 0.25f });
            }

            //展开；退场时冻结形态，只淡出不回缩
            if (!slot.Dying) {
                float expandTarget = expanded == slot ? 1f : 0f;
                slot.Expand += (expandTarget - slot.Expand) * 0.16f;
            }

            //收边：展开或悬停时恢复
            bool wantPeek = slot.IdleFrames > IdleFramesBeforePeek && slot.Hover < 0.3f && expanded != slot;
            slot.Peek += ((wantPeek ? 1f : 0f) - slot.Peek) * 0.08f;

            slot.ContentSwap *= 0.88f;

            //描述排版缓存
            string desc = slot.Decision.CardDesc ?? string.Empty;
            if (slot.CachedDescRaw != desc) {
                slot.CachedDescRaw = desc;
                RebuildDescCache(slot, desc);
            }
        }

        private static void RebuildDescCache(Slot slot, string desc) {
            var font = FontAssets.MouseText.Value;
            const float descScale = 0.78f;
            float contentW = CardW - 24f * 2f;
            int wrapPx = (int)(contentW / descScale);

            List<string> lines = [];
            foreach (string raw in desc.Split('\n')) {
                if (raw.Length == 0) {
                    lines.Add(string.Empty);
                    continue;
                }
                lines.AddRange(VaultUtils.WrapTextArray(raw, font, wrapPx, 9, out _));
            }
            slot.CachedDescLines = [.. lines];

            float lineH = font.MeasureString("A").Y * descScale;
            bool hasFooter = !string.IsNullOrEmpty(slot.Decision.CardFooter);
            //标题区54 + 描述 + 页脚 + 按钮行 + 内边距
            slot.CardH = MathHelper.Clamp(
                54f + lines.Count * lineH + (hasFooter ? 20f : 6f) + ButtonH + 26f, 150f, 320f);
        }

        private void LayoutSlot(Slot slot, ref float stackY) {
            float slideEase = VaultUtils.EaseOutQuad(slot.SlideIn);
            float expandEase = VaultUtils.EaseOutQuad(slot.Expand);
            float peekEase = VaultUtils.EaseOutQuad(slot.Peek) * (1f - expandEase);

            float panelW = MathHelper.Lerp(PillW, CardW, expandEase);
            float panelH = MathHelper.Lerp(PillH, slot.CardH, expandEase);

            //X：滑入自右缘；peek 时向右缩回只留窄条；卡展开留出右边距；悬停微移
            float x = UIScreenW - panelW * slideEase
                + peekEase * (PillW - PeekVisible)
                - 10f * expandEase
                - 4f * slot.Hover * (1f - expandEase);

            //Y：堆叠平滑
            float targetY = stackY;
            if (slot.Y == float.MinValue) {
                slot.Y = targetY;
            }
            slot.Y += (targetY - slot.Y) * 0.18f;

            slot.PanelRect = new Rectangle((int)x, (int)slot.Y, (int)panelW, (int)panelH);
            stackY += (panelH + SlotGap) * slideEase;
        }

        private void HandleSlotInput(Slot slot, bool hover) {
            if (slot.Dying || keyLeftPressState != KeyPressState.Pressed) {
                return;
            }

            //未展开：点击通知条 → 展开
            if (slot.Expand < 0.5f) {
                if (hover) {
                    if (expanded != null) {
                        expanded.IdleFrames = 0;
                    }
                    expanded = slot;
                    slot.IdleFrames = 0;
                    SoundEngine.PlaySound(SoundID.MenuOpen with { Volume = 0.45f, Pitch = 0.1f });
                }
                return;
            }

            //已展开：按钮命中
            if (slot.Expand < 0.85f || swapDelay > 0) {
                return;
            }
            Point mouse = UIMouse.ToPoint();
            for (int i = 0; i < 3; i++) {
                if (!slot.ButtonRects[i].Contains(mouse)) {
                    continue;
                }
                swapDelay = SwapDelayFrames;
                EntryDecision d = slot.Decision;
                switch (i) {
                    case 0: d.Confirm(); break;
                    case 1: d.Skip(); break;
                    case 2: d.Trust(); break;
                }
                //动作后仍有效(如队列推进) → 卡保持展开并闪烁刷新
                EntryDecisionManager.TickValidate();
                if (d.StillValid) {
                    slot.ContentSwap = 1f;
                    SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.5f });
                }
                return;
            }
        }

        private void Collapse() {
            if (expanded == null) {
                return;
            }
            expanded.IdleFrames = 0;
            expanded = null;
            SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.4f });
        }

        public override void Draw(SpriteBatch spriteBatch) {
            //先画未展开的，展开卡最后画保证在顶层
            foreach (Slot slot in slots) {
                if (slot.Expand < 0.5f) {
                    DrawSlot(spriteBatch, slot);
                }
            }
            foreach (Slot slot in slots) {
                if (slot.Expand >= 0.5f) {
                    DrawSlot(spriteBatch, slot);
                }
            }
        }

        private void DrawSlot(SpriteBatch sb, Slot slot) {
            float alpha = VaultUtils.EaseOutQuad(slot.SlideIn);
            if (alpha <= 0.02f) {
                return;
            }

            Rectangle rect = slot.PanelRect;
            float expandEase = VaultUtils.EaseOutQuad(slot.Expand);
            Color accent = slot.Decision.Accent;
            float breathe = MathF.Sin(GlobalTimer * 2f) * 0.5f + 0.5f;

            DrawPanelBase(sb, rect, accent, alpha, breathe, slot.Hover, expandEase);

            //两段内容交叉淡化
            float pillContentA = alpha * Math.Clamp(1f - slot.Expand / 0.4f, 0f, 1f);
            float cardContentA = alpha * Math.Clamp((slot.Expand - 0.55f) / 0.45f, 0f, 1f);

            if (pillContentA > 0.02f) {
                DrawPillContent(sb, slot, rect, accent, pillContentA, breathe);
            }
            if (cardContentA > 0.02f) {
                DrawCardContent(sb, slot, rect, accent, cardContentA, breathe);
            }
        }

        /// <summary>面板底：暗色渐变填充(左角切角) + 左强调竖条 + 上下发丝线 + 扫光</summary>
        private void DrawPanelBase(SpriteBatch sb, Rectangle rect, Color accent, float alpha,
            float breathe, float hover, float expandEase) {
            //暗底，微弱纵向渐变，极轻的域色渗入
            Color top = new Color(15, 15, 19);
            Color bottom = new Color(23, 23, 29);
            top = Color.Lerp(top, accent, 0.045f);
            bottom = Color.Lerp(bottom, accent, 0.03f);
            DrawChamferFill(sb, rect, top * (alpha * 0.94f), bottom * (alpha * 0.94f), Chamfer);

            //左缘强调竖条(呼吸+悬停增亮)
            float barA = (0.55f + breathe * 0.18f + hover * 0.27f) * alpha;
            sb.Draw(Pixel, new Rectangle(rect.X, rect.Y + (int)Chamfer, 2, rect.Height - (int)Chamfer * 2),
                PixelSrc, accent * barA);

            //上发丝线：自左向右淡出
            DrawGradientLineH(sb, new Vector2(rect.X + Chamfer, rect.Y),
                rect.Width - Chamfer - 2f, accent * (alpha * (0.45f + hover * 0.25f)), Color.Transparent);
            //下发丝线：更暗
            DrawGradientLineH(sb, new Vector2(rect.X + Chamfer, rect.Bottom - 1),
                rect.Width - Chamfer - 2f, accent * (alpha * 0.2f), Color.Transparent);

            //展开卡的右侧角括号
            if (expandEase > 0.05f) {
                float bracketA = alpha * expandEase * 0.75f;
                DrawCornerBracket(sb, new Vector2(rect.Right - 2, rect.Y + 1), -1, 1, 12f, accent * bracketA);
                DrawCornerBracket(sb, new Vector2(rect.Right - 2, rect.Bottom - 2), -1, -1, 12f, accent * bracketA);
            }

            //缓慢扫光，一条1px竖线扫过
            float sweepT = GlobalTimer * 0.13f % 1.3f;
            if (sweepT < 1f) {
                float sweepX = rect.X + sweepT * rect.Width;
                sb.Draw(Pixel, new Rectangle((int)sweepX, rect.Y + 2, 1, rect.Height - 4),
                    PixelSrc, Color.White * (alpha * 0.05f));
                sb.Draw(Pixel, new Rectangle((int)sweepX - 1, rect.Y + 2, 1, rect.Height - 4),
                    PixelSrc, accent * (alpha * 0.04f));
            }
        }

        private void DrawPillContent(SpriteBatch sb, Slot slot, Rectangle rect, Color accent,
            float alpha, float breathe) {
            var font = FontAssets.MouseText.Value;
            Vector2 iconCenter = new(rect.X + 20f, rect.Y + rect.Height / 2f);

            //图标底光
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Color glowColor = accent with { A = 0 } * (alpha * (0.22f + breathe * 0.1f));
            sb.Draw(glow, iconCenter, null, glowColor, 0f, glow.Size() / 2f, 0.62f, SpriteEffects.None, 0f);

            slot.Decision.DrawIcon(sb, iconCenter, 30f, alpha);

            //待处理数量徽章
            int count = slot.Decision.PendingCount;
            float badgeZone = 0f;
            if (count > 1) {
                string badge = $"×{count}";
                float badgeScale = 0.72f;
                Vector2 badgeSize = font.MeasureString(badge) * badgeScale;
                Vector2 badgePos = new(rect.Right - badgeSize.X - 10f, rect.Y + (rect.Height - badgeSize.Y) / 2f + 2f);
                Utils.DrawBorderString(sb, badge, badgePos, accent * alpha, badgeScale);
                badgeZone = badgeSize.X + 14f;
            }

            //一行文本，超宽自动缩
            string text = slot.Decision.PillText ?? string.Empty;
            float maxW = rect.Width - 42f - badgeZone - 8f;
            float scale = FitScale(font, text, maxW, 0.8f);
            Vector2 size = font.MeasureString(text) * scale;
            Vector2 pos = new(rect.X + 42f, rect.Y + (rect.Height - size.Y) / 2f + 2f);
            Utils.DrawBorderString(sb, text, pos, new Color(216, 216, 224) * alpha, scale);
        }

        private void DrawCardContent(SpriteBatch sb, Slot slot, Rectangle rect, Color accent,
            float alpha, float breathe) {
            var font = FontAssets.MouseText.Value;
            EntryDecision d = slot.Decision;
            //内容切换时快速压暗刷新
            float contentA = alpha * (1f - slot.ContentSwap * 0.6f);

            //标题行
            Vector2 iconCenter = new(rect.X + 26f, rect.Y + 26f);
            Texture2D glow = CWRAsset.SoftGlow.Value;
            sb.Draw(glow, iconCenter, null, accent with { A = 0 } * (contentA * 0.25f),
                0f, glow.Size() / 2f, 0.7f, SpriteEffects.None, 0f);
            d.DrawIcon(sb, iconCenter, 32f, contentA);

            string title = d.CardTitle ?? string.Empty;
            float titleScale = FitScale(font, title, rect.Width - 68f - 70f, 0.9f);
            Color titleColor = Color.Lerp(new Color(235, 235, 240), accent, 0.35f + breathe * 0.1f);
            Utils.DrawBorderString(sb, title, new Vector2(rect.X + 48f, rect.Y + 13f), titleColor * contentA, titleScale);

            //收起提示(右上角小字)
            string hint = CollapseHint.Value;
            float hintScale = 0.62f;
            Vector2 hintSize = font.MeasureString(hint) * hintScale;
            Utils.DrawBorderString(sb, hint, new Vector2(rect.Right - hintSize.X - 16f, rect.Y + 8f),
                new Color(120, 120, 130) * (alpha * 0.8f), hintScale);

            //标题分隔线
            DrawGradientLineH(sb, new Vector2(rect.X + 16f, rect.Y + 44f), rect.Width - 32f,
                accent * (alpha * 0.5f), Color.Transparent);

            //描述
            const float descScale = 0.78f;
            float lineH = font.MeasureString("A").Y * descScale;
            float y = rect.Y + 54f;
            foreach (string line in slot.CachedDescLines) {
                Utils.DrawBorderString(sb, line, new Vector2(rect.X + 24f, y),
                    new Color(206, 206, 214) * contentA, descScale);
                y += lineH;
            }

            //按钮行
            float btnY = rect.Bottom - 14f - ButtonH;
            float areaX = rect.X + 18f;
            float areaW = rect.Width - 18f * 2f;
            const float btnGap = 10f;
            float btnW = (areaW - btnGap * 2f) / 3f;

            //页脚小字(按钮行上方)
            string footer = d.CardFooter;
            if (!string.IsNullOrEmpty(footer)) {
                float footScale = 0.68f;
                Utils.DrawBorderString(sb, footer, new Vector2(rect.X + 24f, btnY - 22f),
                    accent * (contentA * 0.85f), footScale);
            }

            string[] labels = [d.ConfirmLabel, d.SkipLabel, d.TrustLabel];
            //确认=域色 跳过=中性灰 信任=青色(永久决策的独立语义色)
            Color[] btnColors = [accent, new Color(158, 158, 166), new Color(112, 206, 206)];

            Point mouse = UIMouse.ToPoint();
            for (int i = 0; i < 3; i++) {
                var btnRect = new Rectangle((int)(areaX + (btnW + btnGap) * i), (int)btnY, (int)btnW, (int)ButtonH);
                slot.ButtonRects[i] = btnRect;

                bool hover = btnRect.Contains(mouse) && slot.Expand > 0.85f && !slot.Dying;
                slot.ButtonHover[i] += ((hover ? 1f : 0f) - slot.ButtonHover[i]) * 0.22f;

                DrawBracketButton(sb, btnRect, labels[i] ?? string.Empty, btnColors[i], slot.ButtonHover[i], contentA);
            }
        }

        /// <summary>括线式按钮：两侧竖向短线 + 底部中央展开的下划线，悬停时才出现浅填充</summary>
        private static void DrawBracketButton(SpriteBatch sb, Rectangle rect, string text,
            Color color, float hover, float alpha) {
            var font = FontAssets.MouseText.Value;

            //悬停浅填充
            if (hover > 0.02f) {
                sb.Draw(Pixel, rect, PixelSrc, color * (alpha * hover * 0.12f));
            }

            //两侧竖线(悬停伸长)
            int tickH = (int)(rect.Height - 14f + hover * 8f);
            int tickY = rect.Y + (rect.Height - tickH) / 2;
            Color tick = color * (alpha * (0.4f + hover * 0.5f));
            sb.Draw(Pixel, new Rectangle(rect.X, tickY, 1, tickH), PixelSrc, tick);
            sb.Draw(Pixel, new Rectangle(rect.Right - 1, tickY, 1, tickH), PixelSrc, tick);

            //底部中央展开的下划线
            float lineW = rect.Width * (0.22f + 0.74f * hover);
            var underline = new Rectangle(
                (int)(rect.Center.X - lineW / 2f), rect.Bottom - 1, (int)lineW, 1);
            sb.Draw(Pixel, underline, PixelSrc, color * (alpha * (0.35f + hover * 0.55f)));

            //标签(超宽自动缩)
            float scale = FitScale(font, text, rect.Width - 10f, 0.76f);
            Vector2 size = font.MeasureString(text) * scale;
            Vector2 pos = new(rect.Center.X - size.X / 2f, rect.Center.Y - size.Y / 2f + 2f);
            Color labelColor = Color.Lerp(new Color(198, 198, 206), Color.White, hover);
            Utils.DrawBorderString(sb, text, pos, labelColor * alpha, scale);
        }

        #region 绘制辅助

        /// <summary>文本超出 maxW 时按比例缩小 scale</summary>
        private static float FitScale(ReLogic.Graphics.DynamicSpriteFont font, string text, float maxW, float baseScale) {
            if (string.IsNullOrEmpty(text)) {
                return baseScale;
            }
            float rawW = font.MeasureString(text).X;
            return rawW * baseScale > maxW && rawW > 0f ? maxW / rawW : baseScale;
        }

        /// <summary>纵向渐变填充，左侧两角切角(右侧贴屏幕缘保持直角)</summary>
        private static void DrawChamferFill(SpriteBatch sb, Rectangle rect, Color top, Color bottom, float chamfer) {
            int c = (int)Math.Min(chamfer, rect.Height / 2f);
            for (int i = 0; i < rect.Height; i++) {
                float t = i / (float)rect.Height;
                Color color = Color.Lerp(top, bottom, t);

                int inset = 0;
                if (i < c) {
                    inset = c - i;
                }
                else if (i >= rect.Height - c) {
                    inset = i - (rect.Height - c) + 1;
                }

                sb.Draw(Pixel, new Rectangle(rect.X + inset, rect.Y + i, rect.Width - inset, 1), PixelSrc, color);
            }
        }

        /// <summary>水平渐变线(1px 高)，8px 分段近似</summary>
        private static void DrawGradientLineH(SpriteBatch sb, Vector2 start, float width, Color from, Color to) {
            const int seg = 8;
            int count = Math.Max(1, (int)(width / seg));
            for (int i = 0; i < count; i++) {
                float t = i / (float)count;
                float w = Math.Min(seg, width - i * seg);
                sb.Draw(Pixel, new Rectangle((int)(start.X + i * seg), (int)start.Y, (int)w + 1, 1),
                    PixelSrc, Color.Lerp(from, to, t));
            }
        }

        /// <summary>L形角括号，dirX/dirY 指定两臂延伸方向(±1)</summary>
        private static void DrawCornerBracket(SpriteBatch sb, Vector2 corner, int dirX, int dirY, float len, Color color) {
            int l = (int)len;
            int x = (int)corner.X;
            int y = (int)corner.Y;
            sb.Draw(Pixel, new Rectangle(dirX > 0 ? x : x - l, y, l, 1), PixelSrc, color);
            sb.Draw(Pixel, new Rectangle(x, dirY > 0 ? y : y - l, 1, l), PixelSrc, color);
        }

        #endregion
    }
}
