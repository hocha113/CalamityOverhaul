using CalamityOverhaul.Content.Narrative.Presentation.Skins.Common;
using CalamityOverhaul.Content.UIs.UIEffect;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.Narrative.Presentation.Skins.Draedon
{
    internal sealed class DraedonPanelState
    {
        public float CircuitPulseTimer;
        public float HologramFlicker;
        public float DataStreamTimer;
        public float SweepTimer;
        public float GlitchTimer;

        public readonly string[] CornerHex = ["0x????", "0x????", "0x????", "0x????"];

        private int hexUpdateClock;
        private static readonly char[] HexChars = "0123456789ABCDEF".ToCharArray();

        private readonly List<DraedonDataPRT> dataParticles = [];
        private int dataParticleSpawnTimer;
        private readonly List<CircuitNodePRT> circuitNodes = [];
        private int circuitNodeSpawnTimer;

        public float TechSideMargin { get; init; } = 28f;
        public int DataSpawnInterval { get; init; } = 30;
        public int MaxDataParticles { get; init; } = 10;
        public int CircuitSpawnInterval { get; init; } = 38;
        public int MaxCircuitNodes { get; init; } = 6;
        public float ParticleInsetY { get; init; } = 40f;

        public void Update(Rectangle panelRect, bool active) {
            SkinAnimUtil.Advance(ref CircuitPulseTimer, 0.025f);
            SkinAnimUtil.Advance(ref HologramFlicker, 0.13f);
            SkinAnimUtil.Advance(ref DataStreamTimer, 0.022f);
            SweepTimer = (SweepTimer + 0.008f) % 1f;
            SkinAnimUtil.Advance(ref GlitchTimer, 0.16f);

            hexUpdateClock++;
            if (hexUpdateClock >= 40) {
                hexUpdateClock = 0;
                for (int idx = 0; idx < CornerHex.Length; idx++) {
                    char[] buf = new char[4];
                    for (int c = 0; c < 4; c++) {
                        buf[c] = HexChars[Main.rand.Next(HexChars.Length)];
                    }
                    CornerHex[idx] = $"0x{new string(buf)}";
                }
            }

            float scaleW = Main.UIScale;
            Vector2 panelPos = panelRect.Location.ToVector2();
            Vector2 panelSize = panelRect.Size();

            dataParticleSpawnTimer++;
            if (active && dataParticleSpawnTimer >= DataSpawnInterval && dataParticles.Count < MaxDataParticles) {
                dataParticleSpawnTimer = 0;
                float left = panelRect.X + TechSideMargin * scaleW;
                float right = panelRect.Right - TechSideMargin * scaleW;
                dataParticles.Add(new DraedonDataPRT(new Vector2(
                    Main.rand.NextFloat(left, right),
                    panelRect.Y + Main.rand.NextFloat(ParticleInsetY, panelRect.Height - ParticleInsetY))));
            }
            for (int i = dataParticles.Count - 1; i >= 0; i--) {
                if (dataParticles[i].Update(panelPos, panelSize)) {
                    dataParticles.RemoveAt(i);
                }
            }

            circuitNodeSpawnTimer++;
            if (active && circuitNodeSpawnTimer >= CircuitSpawnInterval && circuitNodes.Count < MaxCircuitNodes) {
                circuitNodeSpawnTimer = 0;
                float left = panelRect.X + TechSideMargin * scaleW;
                float right = panelRect.Right - TechSideMargin * scaleW;
                circuitNodes.Add(new CircuitNodePRT(new Vector2(
                    Main.rand.NextFloat(left, right),
                    panelRect.Y + Main.rand.NextFloat(ParticleInsetY, panelRect.Height - ParticleInsetY))));
            }
            for (int i = circuitNodes.Count - 1; i >= 0; i--) {
                if (circuitNodes[i].Update(panelPos, panelSize)) {
                    circuitNodes.RemoveAt(i);
                }
            }
        }

        public void DrawParticles(SpriteBatch spriteBatch, float alpha, float circuitAlpha, float dataAlpha) {
            foreach (CircuitNodePRT node in circuitNodes) {
                node.Draw(spriteBatch, alpha * circuitAlpha);
            }
            foreach (DraedonDataPRT particle in dataParticles) {
                particle.Draw(spriteBatch, alpha * dataAlpha);
            }
        }

        public void Reset() {
            CircuitPulseTimer = 0f;
            HologramFlicker = 0f;
            DataStreamTimer = 0f;
            SweepTimer = 0f;
            GlitchTimer = 0f;
            hexUpdateClock = 0;
            dataParticles.Clear();
            circuitNodes.Clear();
            dataParticleSpawnTimer = 0;
            circuitNodeSpawnTimer = 0;
            for (int i = 0; i < CornerHex.Length; i++) {
                CornerHex[i] = "0x????";
            }
        }
    }
}
