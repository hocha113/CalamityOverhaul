using CalamityOverhaul.Content.ADV.UIEffect;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.ADV.Scenarios.Helen.Sulfseas.OldDukes.OldDukeShops
{
    /// <summary>
    /// 老公爵商店视觉效果管理器
    /// </summary>
    internal class OldDukeShopEffects
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
            bubbleSpawnTimer++;
            if (isActive && bubbleSpawnTimer >= 12 && bubbles.Count < 25) {
                bubbleSpawnTimer = 0;
                float left = panelPosition.X + SulfseaSideMargin;
                float right = panelPosition.X + panelWidth - SulfseaSideMargin;
                Vector2 start = new(Main.rand.NextFloat(left, right), panelPosition.Y + panelHeight - 10f);
                var bb = new BubblePRT(start);
                bb.CoreColor = Color.LightYellow;
                bb.RimColor = Color.LimeGreen;
                bubbles.Add(bb);
            }

            for (int i = bubbles.Count - 1; i >= 0; i--) {
                if (bubbles[i].Update(panelPosition, new Vector2(panelWidth, panelHeight), SulfseaSideMargin)) {
                    bubbles.RemoveAt(i);
                }
            }
        }

        private void UpdateAshParticles(bool isActive, Vector2 panelPosition, int panelWidth, int panelHeight) {
            ashSpawnTimer++;
            if (isActive && ashSpawnTimer >= 18 && ashParticles.Count < 15) {
                ashSpawnTimer = 0;
                float left = panelPosition.X + SulfseaSideMargin;
                float right = panelPosition.X + panelWidth - SulfseaSideMargin;
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
            if (isActive && starSpawnTimer >= 35 && seaStars.Count < 10) {
                starSpawnTimer = 0;
                Vector2 p = panelPosition + new Vector2(
                    Main.rand.NextFloat(SulfseaSideMargin, panelWidth - SulfseaSideMargin),
                    Main.rand.NextFloat(60f, panelHeight - 60f)
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
        public void DrawEffects(SpriteBatch spriteBatch, float uiAlpha) {
            //先绘制灰烬，后绘制气泡和星星
            foreach (var ash in ashParticles) {
                ash.Draw(spriteBatch, uiAlpha * 0.75f);
            }

            foreach (var bubble in bubbles) {
                bubble.DrawEnhanced(spriteBatch, uiAlpha * 0.9f);
            }

            foreach (var star in seaStars) {
                star.DrawEnhanced(spriteBatch, uiAlpha * 0.4f);
            }
        }

        /// <summary>
        /// 清除所有特效
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
