using CalamityOverhaul.Content.Narrative.Presentation.Skins.Common;
using CalamityOverhaul.Content.UIs.UIEffect;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.Narrative.Presentation.Skins.Sea
{
    internal sealed class SeaPanelState
    {
        public const int ShaderEdgePad = 16;

        public float WavePhase;
        public float AbyssPulse;
        public float PanelPulse;
        public float ShaderTime;

        private readonly List<BubblePRT> _bubbles = [];
        private readonly List<SeaStarPRT> _stars = [];
        private int _bubbleTimer;
        private int _starTimer;
        private const float BubbleSideMargin = 34f;
        private bool _popupMode;

        public void Update(Rectangle panelRect, bool active, bool popupMode = false, float panelAlpha = 1f) {
            _popupMode = popupMode;
            WavePhase = SkinAnimUtil.WrapTimer(WavePhase, 0.02f);
            AbyssPulse = SkinAnimUtil.WrapTimer(AbyssPulse, 0.013f);
            PanelPulse = SkinAnimUtil.WrapTimer(PanelPulse, 0.025f);
            ShaderTime = SkinAnimUtil.AdvanceShaderTime(ShaderTime);

            if (!active) {
                return;
            }

            if (popupMode) {
                UpdatePopupParticles(panelRect, panelAlpha);
                return;
            }

            Vector2 panelPos = panelRect.Location.ToVector2();
            Vector2 panelSize = panelRect.Size();
            float scaleW = Main.UIScale;

            _bubbleTimer++;
            if (_bubbleTimer >= 16 && _bubbles.Count < 20) {
                _bubbleTimer = 0;
                float left = panelPos.X + BubbleSideMargin * scaleW;
                float right = panelPos.X + panelSize.X - BubbleSideMargin * scaleW;
                _bubbles.Add(new BubblePRT(new Vector2(Main.rand.NextFloat(left, right), panelPos.Y + panelSize.Y - 10f)));
            }
            UpdateList(_bubbles, panelPos, panelSize, (b, p, s) => b.Update(p, s, BubbleSideMargin));

            _starTimer++;
            if (_starTimer >= 35 && _stars.Count < 8) {
                _starTimer = 0;
                _stars.Add(new SeaStarPRT(panelPos + new Vector2(
                    Main.rand.NextFloat(BubbleSideMargin, panelSize.X - BubbleSideMargin),
                    Main.rand.NextFloat(56f, panelSize.Y - 56f))));
            }
            UpdateList(_stars, panelPos, panelSize, (s, p, sz) => s.Update(p, sz));
        }

        private void UpdatePopupParticles(Rectangle panelRect, float panelAlpha) {
            Vector2 basePos = panelRect.Center.ToVector2();
            if (panelAlpha <= 0.6f) {
                return;
            }

            _bubbleTimer++;
            if (_bubbleTimer >= 8 && _bubbles.Count < 20) {
                _bubbleTimer = 0;
                _bubbles.Add(new BubblePRT(basePos + new Vector2(Main.rand.NextFloat(-80f, 80f), 40f)));
            }
            for (int i = _bubbles.Count - 1; i >= 0; i--) {
                if (_bubbles[i].Update()) {
                    _bubbles.RemoveAt(i);
                }
            }

            _starTimer++;
            if (_starTimer >= 18 && _stars.Count < 12) {
                _starTimer = 0;
                _stars.Add(new SeaStarPRT(basePos + new Vector2(
                    Main.rand.NextFloat(-100f, 100f),
                    Main.rand.NextFloat(-60f, 20f))));
            }
            for (int i = _stars.Count - 1; i >= 0; i--) {
                if (_stars[i].Update()) {
                    _stars.RemoveAt(i);
                }
            }
        }

        public void DrawForeground(SpriteBatch spriteBatch, float alpha) {
            float bubbleAlpha = _popupMode ? 0.85f : 0.9f;
            float starAlpha = _popupMode ? 0.5f : 0.45f;
            foreach (BubblePRT bubble in _bubbles) {
                if (_popupMode) {
                    bubble.Draw(spriteBatch, alpha * bubbleAlpha);
                }
                else {
                    bubble.DrawEnhanced(spriteBatch, alpha * bubbleAlpha);
                }
            }
            foreach (SeaStarPRT star in _stars) {
                if (_popupMode) {
                    star.Draw(spriteBatch, alpha * starAlpha);
                }
                else {
                    star.DrawEnhanced(spriteBatch, alpha * starAlpha);
                }
            }
        }

        public void Reset() {
            WavePhase = 0f;
            AbyssPulse = 0f;
            PanelPulse = 0f;
            ShaderTime = 0f;
            _bubbles.Clear();
            _stars.Clear();
            _bubbleTimer = 0;
            _starTimer = 0;
            _popupMode = false;
        }

        private static void UpdateList<T>(List<T> list, Vector2 pos, Vector2 size, Func<T, Vector2, Vector2, bool> update) {
            for (int i = list.Count - 1; i >= 0; i--) {
                if (update(list[i], pos, size)) {
                    list.RemoveAt(i);
                }
            }
        }
    }

    internal static class SeaPanelDraw
    {
        public static void DrawShaderBackground(SpriteBatch spriteBatch, Rectangle rect, float alpha, SeaPanelState state, float hoverGlow = 0f) {
            SkinDrawUtil.DrawPanelShadow(spriteBatch, rect, Color.Black * (alpha * 0.50f), 6, 8);
            float pulse01 = (float)Math.Sin(state.AbyssPulse * 1.6f) * 0.5f + 0.5f;
            float bright = MathHelper.Clamp(0.95f + hoverGlow * 0.30f, 0f, 1.4f);
            Color tint = new Color(
                (byte)Math.Min(255, (int)(220 * bright)),
                (byte)Math.Min(255, (int)(238 * bright)),
                (byte)255);
            SeaShaderPanel.Draw(spriteBatch, rect, alpha * 0.97f, pulse01, state.ShaderTime, SeaPanelState.ShaderEdgePad, tint);
        }

        public static void DrawPopupFrame(SpriteBatch spriteBatch, Rectangle rect, float alpha, float hoverGlow) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Color edge = new Color(70, 180, 230) * (alpha * (0.85f + hoverGlow * 0.3f));
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 2), new Rectangle(0, 0, 1, 1), edge);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), new Rectangle(0, 0, 1, 1), edge * 0.7f);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 2, rect.Height), new Rectangle(0, 0, 1, 1), edge * 0.85f);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), new Rectangle(0, 0, 1, 1), edge * 0.85f);
            DrawCornerStar(spriteBatch, new Vector2(rect.X + 10, rect.Y + 10), alpha * (0.9f + hoverGlow * 0.4f));
            DrawCornerStar(spriteBatch, new Vector2(rect.Right - 10, rect.Y + 10), alpha * (0.9f + hoverGlow * 0.4f));
            DrawCornerStar(spriteBatch, new Vector2(rect.X + 10, rect.Bottom - 10), alpha * (0.6f + hoverGlow * 0.3f));
            DrawCornerStar(spriteBatch, new Vector2(rect.Right - 10, rect.Bottom - 10), alpha * (0.6f + hoverGlow * 0.3f));
        }

        private static void DrawCornerStar(SpriteBatch spriteBatch, Vector2 pos, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            const float size = 5f;
            Color color = new Color(150, 230, 255) * alpha;
            spriteBatch.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), color, 0f, new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.26f), SpriteEffects.None, 0f);
            spriteBatch.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), color * 0.8f, MathHelper.PiOver2, new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.26f), SpriteEffects.None, 0f);
        }
    }
}
