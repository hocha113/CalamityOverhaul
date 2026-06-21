using CalamityOverhaul.Content.Narrative.Presentation.Skins.Common;
using CalamityOverhaul.Content.UIs.UIEffect;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.Narrative.Presentation.Skins.SHPC
{
    internal sealed class SHPCPanelState
    {
        public const int EdgePad = 20;

        public float NeonPulse;
        public float SweepTimer;
        public float DataFlow;

        private readonly float[] _dataLinePhases = new float[2];
        private readonly string[] _cornerStatus = ["LINK.OK", "SYS:RDY", "v2.07b", "SYNC.."];
        private int _statusUpdateClock;

        private readonly List<NeonMaidPRT> _neonParticles = [];
        private readonly List<CircuitNodePRT> _circuitNodes = [];
        private int _neonSpawnTimer;
        private int _circuitSpawnTimer;
        private const float SideMargin = 24f;

        private static readonly Color NeonBlue = new(60, 120, 255);
        private static readonly Color NeonBlueDim = new(40, 60, 180);
        private static readonly Color DeepPurple = new(100, 40, 200);
        private static readonly Color PanelDark = new(10, 6, 22);
        private static readonly string[] StatusPool = [
            "MAID.OK", "SYS:RDY", "LINK.UP", "ACT:ON", "v2.07b",
            "NRG:98%", "SYNC..", "CORE:A+", "NET.OK", "STB:Hi",
            "IO:PASS", "CHK:OK", "MOD:RUN", "BUF:CLR", "SIG:99"
        ];

        public void Update(Rectangle panelRect, bool active, bool dialogueDecorations = false) {
            NeonPulse = SkinAnimUtil.WrapTimer(NeonPulse, dialogueDecorations ? 0.028f : 0.035f);
            DataFlow = SkinAnimUtil.WrapTimer(DataFlow, dialogueDecorations ? 0.018f : 0.028f);
            SweepTimer = SkinAnimUtil.AdvanceShaderTime(SweepTimer, 0.016f);

            if (dialogueDecorations) {
                for (int i = 0; i < _dataLinePhases.Length; i++) {
                    _dataLinePhases[i] = (_dataLinePhases[i] + 0.014f + i * 0.005f) % 1f;
                }

                _statusUpdateClock++;
                if (_statusUpdateClock >= 55) {
                    _statusUpdateClock = 0;
                    for (int i = 0; i < _cornerStatus.Length; i++) {
                        _cornerStatus[i] = StatusPool[Main.rand.Next(StatusPool.Length)];
                    }
                }
            }

            if (!active) {
                return;
            }

            Vector2 panelPos = panelRect.Location.ToVector2();
            Vector2 panelSize = panelRect.Size();
            float scaleW = Main.UIScale;

            int neonInterval = dialogueDecorations ? 28 : 22;
            int neonMax = dialogueDecorations ? 8 : 10;
            _neonSpawnTimer++;
            if (_neonSpawnTimer >= neonInterval && _neonParticles.Count < neonMax) {
                _neonSpawnTimer = 0;
                float left = panelPos.X + SideMargin * scaleW;
                float right = panelPos.X + panelSize.X - SideMargin * scaleW;
                float insetY = dialogueDecorations ? 30f : 20f;
                _neonParticles.Add(new NeonMaidPRT(new Vector2(
                    Main.rand.NextFloat(left, right),
                    Main.rand.NextFloat(panelPos.Y + insetY, panelPos.Y + panelSize.Y - insetY))));
            }
            UpdateParticles(_neonParticles, panelPos, panelSize);

            int circuitInterval = dialogueDecorations ? 38 : 30;
            int circuitMax = dialogueDecorations ? 4 : 6;
            _circuitSpawnTimer++;
            if (_circuitSpawnTimer >= circuitInterval && _circuitNodes.Count < circuitMax) {
                _circuitSpawnTimer = 0;
                float left = panelPos.X + SideMargin * scaleW;
                float right = panelPos.X + panelSize.X - SideMargin * scaleW;
                float insetY = dialogueDecorations ? 30f : 24f;
                _circuitNodes.Add(new CircuitNodePRT(new Vector2(
                    Main.rand.NextFloat(left, right),
                    Main.rand.NextFloat(panelPos.Y + insetY, panelPos.Y + panelSize.Y - insetY))));
            }
            UpdateNodes(panelPos, panelSize);
        }

        public void DrawParticles(SpriteBatch spriteBatch, float alpha, bool dialogueDecorations = false) {
            float nodeAlpha = dialogueDecorations ? 0.5f : 0.7f;
            float neonAlpha = dialogueDecorations ? 0.55f : 0.75f;
            foreach (CircuitNodePRT node in _circuitNodes) {
                node.Draw(spriteBatch, alpha * nodeAlpha);
            }
            foreach (NeonMaidPRT particle in _neonParticles) {
                particle.Draw(spriteBatch, alpha * neonAlpha);
            }
        }

        public void Reset() {
            NeonPulse = 0f;
            SweepTimer = 0f;
            DataFlow = 0f;
            _neonParticles.Clear();
            _circuitNodes.Clear();
            _neonSpawnTimer = 0;
            _circuitSpawnTimer = 0;
            _statusUpdateClock = 0;
            Array.Clear(_dataLinePhases, 0, _dataLinePhases.Length);
            _cornerStatus[0] = "LINK.OK";
            _cornerStatus[1] = "SYS:RDY";
            _cornerStatus[2] = "v2.07b";
            _cornerStatus[3] = "SYNC..";
        }

        public static Color NeonBlueColor => NeonBlue;
        public static Color NeonBlueDimColor => NeonBlueDim;
        public static Color PanelDarkColor => PanelDark;

        internal ReadOnlySpan<string> CornerStatus => _cornerStatus;
        internal ReadOnlySpan<float> DataLinePhases => _dataLinePhases;

        private static void UpdateParticles(List<NeonMaidPRT> list, Vector2 pos, Vector2 size) {
            for (int i = list.Count - 1; i >= 0; i--) {
                if (list[i].Update(pos, size)) {
                    list.RemoveAt(i);
                }
            }
        }

        private void UpdateNodes(Vector2 pos, Vector2 size) {
            for (int i = _circuitNodes.Count - 1; i >= 0; i--) {
                if (_circuitNodes[i].Update(pos, size)) {
                    _circuitNodes.RemoveAt(i);
                }
            }
        }
    }

    internal static class SHPCPanelDraw
    {
        public static void DrawCyberBackground(SpriteBatch spriteBatch, Rectangle rect, float alpha, SHPCPanelState state, bool layeredShadow = false) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            if (layeredShadow) {
                for (int d = 6; d >= 1; d--) {
                    Rectangle shadow = rect;
                    shadow.Inflate(d, d);
                    shadow.Offset(3, 4);
                    spriteBatch.Draw(pixel, shadow, new Rectangle(0, 0, 1, 1), new Color(6, 3, 12) * (alpha * 0.09f * (6f - d) / 6f));
                }
            }
            else {
                SkinDrawUtil.DrawPanelShadow(spriteBatch, rect, Color.Black * (alpha * 0.55f), 5, 7);
            }

            CyberShaderPanel.Draw(spriteBatch, rect, alpha * 0.97f, state.SweepTimer, SHPCPanelState.EdgePad, Color.White);
        }

        public static void DrawDialogueDecorations(SpriteBatch spriteBatch, Rectangle rect, float alpha, SHPCPanelState state) {
            DrawDataFlowLines(spriteBatch, rect, alpha, state);
            DrawCornerBrackets(spriteBatch, rect, alpha, state.NeonPulse);
            DrawCornerStatusText(spriteBatch, rect, alpha, state);
        }

        public static void DrawPortraitFrame(SpriteBatch spriteBatch, Rectangle frame, float alpha, float pulse) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            spriteBatch.Draw(pixel, frame, new Rectangle(0, 0, 1, 1), SHPCPanelState.PanelDarkColor * (alpha * 0.95f));
            Color border = SHPCPanelState.NeonBlueColor * (alpha * 0.7f * pulse);
            SkinDrawUtil.DrawRectBorder(spriteBatch, frame, border, 2);
            Rectangle outer = frame;
            outer.Inflate(2, 2);
            SkinDrawUtil.DrawRectBorder(spriteBatch, outer, SHPCPanelState.NeonBlueColor * (alpha * 0.12f * pulse), 1);
        }

        private static void DrawDataFlowLines(SpriteBatch sb, Rectangle rect, float alpha, SHPCPanelState state) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            int[] xOffsets = [9, 18];
            ReadOnlySpan<float> phases = state.DataLinePhases;

            for (int lineIdx = 0; lineIdx < 2; lineIdx++) {
                int lx = rect.X + xOffsets[lineIdx];
                int lineLen = (int)(rect.Height * 0.5f);
                int startY = rect.Y + (int)(phases[lineIdx] * rect.Height);

                for (int dy = 0; dy < lineLen; dy++) {
                    int py = startY + dy;
                    if (py > rect.Bottom) {
                        py -= rect.Height;
                    }
                    if (py < rect.Y || py >= rect.Bottom) {
                        continue;
                    }

                    float t = dy / (float)lineLen;
                    float br = MathF.Sin(t * MathHelper.Pi) * 0.7f + 0.2f;
                    Color color = Color.Lerp(SHPCPanelState.NeonBlueColor, new Color(100, 40, 200), t * 0.7f) * (alpha * br * 0.55f);
                    sb.Draw(pixel, new Rectangle(lx, py, 2, 1), new Rectangle(0, 0, 1, 1), color);
                    sb.Draw(pixel, new Rectangle(lx - 1, py, 1, 1), new Rectangle(0, 0, 1, 1), color * 0.18f);
                    sb.Draw(pixel, new Rectangle(lx + 2, py, 1, 1), new Rectangle(0, 0, 1, 1), color * 0.18f);
                }
            }

            sb.Draw(pixel, new Rectangle(rect.X + 5, rect.Y + 6, 1, rect.Height - 12),
                new Rectangle(0, 0, 1, 1), SHPCPanelState.NeonBlueDimColor * (alpha * 0.18f));
        }

        private static void DrawCornerBrackets(SpriteBatch sb, Rectangle rect, float alpha, float neonPulse) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            float pulse = MathF.Sin(neonPulse * 0.9f) * 0.1f + 0.9f;
            Color bc = SHPCPanelState.NeonBlueColor * (alpha * 0.3f * pulse);
            const int arm = 14;

            sb.Draw(pixel, new Rectangle(rect.Right - 6, rect.Bottom - 6 - arm, 1, arm), new Rectangle(0, 0, 1, 1), bc);
            sb.Draw(pixel, new Rectangle(rect.Right - 6 - arm, rect.Bottom - 6, arm, 1), new Rectangle(0, 0, 1, 1), bc);

            int midX = rect.X + rect.Width / 2;
            sb.Draw(pixel, new Rectangle(midX - 20, rect.Bottom - 4, 16, 1), new Rectangle(0, 0, 1, 1), bc * 0.7f);
            sb.Draw(pixel, new Rectangle(midX + 4, rect.Bottom - 4, 16, 1), new Rectangle(0, 0, 1, 1), bc * 0.7f);
        }

        private static void DrawCornerStatusText(SpriteBatch sb, Rectangle rect, float alpha, SHPCPanelState state) {
            if (alpha < 0.04f) {
                return;
            }

            float blink = MathF.Sin(state.NeonPulse * 0.7f) * 0.12f + 0.88f;
            Color col = SHPCPanelState.NeonBlueDimColor * (alpha * 0.4f * blink);
            const float scale = 0.5f;
            var font = FontAssets.MouseText.Value;
            ReadOnlySpan<string> status = state.CornerStatus;

            Utils.DrawBorderString(sb, status[0], new Vector2(rect.X + 28f, rect.Y + 7f), col, scale);
            float w1 = font.MeasureString(status[1]).X * scale;
            Utils.DrawBorderString(sb, status[1], new Vector2(rect.Right - w1 - 16f, rect.Y + 7f), col, scale);
            Utils.DrawBorderString(sb, status[2], new Vector2(rect.X + 8f, rect.Bottom - 16f), col * 0.6f, scale);
            float w3 = font.MeasureString(status[3]).X * scale;
            Utils.DrawBorderString(sb, status[3], new Vector2(rect.Right - w3 - 16f, rect.Bottom - 16f), col * 0.6f, scale);
        }
    }
}
