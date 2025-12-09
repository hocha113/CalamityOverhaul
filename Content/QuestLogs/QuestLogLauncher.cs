using CalamityOverhaul.Common;
using CalamityOverhaul.Content.QuestLogs.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.QuestLogs
{
    /// <summary>
    /// 任务书启动图标
    /// </summary>
    public class QuestLogLauncher
    {
        //图标位置和大小
        public Rectangle IconRect;
        //是否悬停
        public bool IsHovered;
        //图标动画计时器
        private float animTimer;
        //脉冲动画计时器
        private float pulseTimer;
        //发光强度
        private float glowIntensity;

        public QuestLogLauncher() {
            animTimer = 0f;
            pulseTimer = 0f;
            glowIntensity = 0f;
        }

        /// <summary>
        /// 更新图标状态
        /// </summary>
        /// <param name="position">图标位置</param>
        /// <param name="isOpen">任务书是否打开</param>
        public void Update(Vector2 position, bool isOpen) {
            //更新图标矩形
            int iconSize = 48;
            IconRect = new Rectangle((int)position.X, (int)position.Y, iconSize, iconSize);

            //检测鼠标悬停
            IsHovered = IconRect.Contains(Main.MouseScreen.ToPoint());

            //更新动画计时器
            animTimer += 0.05f;
            if (animTimer > MathHelper.TwoPi) {
                animTimer -= MathHelper.TwoPi;
            }

            pulseTimer += 0.04f;
            if (pulseTimer > MathHelper.TwoPi) {
                pulseTimer -= MathHelper.TwoPi;
            }

            //更新发光强度
            float targetGlow = (isOpen && IsHovered) ? 1f : 0f;
            glowIntensity = MathHelper.Lerp(glowIntensity, targetGlow, 0.15f);
        }

        /// <summary>
        /// 绘制图标
        /// </summary>
        /// <param name="spriteBatch">绘制批次</param>
        /// <param name="isOpen">任务书是否打开</param>
        public void Draw(SpriteBatch spriteBatch, bool isOpen) {
            if (QuestLog.QuestLogStart == null || QuestLog.QuestLogStart.Value == null) {
                return;
            }

            Texture2D iconTexture = QuestLog.QuestLogStart.Value;

            //计算帧索引
            //第0帧:关闭状态
            //第1帧:打开状态
            //第2帧:打开+悬停发光状态
            int frameIndex;
            if (!isOpen) {
                frameIndex = 0;
            }
            else if (IsHovered) {
                frameIndex = 2;
            }
            else {
                frameIndex = 1;
            }

            //计算单帧高度
            int frameHeight = iconTexture.Height / 3;
            Rectangle sourceRect = new Rectangle(0, frameHeight * frameIndex, iconTexture.Width, frameHeight);

            //绘制阴影
            Vector2 shadowOffset = new Vector2(3, 3);
            Color shadowColor = Color.Black * 0.6f;
            spriteBatch.Draw(iconTexture, new Vector2(IconRect.X, IconRect.Y) + shadowOffset,
                sourceRect, shadowColor, 0f, Vector2.Zero,
                new Vector2((float)IconRect.Width / iconTexture.Width, (float)IconRect.Height / frameHeight),
                SpriteEffects.None, 0f);

            //绘制主图标
            float scale = 1f;
            Color drawColor = Color.White;

            //悬停时的微弱呼吸效果
            if (IsHovered) {
                float breathe = (float)Math.Sin(animTimer * 2f) * 0.05f + 1f;
                scale *= breathe;
            }

            //计算绘制位置(居中缩放)
            Vector2 drawPos = new Vector2(
                IconRect.X + IconRect.Width / 2f,
                IconRect.Y + IconRect.Height / 2f
            );

            spriteBatch.Draw(iconTexture, drawPos, sourceRect, drawColor, 0f,
                new Vector2(iconTexture.Width / 2f, frameHeight / 2f),
                scale, SpriteEffects.None, 0f);

            //绘制额外的发光效果(当打开且悬停时)
            if (glowIntensity > 0.01f) {
                DrawGlowEffect(spriteBatch, iconTexture, sourceRect, drawPos, scale);
            }

            DrawNotificationBadge(spriteBatch);
        }

        private void DrawNotificationBadge(SpriteBatch spriteBatch) {
            int unclaimedCount = 0;
            foreach (var quest in QuestNode.AllQuests) {
                if (quest.IsCompleted && quest.Rewards != null) {
                    foreach (var reward in quest.Rewards) {
                        if (!reward.Claimed) {
                            unclaimedCount++;
                        }
                    }
                }
            }

            if (unclaimedCount > 0) {
                string text = unclaimedCount > 99 ? "99+" : unclaimedCount.ToString();
                Vector2 textSize = FontAssets.MouseText.Value.MeasureString(text) * 0.75f;
                float maxDim = Math.Max(textSize.X, textSize.Y);
                float bgSize = Math.Max(18, maxDim + 6);

                //红点位置在图标右上角
                Vector2 badgeCenter = new Vector2(IconRect.Right - 4, IconRect.Top + 4);
                Rectangle badgeRect = new Rectangle((int)(badgeCenter.X - bgSize / 2), (int)(badgeCenter.Y - bgSize / 2), (int)bgSize, (int)bgSize);

                Texture2D pixel = VaultAsset.placeholder2.Value;

                //绘制红点背景
                spriteBatch.Draw(pixel, badgeRect, Color.IndianRed);

                //绘制红点边框
                int border = 1;
                Color borderColor = Color.OrangeRed;
                spriteBatch.Draw(pixel, new Rectangle(badgeRect.X, badgeRect.Y, badgeRect.Width, border), borderColor);
                spriteBatch.Draw(pixel, new Rectangle(badgeRect.X, badgeRect.Bottom - border, badgeRect.Width, border), borderColor);
                spriteBatch.Draw(pixel, new Rectangle(badgeRect.X, badgeRect.Y, border, badgeRect.Height), borderColor);
                spriteBatch.Draw(pixel, new Rectangle(badgeRect.Right - border, badgeRect.Y, border, badgeRect.Height), borderColor);

                //绘制数字
                Vector2 textPos = new Vector2(badgeRect.X + badgeRect.Width / 2 - textSize.X / 2, badgeRect.Y + badgeRect.Height / 2 - textSize.Y / 2);
                Utils.DrawBorderString(spriteBatch, text, textPos, Color.White, 0.75f);
            }
        }

        private void DrawGlowEffect(SpriteBatch spriteBatch, Texture2D texture, Rectangle sourceRect, Vector2 position, float baseScale) {
            //绘制多层外发光
            int glowLayers = 3;
            for (int i = 0; i < glowLayers; i++) {
                float layerScale = baseScale * (1.2f + i * 0.15f);
                float layerAlpha = glowIntensity * (0.4f - i * 0.1f);

                //使用脉冲效果
                float pulse = (float)Math.Sin(pulseTimer + i * 0.5f) * 0.5f + 0.5f;
                layerAlpha *= pulse;

                //橙色发光
                Color glowColor = new Color(255, 180, 100) * layerAlpha;

                spriteBatch.Draw(texture, position, sourceRect, glowColor, 0f,
                    new Vector2(texture.Width / 2f, sourceRect.Height / 2f),
                    layerScale, SpriteEffects.None, 0f);
            }
        }

        /// <summary>
        /// 播放点击音效
        /// </summary>
        public void PlayClickSound(bool isOpening) {
            SoundEngine.PlaySound(isOpening ? CWRSound.ButtonZero with { Pitch = 0.1f, Volume = 0.6f } : CWRSound.ButtonZero with { Pitch = -0.1f, Volume = 0.6f });
        }
    }
}
