using CalamityOverhaul.Content.Narrative.Presentation.Skins.Common;
using CalamityOverhaul.Content.UIs.UIEffect;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.Narrative.Presentation.Skins.Brimstone
{
    internal enum BrimstoneParticleMode
    {
        Dialogue,
        Choice,
        Popup,
    }

    internal sealed class BrimstonePanelState
    {
        public const int ShaderEdgePad = 16;

        public float FlameTimer;
        public float EmberGlowTimer;
        public float InfernoPulse;
        public float ShaderTime;

        private readonly List<EmberPRT> _embers = [];
        private readonly List<AshPRT> _ashes = [];
        private readonly List<FlameWispPRT> _wisps = [];
        private readonly List<BrimstoneChoiceEmber> _choiceEmbers = [];

        private int _emberSpawnTimer;
        private int _ashSpawnTimer;
        private int _wispSpawnTimer;
        private int _choiceEmberTimer;
        private BrimstoneParticleMode _mode = BrimstoneParticleMode.Dialogue;
        private const float ParticleSideMargin = 30f;

        public void Update(Rectangle panelRect, bool active, BrimstoneParticleMode mode = BrimstoneParticleMode.Dialogue, float panelAlpha = 1f) {
            _mode = mode;
            FlameTimer = SkinAnimUtil.WrapTimer(FlameTimer, 0.045f);
            EmberGlowTimer = SkinAnimUtil.WrapTimer(EmberGlowTimer, 0.038f);
            InfernoPulse = SkinAnimUtil.WrapTimer(InfernoPulse, 0.012f);
            ShaderTime = SkinAnimUtil.AdvanceShaderTime(ShaderTime);

            if (!active) {
                return;
            }

            Vector2 panelPos = panelRect.Location.ToVector2();
            Vector2 panelSize = panelRect.Size();

            switch (mode) {
                case BrimstoneParticleMode.Choice:
                    UpdateChoiceEmbers(panelRect);
                    break;
                case BrimstoneParticleMode.Popup:
                    if (panelAlpha > 0.6f) {
                        UpdatePopupParticles(panelPos, panelSize);
                    }
                    break;
                default:
                    UpdateDialogueParticles(panelPos, panelSize);
                    break;
            }
        }

        public void DrawParticles(SpriteBatch spriteBatch, float alpha) {
            switch (_mode) {
                case BrimstoneParticleMode.Choice:
                    foreach (BrimstoneChoiceEmber ember in _choiceEmbers) {
                        ember.Draw(spriteBatch, alpha * 0.9f);
                    }
                    break;
                case BrimstoneParticleMode.Popup:
                    foreach (AshPRT ash in _ashes) {
                        ash.Draw(spriteBatch, alpha * 0.7f);
                    }
                    foreach (FlameWispPRT wisp in _wisps) {
                        wisp.Draw(spriteBatch, alpha * 0.8f);
                    }
                    foreach (EmberPRT ember in _embers) {
                        ember.Draw(spriteBatch, alpha * 0.95f);
                    }
                    break;
                default:
                    foreach (AshPRT ash in _ashes) {
                        ash.Draw(spriteBatch, alpha * 0.55f);
                    }
                    foreach (EmberPRT ember in _embers) {
                        ember.Draw(spriteBatch, alpha * 0.7f);
                    }
                    break;
            }
        }

        public void Reset() {
            FlameTimer = 0f;
            EmberGlowTimer = 0f;
            InfernoPulse = 0f;
            ShaderTime = 0f;
            _embers.Clear();
            _ashes.Clear();
            _wisps.Clear();
            _choiceEmbers.Clear();
            _emberSpawnTimer = 0;
            _ashSpawnTimer = 0;
            _wispSpawnTimer = 0;
            _choiceEmberTimer = 0;
        }

        public Color EdgeColor(float alpha) {
            float pulse = (float)Math.Sin(FlameTimer * 1.8f) * 0.5f + 0.5f;
            return Color.Lerp(new Color(180, 60, 30), new Color(255, 140, 70), pulse) * (alpha * 0.85f);
        }

        private void UpdateDialogueParticles(Vector2 panelPos, Vector2 panelSize) {
            _emberSpawnTimer++;
            if (_emberSpawnTimer >= 18 && _embers.Count < 14) {
                _emberSpawnTimer = 0;
                float xPos = Main.rand.NextFloat(panelPos.X + ParticleSideMargin, panelPos.X + panelSize.X - ParticleSideMargin);
                _embers.Add(new EmberPRT(new Vector2(xPos, panelPos.Y + panelSize.Y - 5f)) {
                    Size = Main.rand.NextFloat(1.1f, 2.0f),
                    RiseSpeed = Main.rand.NextFloat(0.25f, 0.6f),
                    Drift = Main.rand.NextFloat(-0.18f, 0.18f)
                });
            }
            UpdateEmbers(panelPos, panelSize);

            _ashSpawnTimer++;
            if (_ashSpawnTimer >= 24 && _ashes.Count < 14) {
                _ashSpawnTimer = 0;
                float xPos = Main.rand.NextFloat(panelPos.X + ParticleSideMargin, panelPos.X + panelSize.X - ParticleSideMargin);
                _ashes.Add(new AshPRT(new Vector2(xPos, panelPos.Y + panelSize.Y)));
            }
            UpdateAshes(panelPos, panelSize);
        }

        private void UpdateChoiceEmbers(Rectangle panelRect) {
            _choiceEmberTimer++;
            if (_choiceEmberTimer >= 6 && _choiceEmbers.Count < 20) {
                _choiceEmberTimer = 0;
                float xPos = Main.rand.NextFloat(panelRect.X + 15f, panelRect.Right - 15f);
                _choiceEmbers.Add(new BrimstoneChoiceEmber(new Vector2(xPos, panelRect.Bottom - 5f)));
            }

            for (int i = _choiceEmbers.Count - 1; i >= 0; i--) {
                if (_choiceEmbers[i].Update(panelRect)) {
                    _choiceEmbers.RemoveAt(i);
                }
            }
        }

        private void UpdatePopupParticles(Vector2 panelPos, Vector2 panelSize) {
            Vector2 basePos = panelPos + panelSize / 2f;

            _emberSpawnTimer++;
            if (_emberSpawnTimer >= 8 && _embers.Count < 35) {
                _emberSpawnTimer = 0;
                float xPos = Main.rand.NextFloat(basePos.X - 120f + ParticleSideMargin, basePos.X + 120f - ParticleSideMargin);
                _embers.Add(new EmberPRT(new Vector2(xPos, basePos.Y + 66f - 5f)));
            }
            for (int i = _embers.Count - 1; i >= 0; i--) {
                if (_embers[i].Update(basePos)) {
                    _embers.RemoveAt(i);
                }
            }

            _ashSpawnTimer++;
            if (_ashSpawnTimer >= 12 && _ashes.Count < 25) {
                _ashSpawnTimer = 0;
                float xPos = Main.rand.NextFloat(basePos.X - 120f + ParticleSideMargin, basePos.X + 120f - ParticleSideMargin);
                _ashes.Add(new AshPRT(new Vector2(xPos, basePos.Y + 66f)));
            }
            for (int i = _ashes.Count - 1; i >= 0; i--) {
                if (_ashes[i].Update(basePos)) {
                    _ashes.RemoveAt(i);
                }
            }

            _wispSpawnTimer++;
            if (_wispSpawnTimer >= 45 && _wisps.Count < 8) {
                _wispSpawnTimer = 0;
                Vector2 startPos = new(
                    Main.rand.NextFloat(basePos.X - 80f, basePos.X + 80f),
                    Main.rand.NextFloat(basePos.Y - 40f, basePos.Y + 40f));
                var wisp = new FlameWispPRT(startPos) {
                    Size = Main.rand.NextFloat(8f, 12f)
                };
                _wisps.Add(wisp);
            }
            for (int i = _wisps.Count - 1; i >= 0; i--) {
                if (_wisps[i].Update(basePos)) {
                    _wisps.RemoveAt(i);
                }
            }
        }

        private void UpdateEmbers(Vector2 pos, Vector2 size) {
            for (int i = _embers.Count - 1; i >= 0; i--) {
                if (_embers[i].Update(pos, size)) {
                    _embers.RemoveAt(i);
                }
            }
        }

        private void UpdateAshes(Vector2 pos, Vector2 size) {
            for (int i = _ashes.Count - 1; i >= 0; i--) {
                if (_ashes[i].Update(pos, size)) {
                    _ashes.RemoveAt(i);
                }
            }
        }

        private sealed class BrimstoneChoiceEmber
        {
            public Vector2 Pos;
            public float Size;
            public float RiseSpeed;
            public float Drift;
            public float Life;
            public float MaxLife;
            public float Seed;
            public float Rotation;
            public float RotationSpeed;

            public BrimstoneChoiceEmber(Vector2 start) {
                Pos = start;
                Size = Main.rand.NextFloat(2f, 4.5f);
                RiseSpeed = Main.rand.NextFloat(0.5f, 1.2f);
                Drift = Main.rand.NextFloat(-0.3f, 0.3f);
                MaxLife = Main.rand.NextFloat(60f, 110f);
                Seed = Main.rand.NextFloat(10f);
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
                RotationSpeed = Main.rand.NextFloat(-0.06f, 0.06f);
            }

            public bool Update(Rectangle bounds) {
                Life++;
                float t = Life / MaxLife;
                Pos.Y -= RiseSpeed * (1f - t * 0.3f);
                Pos.X += (float)Math.Sin(Life * 0.06f + Seed) * Drift;
                Rotation += RotationSpeed;
                return Life >= MaxLife || Pos.Y < bounds.Y - 10f;
            }

            public void Draw(SpriteBatch sb, float alpha) {
                Texture2D pixel = VaultAsset.placeholder2.Value;
                float t = Life / MaxLife;
                float fade = (float)Math.Sin(t * Math.PI);
                float scale = Size * (1f + (float)Math.Sin((Life + Seed * 20f) * 0.12f) * 0.15f);
                Color emberCore = Color.Lerp(new Color(255, 180, 80), new Color(255, 80, 40), t) * (alpha * 0.85f * fade);
                Color emberGlow = Color.Lerp(new Color(255, 140, 60), new Color(180, 40, 20), t) * (alpha * 0.5f * fade);
                sb.Draw(pixel, Pos, new Rectangle(0, 0, 1, 1), emberGlow, 0f, new Vector2(0.5f, 0.5f), scale * 2.2f, SpriteEffects.None, 0f);
                sb.Draw(pixel, Pos, new Rectangle(0, 0, 1, 1), emberCore, Rotation, new Vector2(0.5f, 0.5f), scale, SpriteEffects.None, 0f);
            }
        }
    }
}
