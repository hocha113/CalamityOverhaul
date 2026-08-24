using CalamityOverhaul.Content.Cyberwares.UIs;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.CommonUIs
{
    /// <summary>
    /// 城镇特殊 NPC 的通用对话条（义体家族皮肤）：底部横栏 + 左侧全息框立绘 +
    /// 说话人名与价格系数徽标 + 打字机正文 + 底部心情报告 + 右侧竖排命令行。
    /// 子类只装配说话人/立绘/台词池/心情数据源/命令项，不再各画各的窗
    /// </summary>
    internal abstract class NPCTalkUIBase : UIHandle
    {
        /// <summary>右侧命令行：文案取值器 + 强调色 + 点击动作（音效由动作自己管）</summary>
        protected readonly record struct TalkCommand(Func<string> Label, Color Accent, Action OnClick);

        public override bool CloseOnEscape => true;
        public override SoundStyle? OpenSound => SoundID.MenuOpen;
        public override SoundStyle? CloseSound => silentClose ? null : SoundID.MenuClose;

        //切到其它界面时静默关，免双音效叠
        private bool silentClose;

        #region 子类装配点

        /// <summary>说话人名</summary>
        protected abstract string SpeakerLabel { get; }
        /// <summary>立绘贴图（纵向逐帧，默认朝左，画时水平翻转），可为 null</summary>
        protected abstract Texture2D Portrait { get; }
        /// <summary>立绘贴图帧数</summary>
        protected abstract int PortraitFrames { get; }
        /// <summary>取一句新台词；空串表示池子未就绪，保留当前句</summary>
        protected abstract string PickDialogueLine();
        /// <summary>幸福度购物系数（1 = 中性）</summary>
        protected abstract double MoodFactor { get; }
        /// <summary>心情报告全文，空串不画</summary>
        protected abstract string MoodReportText { get; }
        /// <summary>价格系数徽标文案</summary>
        protected abstract string FormatPriceFactor(double factor);
        /// <summary>命令行装配，开窗时取一次</summary>
        protected abstract TalkCommand[] BuildCommands();
        /// <summary>绑定的 NPC 是否仍有效；失效当帧收窗，别让玩家对着空气聊天</summary>
        protected virtual bool SessionAlive => true;

        #endregion

        private TalkCommand[] commands = [];
        private string currentDialogue = string.Empty;
        private float revealed;
        private int totalChars;

        private Rectangle barRect;
        private Rectangle portraitRect;
        private Rectangle textRect;
        private Rectangle closeBtnRect;
        private Rectangle[] cmdRects = [];
        private bool[] cmdHovers = [];
        private float[] cmdAnims = [];
        private bool closeHover;

        private readonly CyberPanelRenderer panelRenderer = new();

        protected override void OnOpen() {
            commands = BuildCommands() ?? [];
            cmdRects = new Rectangle[commands.Length];
            cmdHovers = new bool[commands.Length];
            cmdAnims = new float[commands.Length];
            closeHover = false;
            RePickDialogue();
        }

        /// <summary>重抽一句台词并重置打字机</summary>
        protected void RePickDialogue() {
            string line = PickDialogueLine();
            if (string.IsNullOrEmpty(line)) {
                return;
            }
            currentDialogue = line;
            revealed = 0f;
        }

        /// <summary>静默关（不播 CloseSound），切到另一张界面时用，免双音效叠</summary>
        protected void CloseSilent() {
            silentClose = true;
            Close();
            silentClose = false;
        }

        private bool FullyRevealed => revealed >= totalChars;

        private void Layout() {
            float screenW = NPCUIStyle.UIScreenW;
            float screenH = NPCUIStyle.UIScreenH;

            int barW = (int)MathHelper.Clamp(screenW - 120, 760, 1240);
            const int barH = 214;
            int x = (int)(screenW - barW) / 2;

            float ease = VaultUtils.EaseOutCubic(MathHelper.Clamp(OpenProgress.Current, 0f, 1f));
            int baseY = (int)screenH - barH - 28;
            int y = baseY + (int)((1f - ease) * (barH + 44));
            barRect = new Rectangle(x, y, barW, barH);

            const int pad = 18;
            int portH = barH - pad * 2;
            int portW = (int)(portH * 0.74f);
            portraitRect = new Rectangle(barRect.X + pad, barRect.Y + pad, portW, portH);

            const int choiceW = 330;
            int choiceX = barRect.Right - pad - choiceW;
            const int rowH = 58;
            int blockH = rowH * Math.Max(1, commands.Length);
            int rowY = barRect.Y + (barH - blockH) / 2;
            for (int i = 0; i < commands.Length; i++) {
                cmdRects[i] = new Rectangle(choiceX, rowY + rowH * i, choiceW, rowH);
            }

            int textX = portraitRect.Right + 28;
            textRect = new Rectangle(textX, barRect.Y + pad, choiceX - 18 - textX, portH);

            closeBtnRect = CyberPanelRenderer.GetCloseButtonRect(barRect);
        }

        public override void Update() {
            if (!IsOpen && OpenProgress.Current <= 0.001f) {
                return;
            }
            Layout();
            if (!IsOpen) {
                return;
            }

            if (!SessionAlive) {
                Close();
                return;
            }

            if (!FullyRevealed) {
                revealed += 0.9f;
            }

            if (barRect.Contains(MousePoint)) {
                player.mouseInterface = true;
                //点正文区跳过打字机；命令行与关闭钮让位
                if (keyLeftPressState == KeyPressState.Pressed && !FullyRevealed
                    && !AnyControlHovered()) {
                    revealed = totalChars;
                }
            }

            closeHover = closeBtnRect.Contains(MousePoint);
            if (closeHover) {
                player.mouseInterface = true;
                if (keyLeftPressState == KeyPressState.Pressed) {
                    Close();
                    return;
                }
            }

            for (int i = 0; i < commands.Length; i++) {
                UpdateHover(cmdRects[i], ref cmdHovers[i], ref cmdAnims[i]);
            }
            for (int i = 0; i < commands.Length; i++) {
                if (cmdHovers[i] && keyLeftPressState == KeyPressState.Pressed) {
                    commands[i].OnClick?.Invoke();
                    return;
                }
            }
        }

        private bool AnyControlHovered() {
            if (closeBtnRect.Contains(MousePoint)) {
                return true;
            }
            foreach (Rectangle r in cmdRects) {
                if (r.Contains(MousePoint)) {
                    return true;
                }
            }
            return false;
        }

        private void UpdateHover(Rectangle rect, ref bool hover, ref float t) {
            bool now = rect.Contains(MousePoint);
            if (now && !hover) {
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.4f });
            }
            hover = now;
            if (now) {
                player.mouseInterface = true;
            }
            t = MathHelper.Clamp(t + (now ? 0.18f : -0.18f), 0f, 1f);
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (!IsOpen && OpenProgress.Current <= 0.001f) {
                return;
            }
            Layout();
            float alpha = MathHelper.Clamp(OpenProgress.Current, 0f, 1f);

            CyberPanelRenderer.DrawShaderBackground(spriteBatch, alpha * 0.97f, barRect, Vector2.Zero, 0f, mode: 1);
            CyberPanelRenderer.DrawFrameDecor(spriteBatch, alpha, barRect, GlobalTimer);

            Texture2D px = VaultAsset.placeholder2.Value;

            DrawPortrait(spriteBatch, alpha);
            NPCUIStyle.DrawVDivider(spriteBatch, portraitRect.Right + 13, barRect.Y + 14, barRect.Bottom - 14, CyberwareTheme.Accent * (alpha * 0.5f));
            DrawTextBlock(spriteBatch, px, alpha);

            if (commands.Length > 0) {
                NPCUIStyle.DrawVDivider(spriteBatch, cmdRects[0].X - 14, barRect.Y + 14, barRect.Bottom - 14, CyberwareTheme.Accent * (alpha * 0.4f));
                for (int i = 0; i < commands.Length; i++) {
                    DrawChoiceRow(spriteBatch, cmdRects[i], commands[i].Label(), cmdAnims[i], alpha, commands[i].Accent);
                }
            }

            panelRenderer.DrawCloseButton(spriteBatch, alpha, barRect, closeHover);
        }

        private void DrawPortrait(SpriteBatch sb, float alpha) {
            NPCUIStyle.DrawHoloFrame(sb, portraitRect, CyberwareTheme.Accent, alpha, GlobalTimer);
            Texture2D tex = Portrait;
            int frames = PortraitFrames;
            if (tex == null || frames <= 0) {
                return;
            }
            int frameH = tex.Height / frames;
            Rectangle src = new(0, 0, tex.Width, frameH);
            float sc = Math.Min((portraitRect.Width - 20f) / src.Width, (portraitRect.Height - 22f) / src.Height);
            Vector2 anchor = new(portraitRect.Center.X, portraitRect.Bottom - 10);
            sb.Draw(tex, anchor, src, Color.White * alpha, 0f, new Vector2(src.Width / 2f, src.Height), sc, SpriteEffects.FlipHorizontally, 0f);
        }

        private void DrawTextBlock(SpriteBatch sb, Texture2D px, float alpha) {
            string name = SpeakerLabel;
            float nameScale = 0.82f * CyberwareTheme.FontScale;
            Vector2 nameSize = FontAssets.MouseText.Value.MeasureString(name) * nameScale;
            float nameY = textRect.Y + 2f;
            sb.Draw(px, new Rectangle(textRect.X, (int)nameY + 3, 4, (int)nameSize.Y - 4), CyberwareTheme.Accent * alpha);
            Utils.DrawBorderString(sb, name, new Vector2(textRect.X + 12, nameY), CyberwareTheme.Accent * alpha, nameScale);

            //名字行右侧：幸福度价格系数徽标，便宜青、贵红、中性暗
            double factor = MoodFactor;
            string factorText = FormatPriceFactor(factor);
            float factorScale = 0.5f * CyberwareTheme.FontScale;
            Vector2 factorSize = FontAssets.MouseText.Value.MeasureString(factorText) * factorScale;
            Color factorColor = factor < 0.995 ? CyberwareTheme.AccentCyan
                : factor > 1.005 ? CyberwareTheme.Accent : CyberwareTheme.TextDim;
            Utils.DrawBorderString(sb, factorText,
                new Vector2(textRect.Right - factorSize.X, nameY + (nameSize.Y - factorSize.Y) * 0.5f),
                factorColor * alpha, factorScale);

            int divY = (int)(nameY + nameSize.Y + 6f);
            NPCUIStyle.DrawHDivider(sb, textRect.X, textRect.Right, divY, CyberwareTheme.Accent * (alpha * 0.5f));

            //底部心情条：报告最多两行，台词区在其上方截断
            float moodTop = textRect.Bottom;
            string report = MoodReportText;
            if (!string.IsNullOrEmpty(report)) {
                float moodScale = 0.5f * CyberwareTheme.FontScale;
                string[] moodLines = VaultUtils.WrapTextArray(report, FontAssets.MouseText.Value, (int)(textRect.Width / moodScale), 2, out _);
                float moodLineH = FontAssets.MouseText.Value.MeasureString("A").Y * moodScale + 4f;
                int moodCount = 0;
                foreach (string l in moodLines) {
                    if (!string.IsNullOrEmpty(l)) {
                        moodCount++;
                    }
                }
                if (moodCount > 0) {
                    moodTop = textRect.Bottom - moodCount * moodLineH - 6f;
                    NPCUIStyle.DrawHDivider(sb, textRect.X, textRect.Right, (int)moodTop, CyberwareTheme.Accent * (alpha * 0.25f));
                    float moodY = moodTop + 5f;
                    foreach (string line in moodLines) {
                        if (string.IsNullOrEmpty(line)) {
                            continue;
                        }
                        Utils.DrawBorderString(sb, line, new Vector2(textRect.X, moodY), CyberwareTheme.TextDim * (alpha * 0.85f), moodScale);
                        moodY += moodLineH;
                    }
                }
            }

            if (string.IsNullOrEmpty(currentDialogue)) {
                totalChars = 0;
                return;
            }

            float ds = 0.8f * CyberwareTheme.FontScale;
            string[] lines = VaultUtils.WrapTextArray(currentDialogue, FontAssets.MouseText.Value, (int)(textRect.Width / ds), 8, out _);
            int total = 0;
            foreach (string l in lines) {
                if (!string.IsNullOrEmpty(l)) {
                    total += l.Length;
                }
            }
            totalChars = total;

            float lineH = FontAssets.MouseText.Value.MeasureString("A").Y * ds + 7f;
            float y = divY + 12f;

            int budget = (int)revealed;
            float lastY = y;
            foreach (string line in lines) {
                if (string.IsNullOrEmpty(line)) {
                    continue;
                }
                if (budget <= 0) {
                    break;
                }
                //不侵入底部心情条
                if (y + lineH > moodTop - 3f) {
                    break;
                }
                int take = Math.Min(budget, line.Length);
                string seg = take >= line.Length ? line : line[..take];
                Utils.DrawBorderString(sb, seg, new Vector2(textRect.X, y), CyberwareTheme.TextBright * alpha, ds);
                budget -= line.Length;
                lastY = y;
                y += lineH;
            }

            if (!FullyRevealed && (int)(GlobalTimer * 2.5f) % 2 == 0) {
                Utils.DrawBorderString(sb, "▌", new Vector2(textRect.X + 2, lastY + lineH * 0.05f), CyberwareTheme.Accent * alpha, ds);
            }
        }

        private static void DrawChoiceRow(SpriteBatch sb, Rectangle rect, string text, float hoverT, float alpha, Color accent) {
            int slide = NPCUIStyle.DrawCommandRow(sb, rect, accent, hoverT, alpha);
            float scale = 0.6f * CyberwareTheme.FontScale;
            Vector2 size = FontAssets.MouseText.Value.MeasureString(text) * scale;
            float ty = rect.Y + (rect.Height - size.Y) / 2f;
            Color tc = Color.Lerp(CyberwareTheme.TextNormal, CyberwareTheme.TextBright, 0.45f + 0.55f * hoverT) * alpha;
            Utils.DrawBorderString(sb, text, new Vector2(rect.X + 20 + slide, ty), tc, scale);
            Utils.DrawBorderString(sb, ">", new Vector2(rect.Right - 26 - hoverT * 4f, ty), accent * (alpha * (0.35f + 0.65f * hoverT)), scale);
        }
    }
}
