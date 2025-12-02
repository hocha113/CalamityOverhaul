using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.ADV.Common
{
    /// <summary>
    /// 硫磺海风格任务追踪UI基类
    /// </summary>
    internal abstract class BaseSulfurQuestTrackerUI : BaseQuestTrackerUI
    {
        //拖拽相关
        private bool dragBool;
        private float dragOffset;
        private float screenYValue = 0;
        protected override float ScreenY => screenYValue;

        //硫磺海风格动画参数
        private float toxicWavePhase;
        private float sulfurPulse;
        private float miasmaTimer;
        private float bubbleTimer;

        //初始化标志
        private bool initialize = true;

        /// <summary>
        /// 设置默认屏幕Y值
        /// </summary>
        internal void SetDefScreenYValue() => initialize = true;

        /// <summary>
        /// 加载UI数据
        /// </summary>
        public new void LoadUIData(TagCompound tag) {
            tag.TryGet(Name + ":" + nameof(screenYValue), out screenYValue);
        }

        /// <summary>
        /// 保存UI数据
        /// </summary>
        public new void SaveUIData(TagCompound tag) {
            tag[Name + ":" + nameof(screenYValue)] = screenYValue;
        }

        public override void Update() {
            base.Update();

            //更新硫磺海风格动画
            toxicWavePhase += 0.022f;
            sulfurPulse += 0.015f;
            miasmaTimer += 0.032f;
            bubbleTimer += 0.025f;

            if (toxicWavePhase > MathHelper.TwoPi) toxicWavePhase -= MathHelper.TwoPi;
            if (sulfurPulse > MathHelper.TwoPi) sulfurPulse -= MathHelper.TwoPi;
            if (miasmaTimer > MathHelper.TwoPi) miasmaTimer -= MathHelper.TwoPi;
            if (bubbleTimer > MathHelper.TwoPi) bubbleTimer -= MathHelper.TwoPi;
        }

        protected override void DrawPanel(SpriteBatch spriteBatch, float alpha) {
            //初始化屏幕位置
            if (initialize) {
                initialize = false;
                if (screenYValue == 0) {
                    screenYValue = Main.screenHeight / 2f - currentPanelHeight / 2f;
                }
            }

            Texture2D pixel = VaultAsset.placeholder2.Value;

            //处理拖拽
            hoverInMainPage = UIHitBox.Intersects(MouseHitBox);
            if (hoverInMainPage) {
                if (keyLeftPressState == KeyPressState.Held) {
                    if (!dragBool) {
                        dragOffset = MousePosition.Y - screenYValue;
                    }
                    dragBool = true;
                }
            }
            if (dragBool) {
                screenYValue = MousePosition.Y - dragOffset;
                if (keyLeftPressState == KeyPressState.Released) {
                    dragBool = false;
                    dragOffset = MousePosition.Y;
                }
            }

            //限制Y坐标范围
            screenYValue = MathHelper.Clamp(screenYValue, 0, Main.screenHeight - currentPanelHeight);

            //绘制阴影
            Rectangle shadowRect = UIHitBox;
            shadowRect.Offset(6, 8);
            spriteBatch.Draw(pixel, shadowRect, new Rectangle(0, 0, 1, 1), Color.Black * (alpha * 0.6f));

            //绘制硫磺海渐变背景
            DrawSulfurBackground(spriteBatch, alpha);

            //绘制瘴气覆盖层
            float miasmaEffect = (float)Math.Sin(miasmaTimer * 1.1f) * 0.5f + 0.5f;
            Color miasmaTint = new Color(45, 55, 20) * (alpha * 0.4f * miasmaEffect);
            spriteBatch.Draw(pixel, UIHitBox, new Rectangle(0, 0, 1, 1), miasmaTint);

            //绘制毒性波浪覆盖
            DrawToxicWaveOverlay(spriteBatch, UIHitBox, alpha * 0.85f);

            //绘制硫磺海边框
            DrawSulfurFrame(spriteBatch, UIHitBox, alpha, borderGlow);

            //绘制气泡装饰
            DrawBubbleDecoration(spriteBatch, UIHitBox, alpha);
        }

        /// <summary>
        /// 绘制硫磺海渐变背景
        /// </summary>
        private void DrawSulfurBackground(SpriteBatch spriteBatch, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            int segs = 30;

            for (int i = 0; i < segs; i++) {
                float t = i / (float)segs;
                float t2 = (i + 1) / (float)segs;
                int y1 = (int)(DrawPosition.Y + t * currentPanelHeight);
                int y2 = (int)(DrawPosition.Y + t2 * currentPanelHeight);
                Rectangle r = new Rectangle((int)DrawPosition.X, y1, (int)PanelWidth, Math.Max(1, y2 - y1));

                //硫磺海配色
                Color sulfurDeep = new Color(12, 18, 8);
                Color toxicMid = new Color(28, 38, 15);
                Color acidEdge = new Color(65, 85, 30);

                float breathing = (float)Math.Sin(sulfurPulse) * 0.5f + 0.5f;
                Color blendBase = Color.Lerp(sulfurDeep, toxicMid, (float)Math.Sin(pulseTimer * 0.5f + t * 1.4f) * 0.5f + 0.5f);
                Color c = Color.Lerp(blendBase, acidEdge, t * 0.7f * (0.3f + breathing * 0.7f));
                c *= alpha * 0.92f;

                spriteBatch.Draw(pixel, r, new Rectangle(0, 0, 1, 1), c);
            }
        }

        /// <summary>
        /// 绘制毒性波浪覆盖效果
        /// </summary>
        private void DrawToxicWaveOverlay(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            int bands = 5;

            for (int i = 0; i < bands; i++) {
                float t = i / (float)bands;
                float y = rect.Y + 15 + t * (rect.Height - 30);
                float amp = 5f + (float)Math.Sin((toxicWavePhase + t) * 2.2f) * 3.5f;
                float thickness = 1.8f;
                int segments = 35;
                Vector2 prev = Vector2.Zero;

                for (int s = 0; s <= segments; s++) {
                    float p = s / (float)segments;
                    float localY = y + (float)Math.Sin(toxicWavePhase * 2.2f + p * MathHelper.TwoPi * 1.3f + t) * amp;
                    Vector2 point = new(rect.X + 8 + p * (rect.Width - 16), localY);

                    if (s > 0) {
                        Vector2 diff = point - prev;
                        float len = diff.Length();
                        if (len > 0.01f) {
                            float rot = diff.ToRotation();
                            Color c = new Color(60, 90, 30) * (alpha * 0.08f);
                            sb.Draw(pixel, prev, new Rectangle(0, 0, 1, 1), c, rot, Vector2.Zero, new Vector2(len, thickness), SpriteEffects.None, 0f);
                        }
                    }
                    prev = point;
                }
            }
        }

        /// <summary>
        /// 绘制硫磺海边框
        /// </summary>
        private void DrawSulfurFrame(SpriteBatch sb, Rectangle rect, float alpha, float pulse) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Color edge = Color.Lerp(new Color(70, 100, 35), new Color(130, 160, 65), pulse) * (alpha * 0.85f);

            //外边框
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 2), new Rectangle(0, 0, 1, 1), edge);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), new Rectangle(0, 0, 1, 1), edge * 0.75f);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, 2, rect.Height), new Rectangle(0, 0, 1, 1), edge * 0.88f);
            sb.Draw(pixel, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), new Rectangle(0, 0, 1, 1), edge * 0.88f);

            //内边框
            Rectangle inner = rect;
            inner.Inflate(-5, -5);
            Color innerC = new Color(140, 170, 70) * (alpha * 0.22f * pulse);
            sb.Draw(pixel, new Rectangle(inner.X, inner.Y, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerC);
            sb.Draw(pixel, new Rectangle(inner.X, inner.Bottom - 1, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerC * 0.7f);
            sb.Draw(pixel, new Rectangle(inner.X, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerC * 0.88f);
            sb.Draw(pixel, new Rectangle(inner.Right - 1, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerC * 0.88f);

            //角落装饰
            DrawCornerStar(sb, new Vector2(rect.X + 10, rect.Y + 10), alpha * 0.9f, pulse);
            DrawCornerStar(sb, new Vector2(rect.Right - 10, rect.Y + 10), alpha * 0.9f, pulse);
            DrawCornerStar(sb, new Vector2(rect.X + 10, rect.Bottom - 10), alpha * 0.65f, pulse);
            DrawCornerStar(sb, new Vector2(rect.Right - 10, rect.Bottom - 10), alpha * 0.65f, pulse);
        }

        /// <summary>
        /// 绘制角落星形装饰
        /// </summary>
        private static void DrawCornerStar(SpriteBatch sb, Vector2 pos, float a, float pulse) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float size = 5f + (float)Math.Sin(pulse * MathHelper.TwoPi) * 1f;
            Color c = new Color(160, 190, 80) * (a * (0.8f + pulse * 0.2f));

            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), c, 0f, new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.26f), SpriteEffects.None, 0f);
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), c * 0.8f, MathHelper.PiOver2, new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.26f), SpriteEffects.None, 0f);
        }

        /// <summary>
        /// 绘制气泡装饰效果
        /// </summary>
        private void DrawBubbleDecoration(SpriteBatch spriteBatch, Rectangle rect, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //绘制几个漂浮的硫磺气泡
            for (int i = 0; i < 4; i++) {
                float offset = (bubbleTimer + i * MathHelper.PiOver2) % MathHelper.TwoPi;
                float yPos = rect.Y + 20 + (float)Math.Sin(offset) * 15f + i * 30f;
                float xPos = rect.X + 15 + i * 60f;

                if (yPos > rect.Y + 10 && yPos < rect.Bottom - 10) {
                    float bubbleSize = 3f + (float)Math.Sin(offset * 2f) * 1.5f;
                    Color bubbleColor = new Color(140, 180, 70) * (alpha * 0.35f);

                    spriteBatch.Draw(pixel, new Vector2(xPos, yPos), new Rectangle(0, 0, 1, 1), bubbleColor, 0f,
                        new Vector2(0.5f), new Vector2(bubbleSize), SpriteEffects.None, 0f);
                }
            }
        }

        protected override void DrawContent(SpriteBatch spriteBatch, float alpha) {
            var font = FontAssets.MouseText.Value;
            const float titleScale = 0.72f;
            const float textScale = 0.62f;

            //绘制标题
            Vector2 titlePos = DrawPosition + new Vector2(10, 8);
            Color titleColor = new Color(160, 190, 80) * alpha;

            //标题发光效果
            Color titleGlow = new Color(140, 180, 70) * (alpha * 0.6f);
            for (int i = 0; i < 4; i++) {
                float a = MathHelper.TwoPi * i / 4f;
                Vector2 off = a.ToRotationVector2() * 1.5f;
                Utils.DrawBorderString(spriteBatch, QuestTitle.Value, titlePos + off, titleGlow * 0.5f, titleScale);
            }

            Utils.DrawBorderString(spriteBatch, QuestTitle.Value, titlePos, titleColor, titleScale);

            //绘制分隔线
            float titleHeight = font.MeasureString(QuestTitle.Value).Y * titleScale;
            Vector2 dividerStart = titlePos + new Vector2(0, titleHeight + 4);
            Vector2 dividerEnd = dividerStart + new Vector2(PanelWidth - 20, 0);
            DrawGradientLine(spriteBatch, dividerStart, dividerEnd,
                new Color(100, 140, 50) * (alpha * 0.9f), new Color(100, 140, 50) * (alpha * 0.08f), 1.3f);

            //绘制任务内容
            Vector2 contentPos = dividerStart + new Vector2(0, 12);
            DrawQuestContent(spriteBatch, contentPos, alpha, textScale);
        }

        /// <summary>
        /// 绘制任务内容，由子类实现具体逻辑
        /// </summary>
        protected abstract void DrawQuestContent(SpriteBatch spriteBatch, Vector2 startPos, float alpha, float textScale);

        protected override void DrawProgressBar(SpriteBatch spriteBatch, Vector2 position, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            float barWidth = PanelWidth - 20;
            float barHeight = 8;

            //背景
            Rectangle barBg = new Rectangle((int)position.X, (int)position.Y, (int)barWidth, (int)barHeight);
            spriteBatch.Draw(pixel, barBg, new Rectangle(0, 0, 1, 1), new Color(10, 15, 8) * (alpha * 0.8f));

            //进度填充
            var (current, total, _) = GetTrackingData();
            float progress = total > 0 ? current / total : 0f;
            float fillWidth = barWidth * Math.Min(progress, 1f);

            if (fillWidth > 0) {
                Rectangle barFill = new Rectangle((int)position.X + 1, (int)position.Y + 1, (int)fillWidth - 2, (int)barHeight - 2);

                //硫磺海进度条颜色
                Color fillStart = new Color(100, 140, 50);
                Color fillEnd = new Color(160, 190, 80);

                //绘制渐变进度条
                int segmentCount = 20;
                for (int i = 0; i < segmentCount; i++) {
                    float t = i / (float)segmentCount;
                    float t2 = (i + 1) / (float)segmentCount;
                    int x1 = (int)(barFill.X + t * barFill.Width);
                    int x2 = (int)(barFill.X + t2 * barFill.Width);

                    Color segColor = Color.Lerp(fillStart, fillEnd, t);
                    float pulse = (float)Math.Sin(pulseTimer * 2f + t * MathHelper.Pi) * 0.3f + 0.7f;

                    spriteBatch.Draw(pixel, new Rectangle(x1, barFill.Y, Math.Max(1, x2 - x1), barFill.Height),
                        segColor * (alpha * pulse));
                }

                //发光效果
                Color glowColor = new Color(160, 190, 80) * (alpha * 0.4f);
                spriteBatch.Draw(pixel, new Rectangle(barFill.X, barFill.Y - 1, barFill.Width, 1), glowColor);
                spriteBatch.Draw(pixel, new Rectangle(barFill.X, barFill.Bottom, barFill.Width, 1), glowColor);
            }

            //边框
            Color borderColor = new Color(100, 140, 50) * (alpha * 0.8f);
            spriteBatch.Draw(pixel, new Rectangle(barBg.X, barBg.Y, barBg.Width, 1), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(barBg.X, barBg.Bottom - 1, barBg.Width, 1), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(barBg.X, barBg.Y, 1, barBg.Height), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(barBg.Right - 1, barBg.Y, 1, barBg.Height), borderColor);
        }
    }
}
