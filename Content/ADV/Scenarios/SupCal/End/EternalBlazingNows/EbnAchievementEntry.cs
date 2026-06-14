using CalamityOverhaul.Content.UIs.NotificationPopup;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.ADV.Scenarios.SupCal.End.EternalBlazingNows
{
    /// <summary>Ebn 成就弹窗，硫磺火主题</summary>
    internal class EbnAchievementEntry : NotificationEntry
    {
        private readonly Texture2D icon;
        private readonly string title;
        private readonly string description;

        public override float Width => 340f;
        public override float Height => 100f;
        public override int SlideTime => 28;
        public override int DisplayTime => 260;
        public override float Gap => 8f;
        public override SoundStyle? AppearSound
            => SoundID.DD2_BetsyWindAttack with { Volume = 0.6f, Pitch = 0.3f };

        #region 内嵌余烬粒子

        private struct Ember
        {
            public float X, Y;
            public float VelX, VelY;
            public float Life, MaxLife;
            public float Size;
        }

        private readonly Ember[] embers = new Ember[20];
        private bool embersInited;
        private int lastEmberUpdateFrame = -1;

        #endregion

        public EbnAchievementEntry(Texture2D icon, string title, string description) {
            this.icon = icon;
            this.title = title;
            this.description = description;
        }

        public override bool OnClick() {
            SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.4f, Pitch = 0.2f });
            return true;
        }

        public override void DrawContent(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D px = TextureAssets.MagicPixel.Value;
            int life = LifeTimer;
            float time = Main.GameUpdateCount * 0.03f;

            //── 1. 阴影层 ──
            DrawShadow(sb, px, rect, alpha);

            //── 2. 深红渐变背景 ──
            DrawBrimstoneBackground(sb, px, rect, alpha, time);

            //── 3. 火焰脉冲叠层 ──
            DrawFlamePulse(sb, px, rect, alpha, time);

            //── 4. 横向扫光 ──
            DrawSweepLight(sb, px, rect, alpha, time, life);

            //── 5. 脉冲辉光边框 ──
            DrawBrimstoneBorder(sb, px, rect, alpha, time);

            //── 6. 角落火焰十字 ──
            DrawFlameCorners(sb, px, rect, alpha, time);

            //── 7. 余烬粒子 ──
            DrawEmbers(sb, px, rect, alpha, life);

            //── 8. 图标区域 ──
            DrawIcon(sb, px, rect, alpha, time, life);

            //── 9. 分隔线 ──
            float sepX = rect.X + 88;
            int sepH = rect.Height - 20;
            for (int i = 0; i < 3; i++) {
                float fade = i == 1 ? 0.35f : 0.12f;
                sb.Draw(px, new Rectangle((int)sepX, rect.Y + 10 + i * (sepH / 3), 1, sepH / 3),
                    new Color(255, 140, 60) * (fade * alpha));
            }

            //── 10. 标题与描述 ──
            DrawText(sb, rect, alpha, life, time);

            //── 11. 底部进度装饰条 ──
            DrawBottomBar(sb, px, rect, alpha, life);
        }

        #region 背景与氛围

        private static void DrawShadow(SpriteBatch sb, Texture2D px, Rectangle r, float alpha) {
            for (int d = 4; d >= 1; d--) {
                Rectangle shadow = r;
                shadow.Inflate(d, d);
                shadow.Offset(2, 2);
                sb.Draw(px, shadow, Color.Black * (alpha * 0.06f * d));
            }
        }

        private static void DrawBrimstoneBackground(SpriteBatch sb, Texture2D px,
            Rectangle rect, float alpha, float time) {
            //纵向多段渐变，火焰色调
            const int segs = 16;
            Color deep = new(20, 5, 5);
            Color mid = new(70, 15, 12);
            Color hot = new(120, 35, 20);

            for (int i = 0; i < segs; i++) {
                float t = i / (float)segs;
                float t2 = (i + 1) / (float)segs;
                int y1 = rect.Y + (int)(t * rect.Height);
                int y2 = rect.Y + (int)(t2 * rect.Height);

                //火焰波动，让条带颜色随时间微微浮动
                float wave = MathF.Sin(time * 1.4f + t * 2.5f) * 0.5f + 0.5f;
                Color c = Color.Lerp(Color.Lerp(deep, mid, wave), hot, t * 0.55f);
                sb.Draw(px, new Rectangle(rect.X, y1, rect.Width, Math.Max(1, y2 - y1)),
                    c * (alpha * 0.92f));
            }
        }

        private static void DrawFlamePulse(SpriteBatch sb, Texture2D px,
            Rectangle rect, float alpha, float time) {
            //全局火焰脉冲呼吸
            float pulse = MathF.Sin(time * 4f) * 0.5f + 0.5f;
            Color pulseColor = new Color(130, 30, 15) * (alpha * 0.15f * pulse);
            sb.Draw(px, rect, pulseColor);

            //底部热辉光带
            int glowH = rect.Height / 4;
            for (int i = 0; i < 4; i++) {
                float t = i / 4f;
                float intensity = (1f - t) * pulse * 0.12f;
                sb.Draw(px, new Rectangle(rect.X, rect.Bottom - glowH + (int)(t * glowH),
                    rect.Width, Math.Max(1, glowH / 4)),
                    new Color(200, 60, 30) * (alpha * intensity));
            }
        }

        private static void DrawSweepLight(SpriteBatch sb, Texture2D px,
            Rectangle rect, float alpha, float time, int life) {
            //横向扫光，温暖的余烬色调
            float sweepPos = ((time * 0.35f + life * 0.008f) % 1.6f) - 0.3f;
            int sweepWidth = rect.Width / 3;
            int sweepCenterX = rect.X + (int)(sweepPos * (rect.Width + sweepWidth));
            const int segments = 8;
            int segW = sweepWidth / segments;

            for (int i = 0; i < segments; i++) {
                int sx = sweepCenterX - sweepWidth / 2 + i * segW;
                if (sx + segW < rect.X || sx >= rect.Right) continue;
                int clampedX = Math.Max(sx, rect.X);
                int clampedR = Math.Min(sx + segW, rect.Right);
                int w = clampedR - clampedX;
                if (w <= 0) continue;

                float dist = Math.Abs((i + 0.5f) / segments - 0.5f) * 2f;
                float sweepAlpha = MathF.Pow(1f - dist, 3f);
                sb.Draw(px, new Rectangle(clampedX, rect.Y, w, rect.Height),
                    new Color(255, 160, 80) * (sweepAlpha * 0.1f * alpha));
            }
        }

        private static void DrawBrimstoneBorder(SpriteBatch sb, Texture2D px,
            Rectangle rect, float alpha, float time) {
            float pulse = MathF.Sin(time * 3f) * 0.5f + 0.5f;
            Color edge = Color.Lerp(new Color(180, 60, 35), new Color(255, 140, 70), pulse) * alpha;

            //上边框三线渐变
            sb.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 3), edge);
            sb.Draw(px, new Rectangle(rect.X + 6, rect.Y + 4, rect.Width - 12, 1),
                edge * 0.2f);
            //下边框
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 3, rect.Width, 3), edge * 0.7f);
            //左右
            sb.Draw(px, new Rectangle(rect.X, rect.Y, 3, rect.Height), edge * 0.85f);
            sb.Draw(px, new Rectangle(rect.Right - 3, rect.Y, 3, rect.Height), edge * 0.85f);

            //脉冲辉光上边
            Color glowBorder = Color.Lerp(new Color(255, 120, 60), Color.White, pulse * 0.25f);
            sb.Draw(px, new Rectangle(rect.X, rect.Y - 1, rect.Width, 1),
                glowBorder * (0.3f * pulse * alpha));
        }

        private static void DrawFlameCorners(SpriteBatch sb, Texture2D px,
            Rectangle rect, float alpha, float time) {
            float pulse = MathF.Sin(time * 3f) * 0.5f + 0.5f;
            Color c = new Color(255, 150, 70) * (alpha * (0.7f + pulse * 0.3f));

            Vector2[] corners = [
                new(rect.X + 10, rect.Y + 10),
                new(rect.Right - 10, rect.Y + 10),
                new(rect.X + 10, rect.Bottom - 10),
                new(rect.Right - 10, rect.Bottom - 10)
            ];

            float size = 4.5f + pulse * 1.2f;

            foreach (var pos in corners) {
                //十字 + 对角线
                sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), c, 0f,
                    new Vector2(0.5f), new Vector2(size * 1.2f, size * 0.25f), SpriteEffects.None, 0f);
                sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), c * 0.85f, MathHelper.PiOver2,
                    new Vector2(0.5f), new Vector2(size * 1.2f, size * 0.25f), SpriteEffects.None, 0f);
                sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), c * 0.6f, MathHelper.PiOver4,
                    new Vector2(0.5f), new Vector2(size * 0.8f, size * 0.2f), SpriteEffects.None, 0f);
                //中心亮点
                sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), Color.White * (0.4f * pulse * alpha), 0f,
                    new Vector2(0.5f), 1.5f, SpriteEffects.None, 0f);
            }
        }

        #endregion

        #region 余烬粒子

        private void DrawEmbers(SpriteBatch sb, Texture2D px, Rectangle rect, float alpha, int life) {
            if (!embersInited) {
                InitEmbers(rect);
                embersInited = true;
            }

            //用 LifeTimer 防止同帧重复更新
            bool shouldUpdate = lastEmberUpdateFrame != LifeTimer;
            if (shouldUpdate)
                lastEmberUpdateFrame = LifeTimer;

            for (int i = 0; i < embers.Length; i++) {
                ref var e = ref embers[i];

                if (shouldUpdate) {
                    e.Life++;

                    if (e.Life >= e.MaxLife) {
                        e.X = rect.X + Main.rand.NextFloat(10, rect.Width - 10);
                        e.Y = rect.Bottom - Main.rand.NextFloat(0, 8);
                        e.VelX = Main.rand.NextFloat(-0.4f, 0.4f);
                        e.VelY = -Main.rand.NextFloat(0.4f, 1.2f);
                        e.Life = 0;
                        e.MaxLife = Main.rand.NextFloat(35f, 70f);
                        e.Size = Main.rand.NextFloat(1.2f, 2.8f);
                    }

                    e.X += e.VelX;
                    e.Y += e.VelY;
                    e.VelX += Main.rand.NextFloat(-0.03f, 0.03f);
                }

                //只在面板区域内绘制
                if (e.X < rect.X || e.X > rect.Right || e.Y < rect.Y || e.Y > rect.Bottom)
                    continue;

                float t = e.Life / e.MaxLife;
                float fade = MathF.Sin((1f - t) * MathF.PI);
                Color c = Color.Lerp(new Color(255, 180, 90), new Color(255, 70, 40), t);
                sb.Draw(px, new Vector2(e.X, e.Y), new Rectangle(0, 0, 1, 1),
                    c * (fade * alpha * 0.6f), 0f, new Vector2(0.5f), e.Size, SpriteEffects.None, 0f);
            }
        }

        private void InitEmbers(Rectangle rect) {
            for (int i = 0; i < embers.Length; i++) {
                embers[i] = new Ember {
                    X = rect.X + Main.rand.NextFloat(10, rect.Width - 10),
                    Y = rect.Bottom - Main.rand.NextFloat(0, rect.Height),
                    VelX = Main.rand.NextFloat(-0.3f, 0.3f),
                    VelY = -Main.rand.NextFloat(0.4f, 1.0f),
                    Life = Main.rand.NextFloat(0, 40),
                    MaxLife = Main.rand.NextFloat(35f, 70f),
                    Size = Main.rand.NextFloat(1.2f, 2.8f)
                };
            }
        }

        #endregion

        #region 图标

        private void DrawIcon(SpriteBatch sb, Texture2D px, Rectangle rect,
            float alpha, float time, int life) {
            float iconAreaSize = rect.Height - 28;
            float iconCX = rect.X + 16 + iconAreaSize / 2f;
            float iconCY = rect.Y + rect.Height / 2f;
            Vector2 center = new(iconCX, iconCY);

            //辉光圆环（多层叠加模拟）
            float glowPulse = MathF.Sin(time * 2.5f) * 0.5f + 0.5f;
            for (int r = 3; r >= 1; r--) {
                float size = iconAreaSize * 0.5f + r * 4 + glowPulse * 3f;
                Color glowC = new Color(255, 100, 40) * (alpha * 0.08f * (4 - r));
                sb.Draw(px, center, new Rectangle(0, 0, 1, 1), glowC, 0f,
                    new Vector2(0.5f), new Vector2(size, size), SpriteEffects.None, 0f);
            }

            //图标绘制
            if (icon != null) {
                float maxDim = MathHelper.Max(icon.Width, icon.Height);
                float scale = (iconAreaSize * 0.7f) / maxDim;

                //入场弹跳
                float bounce = life < 20
                    ? 1f + MathF.Sin(life / 20f * MathF.PI) * 0.18f
                    : 1f + MathF.Sin(time * 3f) * 0.015f;
                scale *= bounce;

                float rot = MathF.Sin(time * 1.5f) * 0.03f;
                sb.Draw(icon, center, null, Color.White * alpha, rot,
                    icon.Size() / 2f, scale, SpriteEffects.None, 0f);
            }
        }

        #endregion

        #region 文字

        private void DrawText(SpriteBatch sb, Rectangle rect, float alpha, int life, float time) {
            float textX = rect.X + 96;
            float availWidth = rect.Width - 96 - 18;
            var font = FontAssets.MouseText.Value;

            //标题 - 淡入
            float titleAlpha = MathHelper.Clamp((life - 5) / 12f, 0f, 1f);
            float titleGlow = MathF.Sin(time * 2f) * 0.12f;
            Color titleColor = Color.Lerp(new Color(255, 230, 200), new Color(255, 180, 120),
                0.3f + titleGlow);

            string displayTitle = title ?? "";
            Vector2 titleMeasure = font.MeasureString(displayTitle);
            Vector2 titleSize = titleMeasure * 0.85f;
            float titleScale = 0.85f;
            if (titleSize.X > availWidth) {
                titleScale = Math.Max(0.6f, availWidth / titleMeasure.X);
            }

            Vector2 titlePos = new(textX, rect.Y + 18);
            Utils.DrawBorderString(sb, displayTitle, titlePos,
                titleColor * (alpha * titleAlpha), titleScale);

            //标题下装饰横线
            Texture2D px = TextureAssets.MagicPixel.Value;
            int titleW = (int)(titleMeasure.X * titleScale);
            int lineW = Math.Min(titleW + 12, (int)availWidth);
            DrawGradientHLine(sb, px, (int)textX, (int)(titlePos.Y + titleSize.Y + 2),
                lineW, new Color(255, 140, 60) * (alpha * 0.25f), Color.Transparent);

            //描述文字 - 延迟淡入
            if (!string.IsNullOrEmpty(description)) {
                float descAlpha = MathHelper.Clamp((life - 12) / 14f, 0f, 1f);
                Color descColor = new Color(220, 190, 170) * (alpha * descAlpha * 0.85f);

                Vector2 descMeasure = font.MeasureString(description);
                float descScale = 0.68f;
                if (descMeasure.X * descScale > availWidth) {
                    descScale = Math.Max(0.5f, availWidth / descMeasure.X);
                }

                Vector2 descPos = new(textX, rect.Y + 44);
                Utils.DrawBorderString(sb, description, descPos, descColor, descScale);
            }
        }

        #endregion

        #region 底部装饰条

        private static void DrawBottomBar(SpriteBatch sb, Texture2D px,
            Rectangle rect, float alpha, int life) {
            float barY = rect.Bottom - 7;
            float barMaxW = rect.Width - 24;
            //填充用ease-out动画
            float fill = MathHelper.Clamp((life - 8) / 30f, 0f, 1f);
            fill = 1f - (1f - fill) * (1f - fill); //ease-out quadratic
            int fillW = (int)(barMaxW * fill);

            //背景槽
            sb.Draw(px, new Rectangle(rect.X + 12, (int)barY, (int)barMaxW, 3),
                Color.Black * (0.35f * alpha));

            //填充，分段渐变
            if (fillW > 0) {
                const int segs = 10;
                int segW = Math.Max(1, fillW / segs);
                for (int i = 0; i < segs; i++) {
                    int sx = rect.X + 12 + i * segW;
                    int w = i == segs - 1 ? fillW - i * segW : segW;
                    if (w <= 0) break;
                    float t = i / (float)segs;
                    Color c = Color.Lerp(new Color(255, 100, 40), new Color(255, 200, 100), t);
                    sb.Draw(px, new Rectangle(sx, (int)barY, w, 3), c * (alpha * 0.6f));
                }
            }
        }

        #endregion

        #region 辅助

        private static void DrawGradientHLine(SpriteBatch sb, Texture2D px,
            int x, int y, int width, Color from, Color to) {
            const int segs = 6;
            int segW = Math.Max(1, width / segs);
            for (int i = 0; i < segs; i++) {
                float t = i / (float)segs;
                int sx = x + i * segW;
                int w = i == segs - 1 ? width - i * segW : segW;
                if (w <= 0) break;
                sb.Draw(px, new Rectangle(sx, y, w, 1), Color.Lerp(from, to, t));
            }
        }

        #endregion
    }
}
