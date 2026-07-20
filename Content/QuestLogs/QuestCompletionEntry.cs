using CalamityOverhaul.Common;
using CalamityOverhaul.Content.QuestLogs.Core;
using CalamityOverhaul.Content.UIs.NotificationPopup;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.QuestLogs
{
    /// <summary>任务完成通知弹窗条目</summary>
    internal class QuestCompletionEntry : NotificationEntry
    {
        private readonly QuestNode node;

        private struct Spark
        {
            public Vector2 Pos;
            public Vector2 Vel;
            public float Phase;
            public float MaxLife;
            public float Size;
            public Color Tint;
        }
        private readonly Spark[] sparks = new Spark[16];
        private bool sparksInited;

        public override float Width => 320f;
        public override float Height => 78f;
        public override int SlideTime => 24;
        public override int DisplayTime => 210;
        public override float Gap => 6f;

        public QuestCompletionEntry(QuestNode node) {
            this.node = node;
        }

        public override bool OnClick() {
            SoundEngine.PlaySound(CWRSound.ButtonZero with { Volume = 0.6f, Pitch = -0.2f });
            return true;
        }

        public override void DrawContent(SpriteBatch sb, Rectangle panelRect, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            float time = Main.GameUpdateCount * 0.03f;
            int life = LifeTimer;

            string titleText = QuestNotificationSystem.Text1?.Value ?? "任务完成";
            string nameText = node.DisplayName?.Value ?? "";

            //面板背景，通用底色加特化装饰
            DrawPanelBackground(sb, panelRect, Color.Black * 0.75f, Color.Gold, alpha);
            DrawQuestPanelOverlay(sb, pixel, panelRect, alpha, time, life);

            float padding = 12;
            float iconAreaWidth = panelRect.Height - padding * 2;
            float iconCenterX = panelRect.X + padding + iconAreaWidth / 2;
            float iconCenterY = panelRect.Y + padding + iconAreaWidth / 2;

            //图标辉光
            DrawIconGlow(sb, new Vector2(iconCenterX, iconCenterY), iconAreaWidth, alpha, time);

            //竖向分隔线，3段渐变代替逐像素
            float separatorX = panelRect.X + padding * 2 + iconAreaWidth;
            int sepH = panelRect.Height - 16;
            int segH = sepH / 3;
            for (int i = 0; i < 3; i++) {
                float fade = i == 1 ? 0.4f : 0.15f;
                sb.Draw(pixel, new Rectangle((int)separatorX, panelRect.Y + 8 + i * segH, 1, segH),
                    Color.Gold * (fade * alpha));
            }

            //图标绘制
            var icon = node.GetIconTexture();
            if (icon != null) {
                Rectangle? frame = node.GetIconSourceRect(icon);
                Rectangle src = frame ?? icon.Frame();
                float maxDim = MathHelper.Max(src.Width, src.Height);
                float scale = (iconAreaWidth * 0.8f) / maxDim;

                float iconBounce = life < 20
                    ? 1f + MathF.Sin(life / 20f * MathF.PI) * 0.2f
                    : 1f + MathF.Sin(time * 3f) * 0.02f;
                scale *= iconBounce;

                Vector2 iconCenter = new(iconCenterX, iconCenterY);
                float iconRot = MathF.Sin(time * 1.5f) * 0.04f;

                sb.Draw(icon, iconCenter, src, Color.White * alpha, iconRot,
                    src.Size() / 2, scale, SpriteEffects.None, 0f);

                if (node.IsCompleted) {
                    DrawCheckMark(sb, pixel,
                        new Vector2(iconCenterX + iconAreaWidth * 0.3f, iconCenterY + iconAreaWidth * 0.3f),
                        alpha, life);
                }
            }

            //文字区域
            float textX = separatorX + 10;

            //标题辉光底色
            Vector2 titlePos = new(textX, panelRect.Y + 12);
            Vector2 titleSize = FontAssets.MouseText.Value.MeasureString(titleText) * 0.75f;
            sb.Draw(pixel, new Rectangle((int)titlePos.X - 2, (int)titlePos.Y, (int)(titleSize.X + 4), (int)(titleSize.Y + 2)),
                Color.Gold * (0.15f * alpha));

            float titleAlpha = MathHelper.Clamp((life - 5) / 10f, 0f, 1f);
            Color titleColor = Color.Lerp(Color.White, Color.Gold, 0.4f + MathF.Sin(time * 2f) * 0.15f);
            Utils.DrawBorderString(sb, titleText, titlePos, titleColor * (alpha * titleAlpha), 0.75f);

            float nameAlpha = MathHelper.Clamp((life - 10) / 12f, 0f, 1f);
            Vector2 namePos = new(textX, panelRect.Y + 34);
            Utils.DrawBorderString(sb, nameText, namePos, Color.White * (alpha * nameAlpha), 0.95f);

            //底部进度装饰条
            DrawProgressBar(sb, pixel, panelRect, alpha, life);

            //微型星火粒子
            DrawSparks(sb, pixel, panelRect, alpha, life);
        }

        /// <summary>任务完成弹窗特化装饰层，纵向渐变、扫光、脉冲边框、角标</summary>
        private static void DrawQuestPanelOverlay(SpriteBatch sb, Texture2D pixel, Rectangle rect, float alpha, float time, int life) {
            //纵向渐变叠加，4段
            int segH = rect.Height / 4;
            for (int i = 0; i < 4; i++) {
                float t = i / 4f;
                Color gradColor = Color.Lerp(new Color(40, 30, 15) * 0.3f, new Color(10, 8, 4) * 0.3f, t);
                sb.Draw(pixel, new Rectangle(rect.X, rect.Y + i * segH, rect.Width, segH + 1), gradColor * alpha);
            }

            //横向扫光，约6段模拟高斯分布
            float sweepPos = ((time * 0.4f + life * 0.01f) % 1.5f) - 0.25f;
            int sweepWidth = rect.Width / 4;
            int sweepCenterX = rect.X + (int)(sweepPos * (rect.Width + sweepWidth));
            int segments = 6;
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
                sb.Draw(pixel, new Rectangle(clampedX, rect.Y, w, rect.Height),
                    Color.Gold * (sweepAlpha * 0.12f * alpha));
            }

            //脉冲辉光上边框
            float pulse = MathF.Sin(time * 2.5f) * 0.5f + 0.5f;
            Color glowBorder = Color.Lerp(Color.Gold, Color.White, pulse * 0.3f);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y - 1, rect.Width, 1), glowBorder * (0.3f * pulse * alpha));

            //角落十字标记，上方两角亮下方两角暗
            DrawCornerGlyph(sb, pixel, new Vector2(rect.X + 5, rect.Y + 5), glowBorder, alpha, pulse);
            DrawCornerGlyph(sb, pixel, new Vector2(rect.Right - 5, rect.Y + 5), glowBorder, alpha, pulse);
            DrawCornerGlyph(sb, pixel, new Vector2(rect.X + 5, rect.Bottom - 5), glowBorder * 0.6f, alpha, pulse * 0.7f);
            DrawCornerGlyph(sb, pixel, new Vector2(rect.Right - 5, rect.Bottom - 5), glowBorder * 0.6f, alpha, pulse * 0.7f);

            //上部装饰细线
            sb.Draw(pixel, new Rectangle(rect.X + 8, rect.Y + 3, rect.Width - 16, 1),
                Color.Gold * (0.2f * alpha));
        }

        private static void DrawCornerGlyph(SpriteBatch sb, Texture2D pixel, Vector2 pos, Color color, float alpha, float pulse) {
            float size = 3f + pulse * 1.5f;
            sb.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), color * (0.7f * alpha), 0f,
                new Vector2(0.5f, 0.5f), new Vector2(size * 2f, 1f), SpriteEffects.None, 0f);
            sb.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), color * (0.6f * alpha), 0f,
                new Vector2(0.5f, 0.5f), new Vector2(1f, size * 2f), SpriteEffects.None, 0f);
            sb.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), Color.White * (0.4f * pulse * alpha), 0f,
                new Vector2(0.5f, 0.5f), new Vector2(1.5f, 1.5f), SpriteEffects.None, 0f);
        }

        /// <summary>底部进度装饰条（分段渐变）</summary>
        private static void DrawProgressBar(SpriteBatch sb, Texture2D pixel, Rectangle panelRect, float alpha, int life) {
            float progressBarY = panelRect.Bottom - 8;
            float barMaxWidth = panelRect.Width - 20;
            float fillProgress = MathHelper.Clamp((life - 8) / 25f, 0f, 1f);
            fillProgress = 1f - (1f - fillProgress) * (1f - fillProgress);
            int fillWidth = (int)(barMaxWidth * fillProgress);

            //背景槽
            sb.Draw(pixel, new Rectangle(panelRect.X + 10, (int)progressBarY, (int)barMaxWidth, 3),
                Color.Black * (0.4f * alpha));

            //填充，8段渐变
            if (fillWidth > 0) {
                int segs = 8;
                int segW = Math.Max(1, fillWidth / segs);
                for (int i = 0; i < segs; i++) {
                    int sx = panelRect.X + 10 + i * segW;
                    int sw = (i == segs - 1) ? (fillWidth - i * segW) : segW;
                    if (sw <= 0) break;
                    float t = (i + 0.5f) / segs;
                    Color fillColor = Color.Lerp(new Color(180, 120, 30), new Color(255, 220, 80), t);
                    sb.Draw(pixel, new Rectangle(sx, (int)progressBarY, sw, 3), fillColor * (0.8f * alpha));
                }

                //端点高光
                sb.Draw(pixel, new Rectangle(panelRect.X + 10 + fillWidth - 2, (int)progressBarY, 2, 3),
                    Color.White * (0.6f * alpha));
            }
        }

        private static void DrawIconGlow(SpriteBatch sb, Vector2 center, float size, float alpha, float time) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            float pulse = 0.6f + MathF.Sin(time * 3f) * 0.15f;
            if (glow != null) {
                Color glowColor = new Color(255, 200, 80, 0) * (0.25f * pulse * alpha);
                sb.Draw(glow, center, null, glowColor, 0f, glow.Size() / 2f,
                    size / glow.Width * 2.5f, SpriteEffects.None, 0f);
            }
            else {
                Texture2D pixel = VaultAsset.placeholder2.Value;
                Rectangle glowRect = new((int)(center.X - size * 0.6f), (int)(center.Y - size * 0.6f),
                    (int)(size * 1.2f), (int)(size * 1.2f));
                sb.Draw(pixel, glowRect, new Color(255, 200, 80) * (0.08f * pulse * alpha));
            }
        }

        private static void DrawCheckMark(SpriteBatch sb, Texture2D pixel, Vector2 pos, float alpha, int life) {
            float checkProgress = MathHelper.Clamp((life - 15) / 10f, 0f, 1f);
            if (checkProgress <= 0f) return;

            Color bgColor = new Color(50, 160, 70) * (0.85f * alpha * checkProgress);
            float bgSize = 10f;
            sb.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), bgColor, 0f,
                new Vector2(0.5f, 0.5f), new Vector2(bgSize, bgSize), SpriteEffects.None, 0f);

            Color checkColor = Color.White * (alpha * checkProgress);
            float shortLen = 4f * checkProgress;
            sb.Draw(pixel, pos + new Vector2(-3, 0), new Rectangle(0, 0, 1, 1), checkColor,
                MathHelper.PiOver4, new Vector2(0, 0.5f), new Vector2(shortLen, 1.5f), SpriteEffects.None, 0f);
            float longLen = 7f * checkProgress;
            sb.Draw(pixel, pos + new Vector2(-1, 2), new Rectangle(0, 0, 1, 1), checkColor,
                -MathHelper.PiOver4 * 0.7f, new Vector2(0, 0.5f), new Vector2(longLen, 1.5f), SpriteEffects.None, 0f);
        }

        /// <summary>帧率无关的星火粒子，位置基LifeTimer计算不在Draw中累加</summary>
        private void DrawSparks(SpriteBatch sb, Texture2D pixel, Rectangle panelRect, float alpha, int life) {
            if (!sparksInited) {
                sparksInited = true;
                Random rand = new(node.GetHashCode());
                for (int i = 0; i < sparks.Length; i++) {
                    sparks[i] = new Spark {
                        Pos = new Vector2(rand.Next(panelRect.Width), rand.Next(panelRect.Height)),
                        Vel = new Vector2((float)(rand.NextDouble() - 0.5) * 0.4f, (float)(rand.NextDouble() - 0.7) * 0.3f),
                        Phase = (float)(rand.NextDouble() * MathHelper.TwoPi),
                        MaxLife = 40f + rand.Next(80),
                        Size = 1f + (float)rand.NextDouble() * 1.5f,
                        Tint = Color.Lerp(new Color(255, 220, 100), new Color(255, 180, 60), (float)rand.NextDouble()),
                    };
                }
            }

            for (int i = 0; i < sparks.Length; i++) {
                ref readonly var sp = ref sparks[i];
                //基于life计算粒子生命周期位置，帧率无关
                float particleTime = (life + sp.Phase * 10f) % sp.MaxLife;
                float lifeRatio = particleTime / sp.MaxLife;
                float spAlpha = MathF.Sin(lifeRatio * MathF.PI);
                spAlpha = MathF.Pow(spAlpha, 1.5f);
                if (spAlpha < 0.02f) continue;

                //位置基于初始偏移加life线性位移
                float px = panelRect.X + ((sp.Pos.X + sp.Vel.X * life) % panelRect.Width + panelRect.Width) % panelRect.Width;
                float py = panelRect.Y + ((sp.Pos.Y + sp.Vel.Y * life) % panelRect.Height + panelRect.Height) % panelRect.Height;

                sb.Draw(pixel, new Vector2(px, py), new Rectangle(0, 0, 1, 1),
                    sp.Tint * (spAlpha * 0.45f * alpha), 0f,
                    new Vector2(0.5f, 0.5f), new Vector2(sp.Size, sp.Size),
                    SpriteEffects.None, 0f);
            }
        }
    }
}
