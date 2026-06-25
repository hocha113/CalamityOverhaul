using CalamityOverhaul.Content.Narrative.Presentation.Skins.Common;
using CalamityOverhaul.Content.UIs.UIEffect;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.Narrative.Presentation.Skins.Sulfsea
{
    internal sealed class SulfseaPanelState
    {
        public const int ShaderEdgePad = 16;

        public float PanelPulse;
        public float ToxicWavePhase;
        public float SulfurPulse;
        public float MiasmaTimer;
        public float ShaderTime;

        private readonly List<SeaStarPRT> _stars = [];
        private readonly List<BubblePRT> _bubbles = [];
        private readonly List<AshPRT> _ashes = [];
        private int _starSpawnTimer;
        private int _bubbleSpawnTimer;
        private int _ashSpawnTimer;

        private const float BubbleSideMargin = 34f;

        public void Update(Rectangle panelRect, bool active, bool includeStars = true) {
            PanelPulse = SkinAnimUtil.WrapTimer(PanelPulse, 0.028f);
            ToxicWavePhase = SkinAnimUtil.WrapTimer(ToxicWavePhase, 0.022f);
            SulfurPulse = SkinAnimUtil.WrapTimer(SulfurPulse, 0.015f);
            MiasmaTimer = SkinAnimUtil.WrapTimer(MiasmaTimer, 0.032f);
            ShaderTime = SkinAnimUtil.AdvanceShaderTime(ShaderTime);

            if (!active) {
                return;
            }

            Vector2 panelPos = panelRect.Location.ToVector2();
            Vector2 panelSize = panelRect.Size();

            _starSpawnTimer++;
            if (includeStars && _starSpawnTimer >= 35 && _stars.Count < 8) {
                _starSpawnTimer = 0;
                Vector2 p = panelPos + new Vector2(
                    Main.rand.NextFloat(BubbleSideMargin, panelSize.X - BubbleSideMargin),
                    Main.rand.NextFloat(56f, panelSize.Y - 56f));
                _stars.Add(new SeaStarPRT(p));
            }
            UpdateParticles(_stars, panelPos, panelSize, (star, pos, size) => star.Update(pos, size));

            float scaleW = Main.UIScale;
            _bubbleSpawnTimer++;
            if (_bubbleSpawnTimer >= 12 && _bubbles.Count < 25) {
                _bubbleSpawnTimer = 0;
                float left = panelPos.X + BubbleSideMargin * scaleW;
                float right = panelPos.X + panelSize.X - BubbleSideMargin * scaleW;
                Vector2 start = new(Main.rand.NextFloat(left, right), panelPos.Y + panelSize.Y - 10f);
                var bubble = new BubblePRT(start) {
                    CoreColor = Color.LightYellow,
                    RimColor = Color.LimeGreen
                };
                _bubbles.Add(bubble);
            }
            UpdateParticles(_bubbles, panelPos, panelSize, (bubble, pos, size) => bubble.Update(pos, size, BubbleSideMargin));

            _ashSpawnTimer++;
            if (_ashSpawnTimer >= 18 && _ashes.Count < 15) {
                _ashSpawnTimer = 0;
                float left = panelPos.X + BubbleSideMargin * scaleW;
                float right = panelPos.X + panelSize.X - BubbleSideMargin * scaleW;
                Vector2 start = new(Main.rand.NextFloat(left, right), panelPos.Y + panelSize.Y - 10f);
                _ashes.Add(new AshPRT(start));
            }
            UpdateParticles(_ashes, panelPos, panelSize, (ash, pos, size) => ash.Update(pos, size));
        }

        public void DrawForeground(SpriteBatch spriteBatch, float alpha) {
            foreach (AshPRT ash in _ashes) {
                ash.Draw(spriteBatch, alpha * 0.75f);
            }
            foreach (BubblePRT bubble in _bubbles) {
                bubble.DrawEnhanced(spriteBatch, alpha * 0.9f);
            }
            foreach (SeaStarPRT star in _stars) {
                star.DrawEnhanced(spriteBatch, alpha * 0.4f);
            }
        }

        public void Reset() {
            PanelPulse = 0f;
            ToxicWavePhase = 0f;
            SulfurPulse = 0f;
            MiasmaTimer = 0f;
            ShaderTime = 0f;
            _stars.Clear();
            _bubbles.Clear();
            _ashes.Clear();
            _starSpawnTimer = 0;
            _bubbleSpawnTimer = 0;
            _ashSpawnTimer = 0;
        }

        private static void UpdateParticles<T>(List<T> list, Vector2 panelPos, Vector2 panelSize, System.Func<T, Vector2, Vector2, bool> update) {
            for (int i = list.Count - 1; i >= 0; i--) {
                if (update(list[i], panelPos, panelSize)) {
                    list.RemoveAt(i);
                }
            }
        }
    }
}
