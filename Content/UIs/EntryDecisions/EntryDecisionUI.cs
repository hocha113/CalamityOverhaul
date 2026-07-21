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
    /// 入世决策 UI，右缘 pill→操作卡；闲置收 peek，悬停恢复
    /// </summary>
    internal class EntryDecisionUI : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "UI";
        public static EntryDecisionUI Instance => UIHandleLoader.GetUIHandleOfType<EntryDecisionUI>();

        //L10n 共享
        public static LocalizedText CollapseHint { get; private set; }
        //L10n 传奇升级
        public static LocalizedText LegendPill { get; private set; }
        public static LocalizedText LegendTitle { get; private set; }
        public static LocalizedText LegendDesc { get; private set; }
        public static LocalizedText LegendConfirm { get; private set; }
        public static LocalizedText LegendSkip { get; private set; }
        public static LocalizedText LegendTrust { get; private set; }
        public static LocalizedText LegendSuccess { get; private set; }
        public static LocalizedText LegendTrustSuccess { get; private set; }
        public static LocalizedText LegendQueue { get; private set; }
        //L10n 任务检测
        public static LocalizedText QuestPill { get; private set; }
        public static LocalizedText QuestTitle { get; private set; }
        public static LocalizedText QuestDesc { get; private set; }
        public static LocalizedText QuestConfirm { get; private set; }
        public static LocalizedText QuestSkip { get; private set; }
        public static LocalizedText QuestTrust { get; private set; }
        public static LocalizedText QuestEnabled { get; private set; }
        public static LocalizedText QuestDisabled { get; private set; }
        public static LocalizedText QuestTrusted { get; private set; }

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

        /// <summary>UI空间屏宽</summary>
        private static float UIScreenW => PlayerInput.RealScreenWidth / Main.UIScale;
        /// <summary>UI空间屏高</summary>
        private static float UIScreenH => PlayerInput.RealScreenHeight / Main.UIScale;
        private static Vector2 UIMouse => new Vector2(PlayerInput.MouseX, PlayerInput.MouseY) / Main.UIScale;

        private static Texture2D Pixel => VaultAsset.placeholder2.Value;
        private static readonly Rectangle PixelSrc = new(0, 0, 1, 1);

        /// <summary>通知条/卡运行时槽，一决策一槽</summary>
        private sealed class Slot
        {
            public EntryDecision Decision;
            public float SlideIn;          //0→1入场
            public float Peek;             //0→1收边窄条
            public float Hover;
            public float Expand;           //0→1展开卡
            public float Y = float.MinValue;//堆叠Y，MinValue=未init
            public int IdleFrames;
            public bool Dying;
            public bool PlayedEnterSound;
            public float ContentSwap;      //队列推进闪烁
            public Rectangle PanelRect;    //本帧面板(pill↔card)
            public readonly Rectangle[] ButtonRects = new Rectangle[3];
            public readonly float[] ButtonHover = new float[3];
            //desc排版缓存
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

                //闲置计时，全展且未悬停未展开
                if (!hover && expanded != slot && !slot.Dying && slot.SlideIn > 0.95f) {
                    slot.IdleFrames++;
                }

                HandleSlotInput(slot, hover);
            }

            //退场完出列，空则 Active=0
            slots.RemoveAll(static s => s.Dying && s.SlideIn < 0.02f);

            hoverInMainPage = anyHover;
            if (anyHover) {
                player.mouseInterface = true;
            }

            //展开时点卡外/右键收起
            if (expanded != null) {
                if (keyRightPressState == KeyPressState.Pressed
                    || (keyLeftPressState == KeyPressState.Pressed && !anyHover)) {
                    Collapse();
                }
            }

            if (slots.Count > 0) {
                DrawPosition = slots[0].PanelRect.TopLeft();
                Size = slots[0].PanelRect.Size();
                UIHitBox = slots[0].PanelRect;
            }
        }

        /// <summary>槽与管理器同步，新增建槽、移除标退场</summary>
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
            float slideTarget = slot.Dying ? 0f : 1f;
            slot.SlideIn += (slideTarget - slot.SlideIn) * (slot.Dying ? 0.16f : 0.1f);

            if (!slot.PlayedEnterSound && slot.SlideIn > 0.05f) {
                slot.PlayedEnterSound = true;
                SoundEngine.PlaySound(SoundID.MenuOpen with { Volume = 0.4f, Pitch = 0.25f });
            }

            //展开；退场冻结只淡出
            if (!slot.Dying) {
                float expandTarget = expanded == slot ? 1f : 0f;
                slot.Expand += (expandTarget - slot.Expand) * 0.16f;
            }

            //收边，展开/悬停恢复
            bool wantPeek = slot.IdleFrames > IdleFramesBeforePeek && slot.Hover < 0.3f && expanded != slot;
            slot.Peek += ((wantPeek ? 1f : 0f) - slot.Peek) * 0.08f;

            slot.ContentSwap *= 0.88f;

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
            //标题54+描述+页脚+按钮+垫
            slot.CardH = MathHelper.Clamp(
                54f + lines.Count * lineH + (hasFooter ? 20f : 6f) + ButtonH + 26f, 150f, 320f);
        }

        private void LayoutSlot(Slot slot, ref float stackY) {
            float slideEase = VaultUtils.EaseOutQuad(slot.SlideIn);
            float expandEase = VaultUtils.EaseOutQuad(slot.Expand);
            float peekEase = VaultUtils.EaseOutQuad(slot.Peek) * (1f - expandEase);

            float panelW = MathHelper.Lerp(PillW, CardW, expandEase);
            float panelH = MathHelper.Lerp(PillH, slot.CardH, expandEase);

            //X 滑入/peek缩/展开留边/悬停微移
            float x = UIScreenW - panelW * slideEase
                + peekEase * (PillW - PeekVisible)
                - 10f * expandEase
                - 4f * slot.Hover * (1f - expandEase);

            //Y堆叠平滑
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

            //未展开点条→展开
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

            //已展开按钮命中
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
                //仍Valid则保持展开并闪
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
            //展开卡最后画
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

            float pillContentA = alpha * Math.Clamp(1f - slot.Expand / 0.4f, 0f, 1f);
            float cardContentA = alpha * Math.Clamp((slot.Expand - 0.55f) / 0.45f, 0f, 1f);

            if (pillContentA > 0.02f) {
                DrawPillContent(sb, slot, rect, accent, pillContentA, breathe);
            }
            if (cardContentA > 0.02f) {
                DrawCardContent(sb, slot, rect, accent, cardContentA, breathe);
            }
        }

        /// <summary>面板底，渐变+左竖条+发丝线+扫光</summary>
        private void DrawPanelBase(SpriteBatch sb, Rectangle rect, Color accent, float alpha,
            float breathe, float hover, float expandEase) {
            //暗底渐变
            Color top = new Color(15, 15, 19);
            Color bottom = new Color(23, 23, 29);
            top = Color.Lerp(top, accent, 0.045f);
            bottom = Color.Lerp(bottom, accent, 0.03f);
            DrawChamferFill(sb, rect, top * (alpha * 0.94f), bottom * (alpha * 0.94f), Chamfer);

            //左缘竖条
            float barA = (0.55f + breathe * 0.18f + hover * 0.27f) * alpha;
            sb.Draw(Pixel, new Rectangle(rect.X, rect.Y + (int)Chamfer, 2, rect.Height - (int)Chamfer * 2),
                PixelSrc, accent * barA);

            //上发丝线
            DrawGradientLineH(sb, new Vector2(rect.X + Chamfer, rect.Y),
                rect.Width - Chamfer - 2f, accent * (alpha * (0.45f + hover * 0.25f)), Color.Transparent);
            //下发丝线
            DrawGradientLineH(sb, new Vector2(rect.X + Chamfer, rect.Bottom - 1),
                rect.Width - Chamfer - 2f, accent * (alpha * 0.2f), Color.Transparent);

            if (expandEase > 0.05f) {
                float bracketA = alpha * expandEase * 0.75f;
                DrawCornerBracket(sb, new Vector2(rect.Right - 2, rect.Y + 1), -1, 1, 12f, accent * bracketA);
                DrawCornerBracket(sb, new Vector2(rect.Right - 2, rect.Bottom - 2), -1, -1, 12f, accent * bracketA);
            }

            //1px扫光
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

            Texture2D glow = CWRAsset.SoftGlow.Value;
            Color glowColor = accent with { A = 0 } * (alpha * (0.22f + breathe * 0.1f));
            sb.Draw(glow, iconCenter, null, glowColor, 0f, glow.Size() / 2f, 0.62f, SpriteEffects.None, 0f);

            slot.Decision.DrawIcon(sb, iconCenter, 30f, alpha);

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
            //切换压暗闪
            float contentA = alpha * (1f - slot.ContentSwap * 0.6f);

            Vector2 iconCenter = new(rect.X + 26f, rect.Y + 26f);
            Texture2D glow = CWRAsset.SoftGlow.Value;
            sb.Draw(glow, iconCenter, null, accent with { A = 0 } * (contentA * 0.25f),
                0f, glow.Size() / 2f, 0.7f, SpriteEffects.None, 0f);
            d.DrawIcon(sb, iconCenter, 32f, contentA);

            string title = d.CardTitle ?? string.Empty;
            float titleScale = FitScale(font, title, rect.Width - 68f - 70f, 0.9f);
            Color titleColor = Color.Lerp(new Color(235, 235, 240), accent, 0.35f + breathe * 0.1f);
            Utils.DrawBorderString(sb, title, new Vector2(rect.X + 48f, rect.Y + 13f), titleColor * contentA, titleScale);

            string hint = CollapseHint.Value;
            float hintScale = 0.62f;
            Vector2 hintSize = font.MeasureString(hint) * hintScale;
            Utils.DrawBorderString(sb, hint, new Vector2(rect.Right - hintSize.X - 16f, rect.Y + 8f),
                new Color(120, 120, 130) * (alpha * 0.8f), hintScale);

            DrawGradientLineH(sb, new Vector2(rect.X + 16f, rect.Y + 44f), rect.Width - 32f,
                accent * (alpha * 0.5f), Color.Transparent);

            const float descScale = 0.78f;
            float lineH = font.MeasureString("A").Y * descScale;
            float y = rect.Y + 54f;
            foreach (string line in slot.CachedDescLines) {
                Utils.DrawBorderString(sb, line, new Vector2(rect.X + 24f, y),
                    new Color(206, 206, 214) * contentA, descScale);
                y += lineH;
            }

            float btnY = rect.Bottom - 14f - ButtonH;
            float areaX = rect.X + 18f;
            float areaW = rect.Width - 18f * 2f;
            const float btnGap = 10f;
            float btnW = (areaW - btnGap * 2f) / 3f;

            string footer = d.CardFooter;
            if (!string.IsNullOrEmpty(footer)) {
                float footScale = 0.68f;
                Utils.DrawBorderString(sb, footer, new Vector2(rect.X + 24f, btnY - 22f),
                    accent * (contentA * 0.85f), footScale);
            }

            string[] labels = [d.ConfirmLabel, d.SkipLabel, d.TrustLabel];
            //确认域色/跳过灰/信任青
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

        /// <summary>括线按钮，悬停浅填</summary>
        private static void DrawBracketButton(SpriteBatch sb, Rectangle rect, string text,
            Color color, float hover, float alpha) {
            var font = FontAssets.MouseText.Value;

            if (hover > 0.02f) {
                sb.Draw(Pixel, rect, PixelSrc, color * (alpha * hover * 0.12f));
            }

            int tickH = (int)(rect.Height - 14f + hover * 8f);
            int tickY = rect.Y + (rect.Height - tickH) / 2;
            Color tick = color * (alpha * (0.4f + hover * 0.5f));
            sb.Draw(Pixel, new Rectangle(rect.X, tickY, 1, tickH), PixelSrc, tick);
            sb.Draw(Pixel, new Rectangle(rect.Right - 1, tickY, 1, tickH), PixelSrc, tick);

            float lineW = rect.Width * (0.22f + 0.74f * hover);
            var underline = new Rectangle(
                (int)(rect.Center.X - lineW / 2f), rect.Bottom - 1, (int)lineW, 1);
            sb.Draw(Pixel, underline, PixelSrc, color * (alpha * (0.35f + hover * 0.55f)));

            float scale = FitScale(font, text, rect.Width - 10f, 0.76f);
            Vector2 size = font.MeasureString(text) * scale;
            Vector2 pos = new(rect.Center.X - size.X / 2f, rect.Center.Y - size.Y / 2f + 2f);
            Color labelColor = Color.Lerp(new Color(198, 198, 206), Color.White, hover);
            Utils.DrawBorderString(sb, text, pos, labelColor * alpha, scale);
        }

        #region 绘制辅助

        /// <summary>超 maxW 缩 scale</summary>
        private static float FitScale(ReLogic.Graphics.DynamicSpriteFont font, string text, float maxW, float baseScale) {
            if (string.IsNullOrEmpty(text)) {
                return baseScale;
            }
            float rawW = font.MeasureString(text).X;
            return rawW * baseScale > maxW && rawW > 0f ? maxW / rawW : baseScale;
        }

        /// <summary>纵向渐变，左两角切角</summary>
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

        /// <summary>水平渐变线1px，8px段</summary>
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

        /// <summary>L角括号，dirX/dirY=±1</summary>
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
