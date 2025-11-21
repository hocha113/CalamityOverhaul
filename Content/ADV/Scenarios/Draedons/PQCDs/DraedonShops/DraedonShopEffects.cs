using CalamityOverhaul.Content.ADV.UIEffect;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.ADV.Scenarios.Draedons.PQCDs.DraedonShops
{
    /// <summary>
    /// 视觉特效管理器
    /// </summary>
    internal class DraedonShopEffects
    {
        //粒子列表
        private readonly List<DraedonDataPRT> dataParticles = new();
        private readonly List<CircuitNodePRT> circuitNodes = new();
        private readonly List<EnergyLinePRT> energyLines = new();

        //粒子刷新计时器
        private int dataParticleSpawnTimer = 0;
        private int circuitNodeSpawnTimer = 0;
        private int energyLineSpawnTimer = 0;

        private const float TechSideMargin = 35f;

        /// <summary>
        /// 更新所有粒子特效
        /// </summary>
        public void UpdateParticles(bool isActive, Vector2 panelPosition, int panelWidth, int panelHeight) {
            UpdateDataParticles(isActive, panelPosition, panelWidth, panelHeight);
            UpdateCircuitNodes(isActive, panelPosition, panelWidth, panelHeight);
            UpdateEnergyLines(isActive, panelPosition, panelWidth, panelHeight);
        }

        private void UpdateDataParticles(bool isActive, Vector2 panelPosition, int panelWidth, int panelHeight) {
            dataParticleSpawnTimer++;
            if (isActive && dataParticleSpawnTimer >= 15 && dataParticles.Count < 30) {
                dataParticleSpawnTimer = 0;
                Vector2 spawnPos = panelPosition + new Vector2(
                    Main.rand.NextFloat(TechSideMargin, panelWidth - TechSideMargin),
                    Main.rand.NextFloat(50f, panelHeight - 50f)
                );
                dataParticles.Add(new DraedonDataPRT(spawnPos));
            }
            for (int i = dataParticles.Count - 1; i >= 0; i--) {
                if (dataParticles[i].Update(panelPosition, new Vector2(panelWidth, panelHeight))) {
                    dataParticles.RemoveAt(i);
                }
            }
        }

        private void UpdateCircuitNodes(bool isActive, Vector2 panelPosition, int panelWidth, int panelHeight) {
            circuitNodeSpawnTimer++;
            if (isActive && circuitNodeSpawnTimer >= 30 && circuitNodes.Count < 12) {
                circuitNodeSpawnTimer = 0;
                Vector2 spawnPos = panelPosition + new Vector2(
                    Main.rand.NextFloat(TechSideMargin, panelWidth - TechSideMargin),
                    Main.rand.NextFloat(80f, panelHeight - 80f)
                );
                circuitNodes.Add(new CircuitNodePRT(spawnPos));
            }
            for (int i = circuitNodes.Count - 1; i >= 0; i--) {
                if (circuitNodes[i].Update(panelPosition, new Vector2(panelWidth, panelHeight))) {
                    circuitNodes.RemoveAt(i);
                }
            }
        }

        private void UpdateEnergyLines(bool isActive, Vector2 panelPosition, int panelWidth, int panelHeight) {
            energyLineSpawnTimer++;
            if (isActive && energyLineSpawnTimer >= 40 && energyLines.Count < 8) {
                energyLineSpawnTimer = 0;
                bool isVertical = Main.rand.NextBool();
                Vector2 start, end;
                if (isVertical) {
                    float x = panelPosition.X + Main.rand.NextFloat(TechSideMargin, panelWidth - TechSideMargin);
                    start = new Vector2(x, panelPosition.Y + 70);
                    end = new Vector2(x, panelPosition.Y + panelHeight - 30);
                }
                else {
                    float y = panelPosition.Y + Main.rand.NextFloat(100f, panelHeight - 100f);
                    start = new Vector2(panelPosition.X + TechSideMargin, y);
                    end = new Vector2(panelPosition.X + panelWidth - TechSideMargin, y);
                }
                energyLines.Add(new EnergyLinePRT(start, end));
            }
            for (int i = energyLines.Count - 1; i >= 0; i--) {
                if (energyLines[i].Update()) {
                    energyLines.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 绘制所有特效
        /// </summary>
        public void DrawEffects(SpriteBatch spriteBatch, float uiAlpha) {
            foreach (var line in energyLines) {
                line.Draw(spriteBatch, uiAlpha);
            }

            foreach (var node in circuitNodes) {
                node.Draw(spriteBatch, uiAlpha * 0.85f);
            }

            foreach (var particle in dataParticles) {
                particle.Draw(spriteBatch, uiAlpha * 0.75f);
            }
        }

        /// <summary>
        /// 清理所有特效
        /// </summary>
        public void Clear() {
            dataParticles.Clear();
            circuitNodes.Clear();
            energyLines.Clear();
        }
    }
}
