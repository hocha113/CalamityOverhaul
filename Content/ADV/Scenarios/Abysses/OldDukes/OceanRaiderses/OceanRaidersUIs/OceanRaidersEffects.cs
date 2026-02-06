using CalamityOverhaul.Content.ADV.UIEffect;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.OceanRaiderses.OceanRaidersUIs
{
    /// <summary>
    /// 海洋吞噬者UI视觉特效管理器
    /// </summary>
    internal class OceanRaidersEffects
    {
        //粒子列表
        private readonly List<BubblePRT> bubbles = [];
        private readonly List<AshPRT> ashParticles = [];
        private readonly List<SeaStarPRT> seaStars = [];

        //粒子刷新计时器
        private int bubbleSpawnTimer = 0;
        private int ashSpawnTimer = 0;
        private int starSpawnTimer = 0;

        private const float SulfseaSideMargin = 30f;

        /// <summary>
        /// 更新所有粒子和特效
        /// </summary>
        public void UpdateParticles(bool isActive, Vector2 panelPosition, int panelWidth, int panelHeight) {
            UpdateBubbles(isActive, panelPosition, panelWidth, panelHeight);
            UpdateAshParticles(isActive, panelPosition, panelWidth, panelHeight);
            UpdateSeaStars(isActive, panelPosition, panelWidth, panelHeight);
        }

        private void UpdateBubbles(bool isActive, Vector2 panelPosition, int panelWidth, int panelHeight) {
            float scaleW = Main.UIScale;
            bubbleSpawnTimer++;
            if (isActive && bubbleSpawnTimer >= 12 && bubbles.Count < 25) {
                bubbleSpawnTimer = 0;
                float left = panelPosition.X + SulfseaSideMargin * scaleW;
                float right = panelPosition.X + panelWidth - SulfseaSideMargin * scaleW;
                Vector2 start = new(Main.rand.NextFloat(left, right), panelPosition.Y + panelHeight - 10f);
                var bb = new BubblePRT(start) {
                    CoreColor = Color.LightYellow,
                    RimColor = Color.LimeGreen
                };
                bubbles.Add(bb);
            }

            for (int i = bubbles.Count - 1; i >= 0; i--) {
                if (bubbles[i].Update(panelPosition, new Vector2(panelWidth, panelHeight), SulfseaSideMargin)) {
                    bubbles.RemoveAt(i);
                }
            }
        }

        private void UpdateAshParticles(bool isActive, Vector2 panelPosition, int panelWidth, int panelHeight) {
            float scaleW = Main.UIScale;
            ashSpawnTimer++;
            if (isActive && ashSpawnTimer >= 18 && ashParticles.Count < 15) {
                ashSpawnTimer = 0;
                float left = panelPosition.X + SulfseaSideMargin * scaleW;
                float right = panelPosition.X + panelWidth - SulfseaSideMargin * scaleW;
                Vector2 start = new(Main.rand.NextFloat(left, right), panelPosition.Y + panelHeight - 10f);
                ashParticles.Add(new AshPRT(start));
            }

            for (int i = ashParticles.Count - 1; i >= 0; i--) {
                if (ashParticles[i].Update(panelPosition, new Vector2(panelWidth, panelHeight))) {
                    ashParticles.RemoveAt(i);
                }
            }
        }

        private void UpdateSeaStars(bool isActive, Vector2 panelPosition, int panelWidth, int panelHeight) {
            starSpawnTimer++;
            if (isActive && starSpawnTimer >= 35 && seaStars.Count < 8) {
                starSpawnTimer = 0;
                Vector2 p = panelPosition + new Vector2(
                    Main.rand.NextFloat(SulfseaSideMargin, panelWidth - SulfseaSideMargin),
                    Main.rand.NextFloat(56f, panelHeight - 56f)
                );
                seaStars.Add(new SeaStarPRT(p));
            }

            for (int i = seaStars.Count - 1; i >= 0; i--) {
                if (seaStars[i].Update(panelPosition, new Vector2(panelWidth, panelHeight))) {
                    seaStars.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 绘制所有特效
        /// </summary>
        public void DrawEffects(Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch, float uiAlpha) {
            //先绘制灰烬，在底层
            foreach (var ash in ashParticles) {
                ash.Draw(spriteBatch, uiAlpha * 0.75f);
            }

            //然后绘制气泡
            foreach (var b in bubbles) {
                b.DrawEnhanced(spriteBatch, uiAlpha * 0.9f);
            }

            //最后绘制海星
            foreach (var s in seaStars) {
                s.DrawEnhanced(spriteBatch, uiAlpha * 0.4f);
            }
        }

        /// <summary>
        /// 清空所有特效
        /// </summary>
        public void Clear() {
            bubbles.Clear();
            ashParticles.Clear();
            seaStars.Clear();
            bubbleSpawnTimer = 0;
            ashSpawnTimer = 0;
            starSpawnTimer = 0;
        }
    }
}
