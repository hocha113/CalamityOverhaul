using CalamityOverhaul.Content.Narrative.Presentation.Skins.Common;
using CalamityOverhaul.Content.UIs.UIEffect;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.Narrative.Presentation.Skins.Tzeentch
{
    internal sealed class TzeentchPanelState
    {
        public const int ShaderEdgePad = 16;

        public float WarpTimer;
        public float SchemePulse;
        public float ShaderTime;

        private readonly List<TzeentchRunePRT> _runes = [];
        private int _runeSpawnTimer;

        private const float SideMargin = 30f;

        /// <summary>0~1 变数脉动</summary>
        public float Warp01 => (float)System.Math.Sin(WarpTimer * 1.1f) * 0.5f + 0.5f;

        public void Update(Rectangle panelRect, bool active, bool includeRunes = true) {
            WarpTimer = SkinAnimUtil.WrapTimer(WarpTimer, 0.026f);
            SchemePulse = SkinAnimUtil.WrapTimer(SchemePulse, 0.016f);
            ShaderTime = SkinAnimUtil.AdvanceShaderTime(ShaderTime);

            if (!active) {
                return;
            }

            Vector2 panelPos = panelRect.Location.ToVector2();
            Vector2 panelSize = panelRect.Size();
            float scaleW = Main.UIScale;

            _runeSpawnTimer++;
            if (includeRunes && _runeSpawnTimer >= 16 && _runes.Count < 14) {
                _runeSpawnTimer = 0;
                float left = panelPos.X + SideMargin * scaleW;
                float right = panelPos.X + panelSize.X - SideMargin * scaleW;
                Vector2 start = new(Main.rand.NextFloat(left, right), panelPos.Y + panelSize.Y - 10f);
                _runes.Add(new TzeentchRunePRT(start));
            }

            for (int i = _runes.Count - 1; i >= 0; i--) {
                if (_runes[i].Update(panelPos, panelSize)) {
                    _runes.RemoveAt(i);
                }
            }
        }

        public void DrawForeground(SpriteBatch spriteBatch, float alpha) {
            foreach (TzeentchRunePRT rune in _runes) {
                rune.Draw(spriteBatch, alpha * 0.85f);
            }
        }

        public void Reset() {
            WarpTimer = 0f;
            SchemePulse = 0f;
            ShaderTime = 0f;
            _runes.Clear();
            _runeSpawnTimer = 0;
        }
    }
}
